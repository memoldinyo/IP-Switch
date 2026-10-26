using IPSwitch.Core.Diagnostics;
using IPSwitch.Core.Models;

namespace IPSwitch.Core.Storage;

/// <summary>
/// Legt Sicherungen der Adapterkonfiguration als JSON-Dateien ab (eine Datei pro Sicherung).
/// </summary>
public sealed class BackupStore
{
    public const int MaxBackupsPerAdapter = 20;

    private readonly string _directory;

    public BackupStore(string directory) => _directory = directory;

    public string Directory => _directory;

    public string Save(ConfigurationBackup backup)
    {
        System.IO.Directory.CreateDirectory(_directory);
        var fileName = $"{backup.CreatedAt:yyyyMMdd-HHmmss-fff}_{SanitizeId(backup.InterfaceId)}.json";
        var path = Path.Combine(_directory, fileName);
        JsonFile.WriteAtomic(path, backup);
        backup.FilePath = path;
        Prune(backup.InterfaceId);
        return path;
    }

    /// <summary>Alle Sicherungen (optional nur für einen Adapter), neueste zuerst.</summary>
    public IReadOnlyList<ConfigurationBackup> List(string? interfaceId = null)
    {
        if (!System.IO.Directory.Exists(_directory))
        {
            return [];
        }

        var pattern = interfaceId is null ? "*.json" : $"*_{SanitizeId(interfaceId)}.json";
        var result = new List<ConfigurationBackup>();
        foreach (var file in System.IO.Directory.EnumerateFiles(_directory, pattern))
        {
            var backup = TryLoad(file);
            if (backup is not null)
            {
                result.Add(backup);
            }
        }

        return result.OrderByDescending(b => b.CreatedAt).ToList();
    }

    public ConfigurationBackup? Latest(string interfaceId) => List(interfaceId).FirstOrDefault();

    public static ConfigurationBackup? TryLoad(string path)
    {
        try
        {
            var backup = JsonFile.Read<ConfigurationBackup>(path);
            if (backup is not null)
            {
                backup.FilePath = path;
            }

            return backup;
        }
        catch (Exception ex)
        {
            Log.Warn($"Sicherung {path} konnte nicht gelesen werden: {ex.Message}");
            return null;
        }
    }

    private void Prune(string interfaceId)
    {
        foreach (var old in List(interfaceId).Skip(MaxBackupsPerAdapter))
        {
            try
            {
                File.Delete(old.FilePath!);
            }
            catch (Exception ex)
            {
                Log.Warn($"Alte Sicherung {old.FilePath} konnte nicht entfernt werden: {ex.Message}");
            }
        }
    }

    private static string SanitizeId(string interfaceId) =>
        new(interfaceId.Where(char.IsAsciiLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
}
