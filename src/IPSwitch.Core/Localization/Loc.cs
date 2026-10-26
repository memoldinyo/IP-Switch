using System.Collections.Frozen;
using System.Globalization;
using System.Text.Json;

namespace IPSwitch.Core.Localization;

/// <summary>Eine unterstützte Oberflächensprache.</summary>
/// <param name="Code">Zweibuchstabiger ISO-Code, z. B. "de".</param>
/// <param name="NativeName">Name der Sprache in der Sprache selbst.</param>
public sealed record LanguageInfo(string Code, string NativeName, bool RightToLeft = false);

/// <summary>
/// Übersetzungen der Oberfläche. Die Texte liegen als eingebettete JSON-Dateien (Strings/&lt;code&gt;.json) vor.
/// Fehlt ein Text in der gewählten Sprache, wird Englisch verwendet, danach der Schlüssel selbst.
/// </summary>
public static class Loc
{
    public const string Fallback = "en";

    private static readonly Dictionary<string, FrozenDictionary<string, string>> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Lock Sync = new();
    private static FrozenDictionary<string, string> _current = FrozenDictionary<string, string>.Empty;
    private static FrozenDictionary<string, string> _fallback = FrozenDictionary<string, string>.Empty;

    static Loc() => SetLanguage(null);

    public static IReadOnlyList<LanguageInfo> Languages { get; } =
    [
        new("de", "Deutsch"),
        new("en", "English"),
        new("tr", "Türkçe"),
        new("fr", "Français"),
        new("it", "Italiano"),
        new("ar", "العربية", RightToLeft: true),
        new("zh", "中文（简体）"),
        new("ru", "Русский"),
    ];

    /// <summary>Aktive Sprache (Code).</summary>
    public static string Current { get; private set; } = Fallback;

    public static bool IsRightToLeft => Languages.FirstOrDefault(l => l.Code == Current)?.RightToLeft == true;

    /// <summary>Wird nach einem Sprachwechsel ausgelöst.</summary>
    public static event EventHandler? LanguageChanged;

    /// <summary>Sprache der Windows-Oberfläche, falls unterstützt, sonst Englisch.</summary>
    public static string SystemLanguage
    {
        get
        {
            var code = CultureInfo.InstalledUICulture.TwoLetterISOLanguageName;
            var ui = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            return IsSupported(ui) ? ui : IsSupported(code) ? code : Fallback;
        }
    }

    public static bool IsSupported(string? code) => code is not null && Languages.Any(l => l.Code == code);

    /// <summary>Setzt die Sprache. null oder leer = Sprache von Windows.</summary>
    public static void SetLanguage(string? code)
    {
        var effective = string.IsNullOrWhiteSpace(code) || !IsSupported(code) ? SystemLanguage : code!;
        lock (Sync)
        {
            _fallback = Load(Fallback);
            _current = Load(effective);
            Current = effective;
        }

        LanguageChanged?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>Übersetzter Text zu einem Schlüssel.</summary>
    public static string T(string key) =>
        _current.TryGetValue(key, out var value) ? value :
        _fallback.TryGetValue(key, out var fallback) ? fallback : key;

    /// <summary>Übersetzter Text mit Platzhaltern {0}, {1}, …</summary>
    public static string T(string key, params object?[] args) =>
        string.Format(CultureInfo.CurrentCulture, T(key), args);

    /// <summary>Alle Schlüssel und Texte einer Sprache (für Tests).</summary>
    public static IReadOnlyDictionary<string, string> Table(string code) => Load(code);

    private static FrozenDictionary<string, string> Load(string code)
    {
        lock (Sync)
        {
            if (Cache.TryGetValue(code, out var cached))
            {
                return cached;
            }

            var assembly = typeof(Loc).Assembly;
            using var stream = assembly.GetManifestResourceStream($"IPSwitch.Core.Localization.Strings.{code}.json");
            var table = stream is null
                ? FrozenDictionary<string, string>.Empty
                : (JsonSerializer.Deserialize<Dictionary<string, string>>(stream, new JsonSerializerOptions
                {
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true,
                }) ?? []).ToFrozenDictionary(StringComparer.Ordinal);
            Cache[code] = table;
            return table;
        }
    }
}
