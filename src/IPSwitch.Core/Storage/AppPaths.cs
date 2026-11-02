namespace IPSwitch.Core.Storage;

/// <summary>
/// Lokale Ablageorte. Bewusst %LOCALAPPDATA% statt %APPDATA%, damit Profile in Domänenumgebungen
/// nicht über servergespeicherte (Roaming-)Profile übertragen werden.
/// </summary>
public sealed class AppPaths
{
    public AppPaths(string dataDirectory) => DataDirectory = dataDirectory;

    /// <summary>Datenordner der Desktop-Installation (MSI, ZIP).</summary>
    public static string DesktopDataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IPSwitch");

    /// <summary>
    /// Im Store-Paket der LocalState-Ordner des Pakets: Schreibzugriffe auf %LOCALAPPDATA% würden dort sonst
    /// unsichtbar umgeleitet, und „Datenordner öffnen“ zeigte einen leeren Ordner.
    /// </summary>
    public static AppPaths Default { get; } = new(AppPackage.FamilyName is { } family
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages", family, "LocalState")
        : DesktopDataDirectory);

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

    /// <summary>
    /// Übernimmt beim ersten Start der Store-Version Profile und Einstellungen einer vorhandenen Desktop-Installation.
    /// </summary>
    /// <returns>true, wenn Dateien übernommen wurden.</returns>
    public bool CopyFromIfEmpty(string sourceDirectory)
    {
        if (File.Exists(ProfilesFile) || string.Equals(Path.GetFullPath(sourceDirectory), Path.GetFullPath(DataDirectory), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var copied = false;
        foreach (var file in new[] { ProfilesFile, SettingsFile })
        {
            var source = Path.Combine(sourceDirectory, Path.GetFileName(file));
            if (File.Exists(source) && !File.Exists(file))
            {
                File.Copy(source, file);
                copied = true;
            }
        }

        return copied;
    }
}
