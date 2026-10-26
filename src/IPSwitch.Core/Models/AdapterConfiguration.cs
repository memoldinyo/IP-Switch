using IPSwitch.Core.Localization;
namespace IPSwitch.Core.Models;

/// <summary>
/// Persistente IPv4-Konfiguration eines Adapters. Wird sowohl als Ziel beim Anwenden eines Profils
/// als auch als Sicherung (vor jeder Änderung) verwendet.
/// </summary>
public sealed class AdapterConfiguration
{
    public bool DhcpEnabled { get; set; }

    /// <summary>Statische Adressen (nur relevant, wenn <see cref="DhcpEnabled"/> false ist).</summary>
    public List<StaticAddress> Addresses { get; set; } = [];

    /// <summary>Statische Standardgateways (nur relevant, wenn <see cref="DhcpEnabled"/> false ist).</summary>
    public List<string> Gateways { get; set; } = [];

    /// <summary>true = DNS-Server werden per DHCP bezogen.</summary>
    public bool DnsFromDhcp { get; set; }

    /// <summary>Statisch konfigurierte DNS-Server in Prioritätsreihenfolge.</summary>
    public List<string> DnsServers { get; set; } = [];

    /// <summary>
    /// Nur bei DHCP: statische Adressen, die Windows beim Umschalten auf DHCP nicht entfernt hat. Sie bleiben aktiv,
    /// blockieren die Adresse für andere Adapter und werden beim nächsten Anwenden von DHCP gelöscht.
    /// </summary>
    public List<StaticAddress> ResidualAddresses { get; set; } = [];

    public AdapterConfiguration Clone() => new()
    {
        DhcpEnabled = DhcpEnabled,
        Addresses = Addresses.Select(a => new StaticAddress(a.Address, a.SubnetMask)).ToList(),
        ResidualAddresses = ResidualAddresses.Select(a => new StaticAddress(a.Address, a.SubnetMask)).ToList(),
        Gateways = [.. Gateways],
        DnsFromDhcp = DnsFromDhcp,
        DnsServers = [.. DnsServers],
    };

    /// <summary>Kurze, einzeilige Beschreibung für Listen und Meldungen.</summary>
    public string Summary
    {
        get
        {
            var ip = DhcpEnabled
                ? Loc.T("mode.dhcp")
                : Addresses.Count == 0
                    ? Loc.T("summary.staticNoAddress")
                    : string.Join(", ", Addresses.Select(a => $"{a.Address}/{Validation.IpValidator.MaskToPrefix(a.SubnetMask)}"));
            var gw = !DhcpEnabled && Gateways.Count > 0 ? ", " + Loc.T("summary.gateway", string.Join(", ", Gateways)) : string.Empty;
            var dns = DnsFromDhcp
                ? ", " + Loc.T("summary.dnsDhcp")
                : DnsServers.Count > 0 ? ", " + Loc.T("summary.dns", string.Join(", ", DnsServers)) : ", " + Loc.T("summary.noDns");
            return ip + gw + dns;
        }
    }
}

public sealed record StaticAddress(string Address, string SubnetMask);
