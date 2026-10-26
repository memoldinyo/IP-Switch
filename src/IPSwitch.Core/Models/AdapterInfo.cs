using IPSwitch.Core.Localization;
using System.Net.NetworkInformation;

namespace IPSwitch.Core.Models;

/// <summary>
/// Momentaufnahme eines Netzwerkadapters mit Status, aktiver und gespeicherter IPv4-Konfiguration.
/// </summary>
public sealed class AdapterInfo
{
    /// <summary>Interface-GUID ("{...}") – stabile Kennung, unabhängig vom Anzeigenamen.</summary>
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    /// <summary>MAC-Adresse im Format "AA-BB-CC-DD-EE-FF" oder null.</summary>
    public string? MacAddress { get; init; }

    public NetworkInterfaceType InterfaceType { get; init; }

    public OperationalStatus Status { get; init; }

    /// <summary>Verbindungsgeschwindigkeit in Bit/s (≤ 0 = unbekannt).</summary>
    public long Speed { get; init; }

    /// <summary>IPv4-Interface-Index (für netsh). null, wenn IPv4 nicht gebunden ist.</summary>
    public int? IPv4Index { get; init; }

    public bool SupportsIPv4 => IPv4Index is not null;

    /// <summary>Aktuell aktive DHCP-Einstellung laut Netzwerkstack.</summary>
    public bool DhcpEnabled { get; init; }

    public IReadOnlyList<ActiveAddress> Addresses { get; init; } = [];

    public IReadOnlyList<string> IPv6Addresses { get; init; } = [];

    public IReadOnlyList<string> Gateways { get; init; } = [];

    public IReadOnlyList<string> DnsServers { get; init; } = [];

    public IReadOnlyList<string> DhcpServers { get; init; } = [];

    /// <summary>Gespeicherte (persistente) Konfiguration aus der TCP/IP-Registry, sofern lesbar.</summary>
    public AdapterConfiguration? PersistentConfiguration { get; init; }

    public bool IsConnected => Status == OperationalStatus.Up;

    public string StatusText => Status switch
    {
        OperationalStatus.Up => Loc.T("status.up"),
        OperationalStatus.Down => Loc.T("status.down"),
        OperationalStatus.Dormant => Loc.T("status.dormant"),
        OperationalStatus.NotPresent => Loc.T("status.notPresent"),
        OperationalStatus.LowerLayerDown => Loc.T("status.lowerLayerDown"),
        OperationalStatus.Testing => Loc.T("status.testing"),
        _ => Loc.T("status.unknown"),
    };

    public string TypeText => InterfaceType switch
    {
        NetworkInterfaceType.Ethernet or NetworkInterfaceType.Ethernet3Megabit
            or NetworkInterfaceType.FastEthernetT or NetworkInterfaceType.FastEthernetFx
            or NetworkInterfaceType.GigabitEthernet => "Ethernet",
        NetworkInterfaceType.Wireless80211 => Loc.T("type.wifi"),
        NetworkInterfaceType.Wwanpp or NetworkInterfaceType.Wwanpp2 => Loc.T("type.mobile"),
        NetworkInterfaceType.Ppp => "PPP",
        NetworkInterfaceType.Tunnel => "Tunnel",
        NetworkInterfaceType.Loopback => "Loopback",
        (NetworkInterfaceType)53 => Loc.T("type.virtual"), // IF_TYPE_PROP_VIRTUAL (TAP-/VPN-Adapter)
        _ => InterfaceType.ToString(),
    };

    public string SpeedText => Speed switch
    {
        <= 0 => string.Empty,
        >= 1_000_000_000 when Speed % 1_000_000_000 == 0 => $"{Speed / 1_000_000_000} Gbit/s",
        >= 1_000_000 => $"{Speed / 1_000_000} Mbit/s",
        >= 1_000 => $"{Speed / 1_000} kbit/s",
        _ => $"{Speed} bit/s",
    };

    /// <summary>Erste IPv4-Adresse mit Präfix, z. B. "192.168.1.10/24".</summary>
    public string PrimaryAddressText => Addresses.Count > 0
        ? $"{Addresses[0].Address}/{Addresses[0].PrefixLength}"
        : PersistentConfiguration is { DhcpEnabled: false, Addresses.Count: > 0 } pc
            ? $"{pc.Addresses[0].Address}/{Validation.IpValidator.MaskToPrefix(pc.Addresses[0].SubnetMask)}"
            : "–";

    public string ModeText => (PersistentConfiguration?.DhcpEnabled ?? DhcpEnabled) ? Loc.T("mode.dhcp") : Loc.T("mode.static");

    public override string ToString() => Name;
}

public sealed record ActiveAddress(string Address, int PrefixLength, string SubnetMask);
