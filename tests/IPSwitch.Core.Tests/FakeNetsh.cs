using System.Net.NetworkInformation;
using IPSwitch.Core.Models;
using IPSwitch.Core.Network;

namespace IPSwitch.Core.Tests;

/// <summary>
/// Simuliert netsh und den persistenten TCP/IP-Speicher eines einzelnen Adapters,
/// damit Anwenden, Überprüfung und Rollback ohne Systemänderung getestet werden können.
/// </summary>
internal sealed class FakeNetsh : ICommandRunner, IConfigurationReader, IAdapterLocator
{
    public const string AdapterId = "{11111111-2222-3333-4444-555555555555}";
    public const int Index = 7;

    public FakeNetsh(AdapterConfiguration initial) => State = initial.Clone();

    public AdapterConfiguration State { get; private set; }

    public List<string> Executed { get; } = [];

    /// <summary>Befehle, für die dies true liefert, schlagen fehl (Exit-Code 1).</summary>
    public Func<string, bool>? FailWhen { get; set; }

    /// <summary>Simuliert, dass DNS-Befehle zwar "erfolgreich" sind, aber nichts bewirken.</summary>
    public bool IgnoreDnsChanges { get; set; }

    public bool AdapterPresent { get; set; } = true;

    public CommandResult Run(string fileName, IReadOnlyList<string> arguments, TimeSpan timeout)
    {
        var line = string.Join(' ', arguments);
        Executed.Add(line);
        if (FailWhen?.Invoke(line) == true)
        {
            return new CommandResult(1, "Simulierter Fehler.");
        }

        Assert.Contains($"name={Index}", arguments);
        var values = arguments.Where(a => a.Contains('='))
            .ToDictionary(a => a[..a.IndexOf('=')], a => a[(a.IndexOf('=') + 1)..]);
        var verb = arguments[2];
        var noun = arguments[3];

        switch (verb, noun)
        {
            case ("set", "address") when values["source"] == "dhcp":
                // Wie Windows: statische Adressen bleiben beim Umschalten auf DHCP erhalten.
                State.DhcpEnabled = true;
                break;
            case ("delete", "address") when values.TryGetValue("address", out var removed):
                State.Addresses.RemoveAll(a => a.Address == removed);
                break;
            case ("set", "address"):
                State.DhcpEnabled = false;
                State.Addresses = [new StaticAddress(values["address"], values["mask"])];
                State.Gateways = values["gateway"] == "none" ? [] : [values["gateway"]];
                break;
            case ("add", "address"):
                if (values.TryGetValue("address", out var address))
                {
                    State.Addresses.Add(new StaticAddress(address, values["mask"]));
                }

                if (values.TryGetValue("gateway", out var gateway))
                {
                    State.Gateways.Add(gateway);
                }

                break;
            case ("delete", "address") when values.GetValueOrDefault("gateway") == "all":
                State.Gateways.Clear();
                break;
            case ("set", "dnsservers") when !IgnoreDnsChanges:
                State.DnsServers = values["source"] == "dhcp" || values.GetValueOrDefault("address") == "none"
                    ? []
                    : [values["address"]];
                break;
            case ("add", "dnsservers") when !IgnoreDnsChanges:
                State.DnsServers.Insert(Math.Min(int.Parse(values["index"]) - 1, State.DnsServers.Count), values["address"]);
                break;
        }

        return new CommandResult(0, "Ok.");
    }

    /// <summary>Liefert den Zustand mit derselben Semantik wie <see cref="TcpipRegistryReader"/>.</summary>
    public AdapterConfiguration? ReadConfiguration(string interfaceId)
    {
        var copy = State.Clone();
        copy.DnsFromDhcp = copy.DhcpEnabled && copy.DnsServers.Count == 0;
        if (copy.DhcpEnabled)
        {
            copy.ResidualAddresses = copy.Addresses;
            copy.Addresses = [];
            copy.Gateways.Clear();
        }

        return copy;
    }

    /// <summary>Weitere Adapter (für Konfliktprüfungen).</summary>
    public List<AdapterInfo> OtherAdapters { get; } = [];

    public IReadOnlyList<AdapterInfo> GetAll() =>
        Find(AdapterId, null) is { } self ? [self, .. OtherAdapters] : OtherAdapters;

    public AdapterInfo? Find(string interfaceId, string? macAddress) => AdapterPresent
        ? new AdapterInfo
        {
            Id = AdapterId,
            Name = "Ethernet",
            Description = "Testadapter",
            MacAddress = "00-11-22-33-44-55",
            InterfaceType = NetworkInterfaceType.Ethernet,
            Status = OperationalStatus.Up,
            IPv4Index = Index,
        }
        : null;
}
