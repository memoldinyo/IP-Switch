using System.Text;
using IPSwitch.Core.Localization;
using IPSwitch.Core.Models;
using IPSwitch.Core.Network;
using IPSwitch.Core.Validation;

namespace IPSwitch.App.UI;

/// <summary>Textdarstellung von Adaptern und Profilen für Listen, Menüs und Details.</summary>
internal static class AdapterText
{
    public static string Details(AdapterInfo a)
    {
        var rows = new List<(string Label, string? Value)>();
        var sections = new List<(string? Heading, List<(string Label, string? Value)> Rows)> { (null, rows) };
        void Line(string labelKey, string? value) => sections[^1].Rows.Add((Loc.T(labelKey), value));
        void Section(string headingKey) => sections.Add((Loc.T(headingKey), []));

        Line("details.name", a.Name);
        Line("details.description", a.Description);
        Line("details.status", string.Join(", ", new[] { a.StatusText, a.TypeText, a.SpeedText }.Where(s => s.Length > 0)));
        Line("details.mac", a.MacAddress);
        Line("details.guid", a.Id);
        Line("details.index", a.IPv4Index?.ToString());

        Section("details.active");
        Line("details.dhcp", a.DhcpEnabled
            ? Loc.T("common.yes") + (a.DhcpServers.Count > 0 ? $" ({Loc.T("details.dhcpServer", string.Join(", ", a.DhcpServers))})" : string.Empty)
            : Loc.T("common.no"));
        if (a.Addresses.Count == 0)
        {
            Line("details.ipv4", Loc.T(a.IsConnected ? "common.none" : "details.noneDisconnected"));
        }

        foreach (var address in a.Addresses)
        {
            Line("details.ipv4", $"{address.Address}/{address.PrefixLength} ({address.SubnetMask})");
        }

        Line("details.gateway", string.Join(", ", a.Gateways));
        Line("details.dns", string.Join(", ", a.DnsServers));
        foreach (var v6 in a.IPv6Addresses)
        {
            Line("details.ipv6", v6);
        }

        Section("details.stored");
        if (a.PersistentConfiguration is { } p)
        {
            Line("details.mode", p.DhcpEnabled ? Loc.T("mode.dhcp") : Loc.T("mode.static"));
            foreach (var address in p.Addresses)
            {
                Line("details.ipv4", $"{address.Address}/{IpValidator.MaskToPrefix(address.SubnetMask)} ({address.SubnetMask})");
            }

            if (!p.DhcpEnabled)
            {
                Line("details.gateway", string.Join(", ", p.Gateways));
            }

            Line("details.dns", p.DnsFromDhcp ? Loc.T("common.viaDhcp") : string.Join(", ", p.DnsServers));
            foreach (var residual in p.ResidualAddresses)
            {
                Line("details.residual", Loc.T("details.residualValue", $"{residual.Address}/{IpValidator.MaskToPrefix(residual.SubnetMask)}"));
            }
        }
        else
        {
            Line("details.stored", Loc.T("details.notReadable"));
        }

        // Spaltenbreite an die längste Beschriftung der jeweiligen Sprache anpassen.
        var width = sections.SelectMany(s => s.Rows).Max(r => r.Label.Length) + 4;
        var sb = new StringBuilder();
        foreach (var (heading, sectionRows) in sections)
        {
            var indent = heading is null ? string.Empty : "  ";
            if (heading is not null)
            {
                sb.AppendLine().AppendLine(heading);
            }

            foreach (var (label, value) in sectionRows)
            {
                sb.Append(indent).Append((label + ":").PadRight(width - indent.Length))
                  .AppendLine(string.IsNullOrWhiteSpace(value) ? "–" : value);
            }
        }

        return sb.ToString().TrimEnd();
    }

    public static string MenuLabel(AdapterInfo a) => $"{a.Name}   {a.PrimaryAddressText} ({a.ModeText})";

    public static string ProfileMode(IpProfile p) => p.UseDhcp ? Loc.T("mode.dhcp") : Loc.T("mode.static");

    public static string ProfileAddress(IpProfile p)
    {
        if (p.UseDhcp)
        {
            return Loc.T("common.automatic");
        }

        return IpValidator.TryParseSubnet(p.SubnetMask, out var prefix, out _) ? $"{p.IpAddress}/{prefix}" : p.IpAddress ?? "–";
    }

    public static string ProfileDns(IpProfile p)
    {
        var servers = new[] { p.PreferredDns, p.AlternateDns }.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        return servers.Count > 0 ? string.Join(", ", servers) : p.UseDhcp ? Loc.T("common.viaDhcp") : "–";
    }

    /// <summary>Anzeigename des zugeordneten Adapters (aktueller Name, falls umbenannt).</summary>
    public static string ProfileAdapter(IpProfile p, IReadOnlyList<AdapterInfo> adapters)
    {
        if (p.Adapter is null)
        {
            return "–";
        }

        var match = AdapterService.Match(adapters, p.Adapter);
        return match?.Name ?? Loc.T("adapter.missing", p.Adapter.LastKnownName ?? p.Adapter.InterfaceId);
    }

    public static string ProfileSummary(IpProfile p)
    {
        var parts = new List<string> { ProfileMode(p) };
        if (!p.UseDhcp)
        {
            parts.Add(ProfileAddress(p));
            if (!string.IsNullOrWhiteSpace(p.Gateway))
            {
                parts.Add("GW " + p.Gateway);
            }
        }

        parts.Add("DNS " + ProfileDns(p));
        return string.Join(" · ", parts);
    }
}
