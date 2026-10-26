using IPSwitch.Core.Localization;
using IPSwitch.Core.Models;
using IPSwitch.Core.Validation;

namespace IPSwitch.Core.Profiles;

/// <summary>Wandelt Profile in Adapterkonfigurationen um und umgekehrt.</summary>
public static class ProfileConverter
{
    /// <summary>
    /// Bereinigt ein Profil: Leerzeichen entfernen, leere Felder auf null setzen,
    /// IP-Felder bei DHCP leeren.
    /// </summary>
    public static IpProfile Normalize(IpProfile profile)
    {
        var p = profile.Clone();
        p.Name = p.Name?.Trim() ?? string.Empty;
        p.Description = NullIfEmpty(p.Description);
        p.IpAddress = NormalizeIp(p.IpAddress);
        p.SubnetMask = NullIfEmpty(p.SubnetMask);
        p.Gateway = NormalizeIp(p.Gateway);
        p.PreferredDns = NormalizeIp(p.PreferredDns);
        p.AlternateDns = NormalizeIp(p.AlternateDns);

        if (p.UseDhcp)
        {
            p.IpAddress = null;
            p.SubnetMask = null;
            p.Gateway = null;
        }

        return p;
    }

    /// <summary>Erzeugt die Zielkonfiguration aus einem (gültigen) Profil.</summary>
    public static AdapterConfiguration ToConfiguration(IpProfile profile)
    {
        var validation = ProfileValidator.Validate(profile);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(Loc.T("profile.invalid") + Environment.NewLine + validation.Summary);
        }

        var config = new AdapterConfiguration { DhcpEnabled = profile.UseDhcp };

        if (!profile.UseDhcp)
        {
            IpValidator.TryParseSubnet(profile.SubnetMask, out var prefix, out _);
            config.Addresses.Add(new StaticAddress(IpValidator.NormalizeIPv4(profile.IpAddress!), IpValidator.PrefixToMask(prefix)));
            if (!string.IsNullOrWhiteSpace(profile.Gateway))
            {
                config.Gateways.Add(IpValidator.NormalizeIPv4(profile.Gateway));
            }
        }

        if (!string.IsNullOrWhiteSpace(profile.PreferredDns))
        {
            config.DnsServers.Add(IpValidator.NormalizeIPv4(profile.PreferredDns));
        }

        if (!string.IsNullOrWhiteSpace(profile.AlternateDns))
        {
            config.DnsServers.Add(IpValidator.NormalizeIPv4(profile.AlternateDns));
        }

        config.DnsFromDhcp = profile.UseDhcp && config.DnsServers.Count == 0;
        return config;
    }

    /// <summary>Konfiguration "DHCP für IP und DNS".</summary>
    public static AdapterConfiguration DhcpConfiguration() => new() { DhcpEnabled = true, DnsFromDhcp = true };

    /// <summary>
    /// Erstellt einen Profilvorschlag aus der aktuellen Konfiguration eines Adapters. Bei DHCP werden die
    /// aktuell zugewiesenen Werte trotzdem eingetragen, damit sie bei Bedarf als statisches Profil übernommen werden können.
    /// </summary>
    public static IpProfile FromAdapter(AdapterInfo adapter)
    {
        var persistent = adapter.PersistentConfiguration;
        var useDhcp = persistent?.DhcpEnabled ?? adapter.DhcpEnabled;
        var profile = new IpProfile
        {
            Name = adapter.Name,
            UseDhcp = useDhcp,
            Adapter = AdapterBinding.FromAdapter(adapter),
        };

        if (persistent is { DhcpEnabled: false, Addresses.Count: > 0 })
        {
            profile.IpAddress = persistent.Addresses[0].Address;
            profile.SubnetMask = persistent.Addresses[0].SubnetMask;
            profile.Gateway = persistent.Gateways.FirstOrDefault();
        }
        else if (adapter.Addresses.Count > 0)
        {
            profile.IpAddress = adapter.Addresses[0].Address;
            profile.SubnetMask = adapter.Addresses[0].SubnetMask;
            profile.Gateway = adapter.Gateways.FirstOrDefault();
        }

        IReadOnlyList<string> dns = persistent is { DnsFromDhcp: false } ? persistent.DnsServers : useDhcp ? [] : adapter.DnsServers;
        profile.PreferredDns = dns.ElementAtOrDefault(0);
        profile.AlternateDns = dns.ElementAtOrDefault(1);
        return profile;
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeIp(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : IpValidator.NormalizeIPv4(value);
}
