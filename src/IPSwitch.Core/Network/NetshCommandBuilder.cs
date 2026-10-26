using IPSwitch.Core.Localization;
using System.Globalization;
using IPSwitch.Core.Models;

namespace IPSwitch.Core.Network;

/// <param name="Arguments">Einzelargumente für netsh.exe (werden ohne Shell übergeben).</param>
/// <param name="Description">Verständliche Beschreibung des Schritts.</param>
/// <param name="AllowFailure">true = ein Fehler bricht den Vorgang nicht ab (das Ergebnis wird anschließend geprüft).</param>
public sealed record NetshCommand(IReadOnlyList<string> Arguments, string Description, bool AllowFailure = false)
{
    public string CommandLine => "netsh " + string.Join(' ', Arguments);
}

/// <summary>
/// Erzeugt die netsh-Befehle, um eine Zielkonfiguration herzustellen. Der Adapter wird über
/// seinen Interface-Index angesprochen, damit Sonderzeichen im Namen und Umbenennungen keine Rolle spielen.
/// </summary>
public static class NetshCommandBuilder
{
    public static IReadOnlyList<NetshCommand> Build(int interfaceIndex, AdapterConfiguration target, AdapterConfiguration? current)
    {
        if (!target.DhcpEnabled && target.Addresses.Count == 0)
        {
            throw new ArgumentException("Eine statische Konfiguration benötigt mindestens eine Adresse.", nameof(target));
        }

        var name = "name=" + interfaceIndex.ToString(CultureInfo.InvariantCulture);
        var commands = new List<NetshCommand>();

        if (target.DhcpEnabled)
        {
            if (current is not { DhcpEnabled: true })
            {
                if (current is null || current.Gateways.Count > 0)
                {
                    // Ohne diesen Schritt bleiben statische Standardgateways nach dem Umschalten auf DHCP erhalten.
                    commands.Add(new(["interface", "ipv4", "delete", "address", name, "gateway=all"],
                        Loc.T("netsh.removeGateways"), AllowFailure: true));
                }

                commands.Add(new(["interface", "ipv4", "set", "address", name, "source=dhcp"],
                    Loc.T("netsh.dhcpAddress"), AllowFailure: current is null));
            }

            // "set address source=dhcp" lässt bisherige statische Adressen aktiv und gespeichert – sie würden die
            // Adresse für andere Adapter blockieren ("Das Objekt ist bereits vorhanden"). Daher ausdrücklich löschen.
            var leftovers = (current is { DhcpEnabled: false } ? current.Addresses : [])
                .Concat(current?.ResidualAddresses ?? [])
                .Select(a => a.Address)
                .Distinct();
            foreach (var address in leftovers)
            {
                commands.Add(new(["interface", "ipv4", "delete", "address", name, $"address={address}"],
                    Loc.T("netsh.removeAddress", address), AllowFailure: true));
            }
        }
        else
        {
            var first = target.Addresses[0];
            var gateway = target.Gateways.Count > 0 ? target.Gateways[0] : "none";
            commands.Add(new(
                ["interface", "ipv4", "set", "address", name, "source=static", $"address={first.Address}", $"mask={first.SubnetMask}", $"gateway={gateway}"],
                (gateway == "none" ? Loc.T("netsh.setStatic", first.Address, first.SubnetMask) : Loc.T("netsh.setStaticGateway", first.Address, first.SubnetMask, gateway))));

            foreach (var extra in target.Addresses.Skip(1))
            {
                commands.Add(new(["interface", "ipv4", "add", "address", name, $"address={extra.Address}", $"mask={extra.SubnetMask}"],
                    Loc.T("netsh.addAddress", extra.Address)));
            }

            foreach (var extraGateway in target.Gateways.Skip(1))
            {
                commands.Add(new(["interface", "ipv4", "add", "address", name, $"gateway={extraGateway}", "gwmetric=0"],
                    Loc.T("netsh.addGateway", extraGateway), AllowFailure: true));
            }
        }

        if (target.DnsFromDhcp)
        {
            if (current is not { DnsFromDhcp: true })
            {
                commands.Add(new(["interface", "ipv4", "set", "dnsservers", name, "source=dhcp"],
                    Loc.T("netsh.dhcpDns"), AllowFailure: current is null));
            }
        }
        else if (target.DnsServers.Count == 0)
        {
            commands.Add(new(["interface", "ipv4", "set", "dnsservers", name, "source=static", "address=none", "validate=no"],
                Loc.T("netsh.removeDns"), AllowFailure: true));
        }
        else
        {
            commands.Add(new(["interface", "ipv4", "set", "dnsservers", name, "source=static", $"address={target.DnsServers[0]}", "validate=no"],
                Loc.T("netsh.setDns", target.DnsServers[0])));
            for (var i = 1; i < target.DnsServers.Count; i++)
            {
                commands.Add(new(
                    ["interface", "ipv4", "add", "dnsservers", name, $"address={target.DnsServers[i]}", $"index={(i + 1).ToString(CultureInfo.InvariantCulture)}", "validate=no"],
                    Loc.T("netsh.addDns", target.DnsServers[i])));
            }
        }

        return commands;
    }
}
