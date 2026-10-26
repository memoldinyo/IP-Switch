namespace IPSwitch.Core.Storage;

/// <summary>
/// Lokale Ablageorte. Bewusst %LOCALAPPDATA% statt %APPDATA%, damit Profile in Domänenumgebungen
/// nicht über servergespeicherte (Roaming-)Profile übertragen werden.
/// </summary>
public sealed class AppPaths
{
    public AppPaths(string dataDirectory) => DataDirectory = dataDirectory;

    public static AppPaths Default { get; } = new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IPSwitch"));

    public string DataDirectory { get; }

    public string ProfilesFile => Path.Combine(DataDirectory, "profiles.json");

    public string SettingsFile => Path.Combine(DataDirectory, "settings.json");

    public string BackupDirectory => Path.Combine(DataDirectory, "backups");

    public string LogDirectory => Path.Combine(DataDirectory, "logs");

    /// <summary>Austauschverzeichnis für Ergebnisse des erhöhten Hilfsprozesses.</summary>
    public string RuntimeDirectory => Path.Combine(DataDirectory, "runtime");

    public void EnsureCreated()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(BackupDirectory);
        Directory.CreateDirectory(LogDirectory);
        Directory.CreateDirectory(RuntimeDirectory);
    }
}
