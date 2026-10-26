using IPSwitch.Core.Diagnostics;

namespace IPSwitch.Core.Storage;

/// <summary>Farbschema der Fenster.</summary>
public enum ThemeMode
{
    /// <summary>Wie in Windows eingestellt (App-Modus hell/dunkel).</summary>
    System,
    Light,
    Dark,
}

/// <summary>Benutzereinstellungen (settings.json).</summary>
public sealed class AppSettings
{
    /// <summary>Farbschema der Fenster.</summary>
    public ThemeMode Theme { get; set; } = ThemeMode.System;

    /// <summary>Oberflächensprache (z. B. "de", "en"); null = wie Windows.</summary>
    public string? Language { get; set; }

    /// <summary>Auch virtuelle/sekundäre Adapter (Wi-Fi Direct, WAN Miniport usw.) anzeigen.</summary>
    public bool ShowAllAdapters { get; set; }

    /// <summary>Erfolgsmeldungen als Benachrichtigung anzeigen.</summary>
    public bool ShowNotifications { get; set; } = true;

    /// <summary>Start bei der Anmeldung über eine geplante Aufgabe mit Administratorrechten ist eingerichtet.</summary>
    public bool AdminAutostart { get; set; }

    public static AppSettings Load(string path)
    {
        try
        {
            return File.Exists(path) ? JsonFile.Read<AppSettings>(path) ?? new AppSettings() : new AppSettings();
        }
        catch (Exception ex)
        {
            Log.Warn($"Einstellungen konnten nicht gelesen werden, Standardwerte werden verwendet: {ex.Message}");
            return new AppSettings();
        }
    }

    public void Save(string path)
    {
        try
        {
            JsonFile.WriteAtomic(path, this);
        }
        catch (Exception ex)
        {
            Log.Error("Einstellungen konnten nicht gespeichert werden", ex);
        }
    }
}
