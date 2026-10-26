using System.Globalization;
using IPSwitch.Core.Localization;

namespace IPSwitch.Core.Validation;

/// <summary>
/// Strikte IPv4-Hilfsfunktionen. Bewusst ohne <see cref="System.Net.IPAddress.TryParse(string, out System.Net.IPAddress)"/>,
/// da dieses auch Kurzformen wie "10.1" oder Hex-Schreibweisen akzeptiert, die für Benutzer missverständlich sind.
/// </summary>
public static class IpValidator
{
    /// <summary>Parst eine IPv4-Adresse in Punktnotation (vier Oktette, 0–255, ohne führende Nullen).</summary>
    public static bool TryParseIPv4(string? text, out uint value, out string? error)
    {
        value = 0;
        error = null;
        var s = text?.Trim() ?? string.Empty;
        if (s.Length == 0)
        {
            error = Loc.T("ip.empty");
            return false;
        }

        var parts = s.Split('.');
        if (parts.Length != 4)
        {
            error = Loc.T("ip.format", s);
            return false;
        }

        for (var i = 0; i < 4; i++)
        {
            var part = parts[i];
            if (part.Length is 0 or > 3 || !part.All(char.IsAsciiDigit))
            {
                error = Loc.T("ip.octet", s);
                return false;
            }

            if (part.Length > 1 && part[0] == '0')
            {
                error = Loc.T("ip.leadingZero", s, part);
                return false;
            }

            var octet = int.Parse(part, NumberStyles.None, CultureInfo.InvariantCulture);
            if (octet > 255)
            {
                error = Loc.T("ip.range", s, octet);
                return false;
            }

            value = (value << 8) | (uint)octet;
        }

        return true;
    }

    public static bool IsValidIPv4(string? text) => TryParseIPv4(text, out _, out _);

    public static string FormatIPv4(uint value) =>
        $"{(value >> 24) & 0xFF}.{(value >> 16) & 0xFF}.{(value >> 8) & 0xFF}.{value & 0xFF}";

    /// <summary>Normalisiert eine gültige IPv4-Adresse (entfernt Leerzeichen), sonst unverändert.</summary>
    public static string NormalizeIPv4(string text) =>
        TryParseIPv4(text, out var v, out _) ? FormatIPv4(v) : text.Trim();

    public static uint PrefixToMaskValue(int prefix) =>
        prefix <= 0 ? 0u : prefix >= 32 ? 0xFFFFFFFFu : 0xFFFFFFFFu << (32 - prefix);

    public static string PrefixToMask(int prefix) => FormatIPv4(PrefixToMaskValue(prefix));

    /// <summary>Liefert die Präfixlänge einer zusammenhängenden Maske oder -1.</summary>
    public static int MaskToPrefix(uint mask)
    {
        var inverted = ~mask;
        // Zusammenhängend genau dann, wenn ~mask + 1 eine Zweierpotenz (oder 0 bei /0) ist.
        if ((inverted & (inverted + 1)) != 0)
        {
            return -1;
        }

        return 32 - System.Numerics.BitOperations.PopCount(inverted);
    }

    public static int MaskToPrefix(string? mask) =>
        TryParseSubnet(mask, out var prefix, out _) ? prefix : -1;

    /// <summary>
    /// Akzeptiert eine Subnetzmaske ("255.255.255.0") oder eine Präfixlänge ("24" bzw. "/24").
    /// </summary>
    public static bool TryParseSubnet(string? text, out int prefix, out string? error)
    {
        prefix = -1;
        error = null;
        var s = text?.Trim() ?? string.Empty;
        if (s.Length == 0)
        {
            error = Loc.T("subnet.empty");
            return false;
        }

        var prefixText = s.StartsWith('/') ? s[1..].Trim() : s;
        if (prefixText.Length is > 0 and <= 2 && prefixText.All(char.IsAsciiDigit))
        {
            var p = int.Parse(prefixText, NumberStyles.None, CultureInfo.InvariantCulture);
            if (p is < 1 or > 32)
            {
                error = Loc.T("subnet.prefixRange", p);
                return false;
            }

            prefix = p;
            return true;
        }

        if (!TryParseIPv4(s, out var mask, out _))
        {
            error = Loc.T("subnet.invalid", s);
            return false;
        }

        var maskPrefix = MaskToPrefix(mask);
        if (maskPrefix < 0)
        {
            error = Loc.T("subnet.notContiguous", s);
            return false;
        }

        if (maskPrefix == 0)
        {
            error = Loc.T("subnet.zero");
            return false;
        }

        prefix = maskPrefix;
        return true;
    }

    /// <summary>Prüft, ob die Adresse als Host-Adresse in einem Subnetz mit der angegebenen Präfixlänge nutzbar ist.</summary>
    /// <param name="role">Bezeichnung für die Meldung (übersetzt), Standard: "Die IP-Adresse".</param>
    public static string? ValidateHostAddress(uint ip, int prefix, string? role = null)
    {
        role ??= Loc.T("role.ip");
        var unicastError = ValidateUnicast(ip, role, allowLoopback: false);
        if (unicastError is not null)
        {
            return unicastError;
        }

        if (prefix is >= 1 and <= 30)
        {
            var mask = PrefixToMaskValue(prefix);
            var network = ip & mask;
            var broadcast = network | ~mask;
            if (ip == network)
            {
                return Loc.T("host.network", role, FormatIPv4(ip), FormatIPv4(network), prefix);
            }

            if (ip == broadcast)
            {
                return Loc.T("host.broadcast", role, FormatIPv4(ip), FormatIPv4(network), prefix);
            }
        }

        return null;
    }

    public static string? ValidateGateway(uint gateway, uint ip, int prefix)
    {
        var role = Loc.T("role.gateway");
        var error = ValidateUnicast(gateway, role, allowLoopback: false);
        if (error is not null)
        {
            return error;
        }

        if (gateway == ip)
        {
            return Loc.T("gw.self");
        }

        var mask = PrefixToMaskValue(prefix);
        if ((gateway & mask) != (ip & mask))
        {
            return Loc.T("gw.subnet", FormatIPv4(gateway), FormatIPv4(ip & mask), prefix);
        }

        return ValidateHostAddress(gateway, prefix, role);
    }

    /// <param name="role">Bezeichnung für die Meldung (übersetzt), Standard: "Der DNS-Server".</param>
    public static string? ValidateDnsServer(uint dns, string? role = null) =>
        ValidateUnicast(dns, role ?? Loc.T("role.dns"), allowLoopback: true);

    private static string? ValidateUnicast(uint ip, string role, bool allowLoopback)
    {
        var first = ip >> 24;
        var text = FormatIPv4(ip);
        if (first == 0)
        {
            return Loc.T("unicast.zero", role, text);
        }

        if (first == 127 && !allowLoopback)
        {
            return Loc.T("unicast.loopback", role, text);
        }

        if (first is >= 224 and <= 239)
        {
            return Loc.T("unicast.multicast", role, text);
        }

        if (first >= 240)
        {
            return Loc.T("unicast.reserved", role, text);
        }

        return null;
    }
}
