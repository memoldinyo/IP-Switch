using IPSwitch.Core.Localization;
using System.Text;
using System.Text.Json;
using IPSwitch.Core.Models;
using IPSwitch.Core.Storage;

namespace IPSwitch.Core.Elevation;

/// <summary>
/// Kommandozeilenprotokoll zwischen Tray-Anwendung (Standardrechte) und dem per UAC erhöhten Hilfsprozess:
/// <c>IPSwitch.exe --apply &lt;Auftrag als Base64Url-JSON&gt; "&lt;Ergebnisdatei&gt;"</c>.
/// Der Auftrag wird direkt übergeben (keine Datei), das Ergebnis als JSON-Datei zurückgeschrieben.
/// </summary>
public static class ElevatedProtocol
{
    public const string ApplySwitch = "--apply";

    /// <summary><c>IPSwitch.exe --admin-autostart enable|disable &lt;Benutzer-SID&gt; &lt;Sprache&gt; "&lt;Ergebnisdatei&gt;"</c></summary>
    public const string AdminAutostartSwitch = "--admin-autostart";
    private const int MaxEncodedLength = 16 * 1024;

    public static string EncodeRequest(ApplyRequest request) =>
        Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(request, JsonFile.CompactOptions))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static ApplyRequest DecodeRequest(string encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded) || encoded.Length > MaxEncodedLength)
        {
            throw new FormatException(Loc.T("protocol.invalid"));
        }

        var base64 = encoded.Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + ((4 - (base64.Length % 4)) % 4), '=');
        var json = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
        return JsonSerializer.Deserialize<ApplyRequest>(json, JsonFile.Options)
               ?? throw new FormatException(Loc.T("protocol.empty"));
    }

    public static string BuildArguments(ApplyRequest request, string resultFile) =>
        $"{ApplySwitch} {EncodeRequest(request)} \"{resultFile}\"";

    public static string BuildAdminAutostartArguments(bool enable, string userSid, string language, string resultFile) =>
        $"{AdminAutostartSwitch} {(enable ? "enable" : "disable")} {userSid} {language} \"{resultFile}\"";

    /// <summary>Prüft den Ergebnispfad: vollqualifiziert, .json-Endung, keine Anführungszeichen.</summary>
    public static bool IsValidResultPath(string? path) =>
        !string.IsNullOrWhiteSpace(path) &&
        Path.IsPathFullyQualified(path) &&
        path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) &&
        path.IndexOfAny(Path.GetInvalidPathChars()) < 0 &&
        !path.Contains('"');
}
