using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IPSwitch.Core.Storage;

/// <summary>Gemeinsame JSON-Einstellungen und atomares Schreiben von Dateien.</summary>
public static class JsonFile
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Kompakte Variante (z. B. für die Übergabe an den Hilfsprozess).</summary>
    public static JsonSerializerOptions CompactOptions { get; } = new(Options) { WriteIndented = false };

    public static T? Read<T>(string path) =>
        JsonSerializer.Deserialize<T>(File.ReadAllText(path, Encoding.UTF8), Options);

    /// <summary>Schreibt zuerst in eine temporäre Datei und ersetzt dann das Ziel, damit keine halbe Datei entsteht.</summary>
    public static void WriteAtomic<T>(string path, T value)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory);
        var temp = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(value, Options), new UTF8Encoding(false));
            MoveWithRetry(temp, path);
        }
        finally
        {
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }
        }
    }

    /// <summary>
    /// Virenscanner oder Indexdienste halten frisch geschriebene Dateien oft kurz geöffnet,
    /// sodass das Ersetzen mit "Zugriff verweigert" scheitert. Daher einige kurze Wiederholungen.
    /// </summary>
    private static void MoveWithRetry(string source, string destination)
    {
        const int attempts = 20;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                File.Move(source, destination, overwrite: true);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException && attempt < attempts)
            {
                Thread.Sleep(25 * attempt);
            }
        }
    }
}
