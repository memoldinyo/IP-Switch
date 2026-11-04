using System.Net.NetworkInformation;
using IPSwitch.Core.Elevation;
using IPSwitch.Core.Models;
using IPSwitch.Core.Network;

namespace IPSwitch.Core.Tests;

public class AdapterStateTests
{
    private const string Id = "{11111111-2222-3333-4444-555555555555}";

    /// <summary>Simuliert einen Adapter, dessen Gerätezustand sich durch netsh bzw. PowerShell umschalten lässt.</summary>
    private sealed class FakeDevice : ICommandRunner, IAdapterLocator
    {
        public bool Disabled { get; set; }

        public bool NetshWorks { get; set; } = true;

        public bool PowerShellWorks { get; set; } = true;

        public List<string> Calls { get; } = [];

        public CommandResult Run(string fileName, IReadOnlyList<string> arguments, TimeSpan timeout)
        {
            var tool = Path.GetFileNameWithoutExtension(fileName);
            Calls.Add($"{tool} {string.Join(' ', arguments)}");
            var works = tool == "netsh" ? NetshWorks : PowerShellWorks;
            if (!works)
            {
                return new CommandResult(1, "Simulierter Fehler.");
            }

            var line = string.Join(' ', arguments);
            Disabled = line.Contains("admin=disabled") || line.Contains("Disable-NetAdapter");
            return new CommandResult(0, "Ok.");
        }

        public AdapterInfo? Find(string interfaceId, string? macAddress) => AdapterService.SameId(interfaceId, Id)
            ? new AdapterInfo
            {
                Id = Id,
                Name = "Ethernet 2",
                Description = "Testadapter",
                InterfaceType = NetworkInterfaceType.Ethernet,
                Status = Disabled ? OperationalStatus.Down : OperationalStatus.Up,
                IsDisabled = Disabled,
                IPv4Index = Disabled ? null : 7,
            }
            : null;

        public IReadOnlyList<AdapterInfo> GetAll() => Find(Id, null) is { } a ? [a] : [];
    }

    private static AdapterStateChanger Changer(FakeDevice device) =>
        new(device, device, _ => device.Disabled, "netsh.exe", "powershell.exe", verifyAttempts: 2, verifyDelay: TimeSpan.Zero);

    [Fact]
    public void Disable_UsesNetshWithAdapterNameAndVerifies()
    {
        var device = new FakeDevice();

        var result = Changer(device).SetEnabled(Id, enable: false);

        Assert.True(result.Success, result.Message);
        Assert.True(device.Disabled);
        Assert.Equal(["netsh interface set interface Ethernet 2 admin=disabled"], device.Calls);
        Assert.Contains("deaktiviert", result.Message);
    }

    [Fact]
    public void Enable_DisabledAdapter()
    {
        var device = new FakeDevice { Disabled = true };

        var result = Changer(device).SetEnabled(Id, enable: true);

        Assert.True(result.Success, result.Message);
        Assert.False(device.Disabled);
        Assert.Single(device.Calls, c => c.EndsWith("admin=enabled"));
    }

    [Fact]
    public void AlreadyInTargetState_RunsNothing()
    {
        var device = new FakeDevice();

        var result = Changer(device).SetEnabled(Id, enable: true);

        Assert.True(result.Success);
        Assert.Empty(device.Calls);
        Assert.Contains("bereits aktiviert", result.Message);
    }

    [Fact]
    public void NetshFails_FallsBackToPowerShellByGuid()
    {
        var device = new FakeDevice { NetshWorks = false };

        var result = Changer(device).SetEnabled(Id, enable: false);

        Assert.True(result.Success, result.Message);
        Assert.Equal(2, device.Calls.Count);
        Assert.StartsWith("powershell -NoProfile -NonInteractive -Command ", device.Calls[1]);
        Assert.Contains("InterfaceGuid -eq '{11111111-2222-3333-4444-555555555555}'", device.Calls[1]);
        Assert.Contains("Disable-NetAdapter -Confirm:$false", device.Calls[1]);
    }

    [Fact]
    public void BothFail_ReportsFailureWithoutChange()
    {
        var device = new FakeDevice { NetshWorks = false, PowerShellWorks = false };

        var result = Changer(device).SetEnabled(Id, enable: false);

        Assert.Equal(ApplyOutcome.FailedNoChanges, result.Outcome);
        Assert.False(device.Disabled);
        Assert.Contains("konnte nicht deaktiviert werden", result.Message);
        Assert.Contains("Simulierter Fehler", result.Message);
    }

    [Theory]
    [InlineData("kein-guid")]
    [InlineData("{11111111-2222-3333-4444-555555555555}' ; Remove-Item x")]
    public void InvalidId_IsRejectedWithoutCommands(string id)
    {
        var device = new FakeDevice();

        var result = Changer(device).SetEnabled(id, enable: false);

        Assert.Equal(ApplyOutcome.InvalidRequest, result.Outcome);
        Assert.Empty(device.Calls);
    }

    [Fact]
    public void UnknownAdapter_IsRejected()
    {
        var device = new FakeDevice();
        var result = Changer(device).SetEnabled("{99999999-2222-3333-4444-555555555555}", enable: false);
        Assert.Equal(ApplyOutcome.InvalidRequest, result.Outcome);
        Assert.Empty(device.Calls);
    }

    [Fact]
    public void Protocol_AdapterStateArguments() =>
        Assert.Equal($"--adapter-state disable {Id} de \"C:\\x\\r.json\"",
            ElevatedProtocol.BuildAdapterStateArguments(false, Id, "de", @"C:\x\r.json"));

    [Theory]
    [InlineData("--apply", true)]
    [InlineData("--admin-autostart", true)]
    [InlineData("--adapter-state", true)]
    [InlineData("--autostart", false)]
    [InlineData(null, false)]
    public void Protocol_EveryHelperSwitchStartsTheHelper(string? argument, bool expected) =>
        Assert.Equal(expected, ElevatedProtocol.IsHelperSwitch(argument));

    [Fact]
    public void Protocol_BuiltArgumentsStartWithAHelperSwitch()
    {
        var request = new ApplyRequest { InterfaceId = Id, Target = new AdapterConfiguration { DhcpEnabled = true, DnsFromDhcp = true } };
        foreach (var line in new[]
                 {
                     ElevatedProtocol.BuildArguments(request, @"C:\x\r.json"),
                     ElevatedProtocol.BuildAdminAutostartArguments(true, "S-1-5-21-1-2-3-1001", "de", @"C:\x\r.json"),
                     ElevatedProtocol.BuildAdapterStateArguments(true, Id, "de", @"C:\x\r.json"),
                 })
        {
            Assert.True(ElevatedProtocol.IsHelperSwitch(line.Split(' ')[0]), line);
        }
    }

    [Fact]
    public void DisabledAdapter_StatusText() =>
        Assert.Equal("Deaktiviert", new AdapterInfo { Id = Id, Name = "x", Description = "x", Status = OperationalStatus.Down, IsDisabled = true }.StatusText);

    [Fact]
    public void Apply_ToDisabledAdapter_IsRejectedBeforeAnyChange()
    {
        var fake = new FakeNetsh(new AdapterConfiguration { DhcpEnabled = true, DnsFromDhcp = true });
        var disabled = new DisabledLocator(fake);
        var configurator = new NetworkConfigurator(fake, fake, disabled, "netsh.exe", verifyAttempts: 1, verifyDelay: TimeSpan.Zero);

        var result = configurator.Apply(new ApplyRequest
        {
            InterfaceId = FakeNetsh.AdapterId,
            Target = new AdapterConfiguration { DhcpEnabled = true, DnsFromDhcp = true },
            BackupDirectory = Path.GetTempPath(),
        });

        Assert.Equal(ApplyOutcome.InvalidRequest, result.Outcome);
        Assert.Contains("deaktiviert", result.Message);
        Assert.Empty(fake.Executed);
    }

    private sealed class DisabledLocator(FakeNetsh inner) : IAdapterLocator
    {
        public AdapterInfo? Find(string interfaceId, string? macAddress) => inner.Find(interfaceId, macAddress) is { } a
            ? new AdapterInfo { Id = a.Id, Name = a.Name, Description = a.Description, Status = OperationalStatus.Down, IsDisabled = true }
            : null;

        public IReadOnlyList<AdapterInfo> GetAll() => Find(FakeNetsh.AdapterId, null) is { } a ? [a] : [];
    }
}
