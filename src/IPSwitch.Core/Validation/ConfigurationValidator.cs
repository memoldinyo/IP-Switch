using IPSwitch.Core.Localization;
using IPSwitch.Core.Models;

namespace IPSwitch.Core.Validation;

/// <summary>
/// Prüft eine <see cref="AdapterConfiguration"/>, bevor sie angewendet wird. Wird auch im erhöhten
/// Hilfsprozess erneut ausgeführt, damit dort nur geprüfte Werte an netsh übergeben werden.
/// </summary>
public static class ConfigurationValidator
{
    public const int MaxAddresses = 16;
    public const int MaxGateways = 8;
    public const int MaxDnsServers = 8;

    public static IReadOnlyList<string> Validate(AdapterConfiguration config)
    {
        var errors = new List<string>();

        if (!config.DhcpEnabled)
        {
            if (config.Addresses.Count == 0)
            {
                errors.Add(Loc.T("config.noAddress"));
            }

            if (config.Addresses.Count > MaxAddresses)
            {
                errors.Add(Loc.T("config.tooManyAddresses", MaxAddresses));
            }

            foreach (var address in config.Addresses)
            {
                if (!IpValidator.TryParseIPv4(address.Address, out var ip, out var ipError))
                {
                    errors.Add(ipError!);
                    continue;
                }

                if (!IpValidator.TryParseSubnet(address.SubnetMask, out var prefix, out var maskError))
                {
                    errors.Add(maskError!);
                    continue;
                }

                if (IpValidator.ValidateHostAddress(ip, prefix) is { } hostError)
                {
                    errors.Add(hostError);
                }
            }

            if (config.Gateways.Count > MaxGateways)
            {
                errors.Add(Loc.T("config.tooManyGateways", MaxGateways));
            }

            foreach (var gateway in config.Gateways)
            {
                if (!IpValidator.TryParseIPv4(gateway, out _, out var gwError))
                {
                    errors.Add(Loc.T("fmt.labelled", Loc.T("label.gateway"), gwError));
                }
            }

            if (config.DnsFromDhcp)
            {
                errors.Add(Loc.T("config.staticWithDhcpDns"));
            }
        }

        if (config.DnsFromDhcp && config.DnsServers.Count > 0)
        {
            errors.Add(Loc.T("config.dnsConflict"));
        }

        if (config.DnsServers.Count > MaxDnsServers)
        {
            errors.Add(Loc.T("config.tooManyDns", MaxDnsServers));
        }

        foreach (var dns in config.DnsServers)
        {
            if (!IpValidator.TryParseIPv4(dns, out var value, out var dnsError))
            {
                errors.Add(Loc.T("fmt.labelled", Loc.T("label.dns"), dnsError));
            }
            else if (IpValidator.ValidateDnsServer(value) is { } error)
            {
                errors.Add(error);
            }
        }

        return errors;
    }
}
