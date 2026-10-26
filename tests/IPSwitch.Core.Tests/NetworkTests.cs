using System.Net.NetworkInformation;
using IPSwitch.Core.Elevation;
using IPSwitch.Core.Models;
using IPSwitch.Core.Network;
using IPSwitch.Core.Storage;

namespace IPSwitch.Core.Tests;

public sealed class NetworkTests : IDisposable
{
    private readonly string _backupDir = Path.Combine(Path.GetTempPath(), "IPSwitchTests", Guid.NewGuid().ToString("N"), "backups");

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path.GetDirectoryName(_backupDir)!, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private static AdapterConfiguration Dhcp() => new() { DhcpEnabled = true, DnsFromDhcp = true };

    private static AdapterConfiguration Static(string ip = "192.168.0.50", string? gateway = "192.168.0.1", params string[] dns) => new()
    {
        DhcpEnabled = false,
        Addresses = [new StaticAddress(ip, "255.255.255.0")],
        Gateways = gateway is null ? [] : [gateway],
        DnsServers = [.. dns],
    };

    private ApplyRequest Request(AdapterConfiguration target) => new()
    {
        InterfaceId = FakeNetsh.AdapterId,
        Target = target,
        Label = "Test",
        BackupDirectory = _backupDir,
    };

    private static NetworkConfigurator Configurator(FakeNetsh fake) =>
        new(fake, fake, fake, netshPath: "netsh.exe", verifyAttempts: 2, verifyDelay: TimeSpan.Zero);

    // ---------- NetshCommandBuilder ----------

    [Fact]
    public void Build_Static_UsesIndexMaskGatewayAndDns()
    {
        var commands = NetshCommandBuilder.Build(12, Static(dns: ["1.1.1.1", "8.8.8.8"]), Dhcp());
        var lines = commands.Select(c => c.CommandLine).ToList();

        Assert.Equal("netsh interface ipv4 set address name=12 source=static address=192.168.0.50 mask=255.255.255.0 gateway=192.168.0.1", lines[0]);
        Assert.Equal("netsh interface ipv4 set dnsservers name=12 source=static address=1.1.1.1 validate=no", lines[1]);
        Assert.Equal("netsh interface ipv4 add dnsservers name=12 address=8.8.8.8 index=2 validate=no", lines[2]);
        Assert.Equal(3, lines.Count);
    }

    [Fact]
    public void Build_StaticWithoutGatewayAndDns_UsesNone()
    {
        var lines = NetshCommandBuilder.Build(3, Static(gateway: null), Dhcp()).Select(c => c.CommandLine).ToList();
        Assert.EndsWith("gateway=none", lines[0]);
        Assert.Contains("address=none", lines[1]);
    }

    [Fact]
    public void Build_DhcpFromStatic_RemovesGatewaysAndStaticAddresses()
    {
        var commands = NetshCommandBuilder.Build(5, Dhcp(), Static());
        Assert.Collection(commands,
            c => Assert.Contains("delete address name=5 gateway=all", c.CommandLine),
            c => Assert.Contains("set address name=5 source=dhcp", c.CommandLine),
            c => Assert.Contains("delete address name=5 address=192.168.0.50", c.CommandLine),
            c => Assert.Contains("set dnsservers name=5 source=dhcp", c.CommandLine));
        Assert.True(commands[0].AllowFailure);
        Assert.False(commands[1].AllowFailure);
        Assert.True(commands[2].AllowFailure);
    }

    [Fact]
    public void Build_DhcpWhenAlreadyDhcp_SkipsRedundantCommands() =>
        Assert.Empty(NetshCommandBuilder.Build(5, Dhcp(), Dhcp()));

    [Fact]
    public void Build_DhcpWhenAlreadyDhcp_RemovesResidualAddresses()
    {
        var current = Dhcp();
        current.ResidualAddresses.Add(new StaticAddress("10.10.10.5", "255.255.255.0"));
        var commands = NetshCommandBuilder.Build(16, Dhcp(), current);
        Assert.Equal("netsh interface ipv4 delete address name=16 address=10.10.10.5", Assert.Single(commands).CommandLine);
    }

    [Fact]
    public void Apply_StaticToDhcp_LeavesNoResidualAddress()
    {
        // Reproduziert den Fehler: Windows lässt die statische Adresse beim Wechsel auf DHCP stehen.
        var fake = new FakeNetsh(Static("10.10.10.5", null));

        var result = Configurator(fake).Apply(Request(Dhcp()));

        Assert.Equal(ApplyOutcome.Success, result.Outcome);
        Assert.Empty(fake.ReadConfiguration(FakeNetsh.AdapterId)!.ResidualAddresses);
    }

    [Fact]
    public void Apply_DhcpCleansUpExistingResidualAddress()
    {
        var residual = Static("10.10.10.5", null);
        residual.DhcpEnabled = true;
        var fake = new FakeNetsh(residual);

        var result = Configurator(fake).Apply(Request(Dhcp()));

        Assert.Equal(ApplyOutcome.Success, result.Outcome);
        Assert.Contains(fake.Executed, l => l.Contains("delete address name=7 address=10.10.10.5"));
        Assert.Empty(fake.State.Addresses);
    }

    [Fact]
    public void Apply_AddressUsedByOtherAdapter_IsRejectedWithoutChanges()
    {
        var fake = new FakeNetsh(Dhcp());
        var otherConfig = new AdapterConfiguration { DhcpEnabled = true, DnsFromDhcp = true };
        otherConfig.ResidualAddresses.Add(new StaticAddress("192.168.0.50", "255.255.255.0"));
        fake.OtherAdapters.Add(new AdapterInfo
        {
            Id = "{99999999-0000-0000-0000-000000000000}",
            Name = "Lenovo Docking station",
            Description = "USB-Ethernet",
            Status = OperationalStatus.Down,
            IPv4Index = 16,
            PersistentConfiguration = otherConfig,
        });

        var result = Configurator(fake).Apply(Request(Static()));

        Assert.Equal(ApplyOutcome.InvalidRequest, result.Outcome);
        Assert.Contains("Lenovo Docking station", result.Message);
        Assert.Contains("Überbleibsel", result.Message);
        Assert.Empty(fake.Executed);
        Assert.False(Directory.Exists(_backupDir));
    }

    [Fact]
    public void AddressConflicts_IgnoresOwnAdapterAndDhcpTargets()
    {
        var self = new AdapterInfo
        {
            Id = FakeNetsh.AdapterId,
            Name = "Ethernet",
            Description = "x",
            Addresses = [new ActiveAddress("192.168.0.50", 24, "255.255.255.0")],
        };
        Assert.Null(AddressConflicts.Find([self], FakeNetsh.AdapterId, Static()));
        Assert.Null(AddressConflicts.Find([self], Guid.NewGuid().ToString("B"), Dhcp()));
        Assert.NotNull(AddressConflicts.Find([self], Guid.NewGuid().ToString("B"), Static()));
    }

    [Fact]
    public void Build_StaticWithoutAddress_Throws() =>
        Assert.Throws<ArgumentException>(() => NetshCommandBuilder.Build(1, new AdapterConfiguration(), null));

    // ---------- NetworkConfigurator ----------

    [Fact]
    public void Apply_Success_ChangesStateAndWritesBackup()
    {
        var fake = new FakeNetsh(Dhcp());
        var target = Static(dns: ["192.168.0.1", "9.9.9.9"]);

        var result = Configurator(fake).Apply(Request(target));

        Assert.Equal(ApplyOutcome.Success, result.Outcome);
        Assert.Null(NetworkConfigurator.Compare(target, fake.ReadConfiguration(FakeNetsh.AdapterId)!));
        var backup = Assert.Single(new BackupStore(_backupDir).List(FakeNetsh.AdapterId));
        Assert.True(backup.Configuration.DhcpEnabled);
        Assert.Equal(result.BackupFile, backup.FilePath);
    }

    [Fact]
    public void Apply_CommandFails_RestoresPreviousConfiguration()
    {
        var original = Static("10.0.0.5", "10.0.0.1", "10.0.0.1");
        original.Addresses[0] = new StaticAddress("10.0.0.5", "255.255.255.0");
        var fake = new FakeNetsh(original)
        {
            // Erster DNS-Befehl des Ziels schlägt fehl, die Wiederherstellung (10.0.0.1) funktioniert.
            FailWhen = line => line.Contains("address=8.8.8.8"),
        };

        var result = Configurator(fake).Apply(Request(Static(dns: ["8.8.8.8"])));

        Assert.Equal(ApplyOutcome.FailedRolledBack, result.Outcome);
        Assert.Contains("wiederhergestellt", result.Message);
        Assert.Null(NetworkConfigurator.Compare(original, fake.ReadConfiguration(FakeNetsh.AdapterId)!));
        Assert.Contains(fake.Executed, l => l.Contains("address=10.0.0.5"));
    }

    [Fact]
    public void Apply_VerificationFails_RollsBack()
    {
        var fake = new FakeNetsh(Dhcp()) { IgnoreDnsChanges = true };

        var result = Configurator(fake).Apply(Request(Static(dns: ["1.1.1.1"])));

        Assert.Equal(ApplyOutcome.FailedRolledBack, result.Outcome);
        Assert.Contains("Überprüfung", result.Message);
        Assert.True(fake.State.DhcpEnabled);
    }

    [Fact]
    public void Apply_RollbackFails_ReportsIt()
    {
        var fake = new FakeNetsh(Static("10.0.0.5", null)) { FailWhen = line => line.Contains("set address") };

        var result = Configurator(fake).Apply(Request(Dhcp()));

        Assert.Equal(ApplyOutcome.FailedRollbackFailed, result.Outcome);
        Assert.Contains("NICHT", result.Message);
    }

    [Fact]
    public void Apply_InvalidTarget_DoesNothing()
    {
        var fake = new FakeNetsh(Dhcp());
        var invalid = new AdapterConfiguration { DhcpEnabled = false, Addresses = [new StaticAddress("999.0.0.1", "255.0.0.0")] };

        var result = Configurator(fake).Apply(Request(invalid));

        Assert.Equal(ApplyOutcome.InvalidRequest, result.Outcome);
        Assert.Empty(fake.Executed);
        Assert.False(Directory.Exists(_backupDir));
    }

    [Fact]
    public void Apply_AdapterMissing_DoesNothing()
    {
        var fake = new FakeNetsh(Dhcp()) { AdapterPresent = false };
        var result = Configurator(fake).Apply(Request(Static()));
        Assert.Equal(ApplyOutcome.InvalidRequest, result.Outcome);
        Assert.Empty(fake.Executed);
    }

    [Fact]
    public void Apply_BackupNotWritable_DoesNotChangeAnything()
    {
        var fake = new FakeNetsh(Dhcp());
        var blocker = Path.Combine(Path.GetTempPath(), "IPSwitchTests", Guid.NewGuid().ToString("N") + ".file");
        Directory.CreateDirectory(Path.GetDirectoryName(blocker)!);
        File.WriteAllText(blocker, "Datei statt Verzeichnis");
        try
        {
            var request = Request(Static());
            request.BackupDirectory = blocker;
            var result = Configurator(fake).Apply(request);

            Assert.Equal(ApplyOutcome.FailedNoChanges, result.Outcome);
            Assert.Empty(fake.Executed);
        }
        finally
        {
            File.Delete(blocker);
        }
    }

    // ---------- Registry-Parser ----------

    [Fact]
    public void RegistryParse_Static()
    {
        var config = TcpipRegistryReader.Parse(false, ["192.168.1.10", "0.0.0.0"], ["255.255.255.0", "0.0.0.0"], ["192.168.1.1"], "1.1.1.1,8.8.8.8");
        Assert.False(config.DhcpEnabled);
        Assert.Equal(new StaticAddress("192.168.1.10", "255.255.255.0"), Assert.Single(config.Addresses));
        Assert.Equal(["192.168.1.1"], config.Gateways);
        Assert.Equal(["1.1.1.1", "8.8.8.8"], config.DnsServers);
        Assert.False(config.DnsFromDhcp);
    }

    [Fact]
    public void RegistryParse_DhcpIgnoresStaleStaticValues()
    {
        var config = TcpipRegistryReader.Parse(true, ["10.0.0.5"], ["255.0.0.0"], ["10.0.0.1"], "");
        Assert.True(config.DhcpEnabled);
        Assert.Empty(config.Addresses);
        Assert.Empty(config.Gateways);
        Assert.True(config.DnsFromDhcp);
        Assert.Equal(new StaticAddress("10.0.0.5", "255.0.0.0"), Assert.Single(config.ResidualAddresses));
    }

    [Fact]
    public void RegistryParse_SpaceSeparatedDns()
    {
        var config = TcpipRegistryReader.Parse(true, [], [], [], "9.9.9.9 149.112.112.112");
        Assert.Equal(["9.9.9.9", "149.112.112.112"], config.DnsServers);
        Assert.False(config.DnsFromDhcp);
    }

    // ---------- Adapterzuordnung ----------

    private static AdapterInfo Adapter(string id, string? mac) => new()
    {
        Id = id,
        Name = "N" + id,
        Description = "D",
        MacAddress = mac,
        Status = OperationalStatus.Up,
    };

    [Fact]
    public void Match_PrefersGuidAndIgnoresCaseAndBraces()
    {
        var guid = Guid.NewGuid();
        var adapters = new[] { Adapter(guid.ToString("B").ToUpperInvariant(), "AA-AA-AA-AA-AA-AA"), Adapter(Guid.NewGuid().ToString("B"), "BB-BB-BB-BB-BB-BB") };
        Assert.Same(adapters[0], AdapterService.Match(adapters, guid.ToString("D"), "BB-BB-BB-BB-BB-BB"));
    }

    [Fact]
    public void Match_FallsBackToUniqueMac()
    {
        var adapters = new[] { Adapter(Guid.NewGuid().ToString("B"), "AA-AA-AA-AA-AA-AA") };
        Assert.Same(adapters[0], AdapterService.Match(adapters, Guid.NewGuid().ToString("B"), "aa-aa-aa-aa-aa-aa"));
    }

    [Fact]
    public void Match_AmbiguousMac_ReturnsNull()
    {
        var adapters = new[] { Adapter(Guid.NewGuid().ToString("B"), "AA-AA-AA-AA-AA-AA"), Adapter(Guid.NewGuid().ToString("B"), "AA-AA-AA-AA-AA-AA") };
        Assert.Null(AdapterService.Match(adapters, Guid.NewGuid().ToString("B"), "AA-AA-AA-AA-AA-AA"));
    }

    [Fact]
    public void GetAdapters_OnThisMachine_DoesNotThrow()
    {
        var adapters = new AdapterService().GetAdapters(includeSecondary: true);
        Assert.All(adapters, a => Assert.True(Guid.TryParse(a.Id, out _)));
        Assert.All(adapters, a => Assert.NotNull(a.IPv4Index));
    }

    // ---------- Hilfsprozess-Protokoll ----------

    [Fact]
    public void Protocol_RoundTrip()
    {
        var request = Request(Static(dns: ["1.1.1.1"]));
        request.Label = "Profil \"Büro\" & mehr";
        var encoded = ElevatedProtocol.EncodeRequest(request);

        Assert.DoesNotContain(encoded, c => c is '"' or ' ' or '+' or '/' or '=');
        var decoded = ElevatedProtocol.DecodeRequest(encoded);
        Assert.Equal(request.Label, decoded.Label);
        Assert.Equal(request.InterfaceId, decoded.InterfaceId);
        Assert.Null(NetworkConfigurator.Compare(request.Target, decoded.Target));
    }

    [Theory]
    [InlineData(@"C:\Users\x\AppData\Local\IPSwitch\runtime\result-1.json", true)]
    [InlineData(@"relative\result.json", false)]
    [InlineData(@"C:\temp\result.txt", false)]
    [InlineData(@"C:\te""mp\result.json", false)]
    [InlineData("", false)]
    public void Protocol_ValidatesResultPath(string path, bool expected) =>
        Assert.Equal(expected, ElevatedProtocol.IsValidResultPath(path));

    [Fact]
    public void Protocol_RejectsGarbage() =>
        Assert.ThrowsAny<Exception>(() => ElevatedProtocol.DecodeRequest("!!!kein-base64!!!"));
}
