using IPSwitch.Core.Models;
using IPSwitch.Core.Validation;
using Microsoft.Win32;

namespace IPSwitch.Core.Network;

/// <summary>
/// Liest die persistente IPv4-Konfiguration aus
/// HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\{GUID}.
/// Dieser Schlüssel ist für Standardbenutzer lesbar; geschrieben wird hier nichts.
/// </summary>
public sealed class TcpipRegistryReader : IConfigurationReader
{
    private const string InterfacesKey = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces";

    public AdapterConfiguration? ReadConfiguration(string interfaceId)
    {
        if (!Guid.TryParse(interfaceId, out var guid))
        {
            return null;
        }

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey($@"{InterfacesKey}\{guid:B}", writable: false);
            if (key?.GetValue("EnableDHCP") is not int enableDhcp)
            {
                return null;
            }

            return Parse(
                enableDhcp != 0,
                ReadMulti(key, "IPAddress"),
                ReadMulti(key, "SubnetMask"),
                ReadMulti(key, "DefaultGateway"),
                key.GetValue("NameServer") as string);
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            Diagnostics.Log.Warn($"TCP/IP-Konfiguration für {interfaceId} nicht lesbar: {ex.Message}");
            return null;
        }
    }

    /// <summary>Erzeugt aus den Registry-Rohwerten eine bereinigte Konfiguration.</summary>
    internal static AdapterConfiguration Parse(bool dhcp, string[] ipAddresses, string[] masks, string[] gateways, string? nameServer)
    {
        var config = new AdapterConfiguration { DhcpEnabled = dhcp };

        for (var i = 0; i < ipAddresses.Length; i++)
        {
            var ip = ipAddresses[i].Trim();
            var mask = i < masks.Length ? masks[i].Trim() : string.Empty;
            if (IpValidator.TryParseIPv4(ip, out var ipValue, out _) && ipValue != 0 &&
                IpValidator.TryParseSubnet(mask, out _, out _))
            {
                config.Addresses.Add(new StaticAddress(ip, mask));
            }
        }

        config.Gateways.AddRange(gateways.Select(g => g.Trim())
            .Where(g => IpValidator.TryParseIPv4(g, out var v, out _) && v != 0));

        config.DnsServers.AddRange((nameServer ?? string.Empty)
            .Split([',', ' ', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(IpValidator.IsValidIPv4));

        config.DnsFromDhcp = dhcp && config.DnsServers.Count == 0;

        if (dhcp)
        {
            // Bei DHCP noch gespeicherte statische Adressen sind Überbleibsel: "netsh … source=dhcp" entfernt sie nicht,
            // sie bleiben aktiv und blockieren die Adresse für andere Adapter.
            config.ResidualAddresses.AddRange(config.Addresses);
            config.Addresses.Clear();
            config.Gateways.Clear();
        }

        return config;
    }

    private static string[] ReadMulti(RegistryKey key, string name) => key.GetValue(name) switch
    {
        string[] values => values,
        string single when single.Length > 0 => [single],
        _ => [],
    };
}
