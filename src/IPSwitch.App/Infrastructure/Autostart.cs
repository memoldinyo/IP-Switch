using IPSwitch.Core.Diagnostics;
using Microsoft.Win32;

namespace IPSwitch.App.Infrastructure;

/// <summary>Autostart über HKCU\...\Run (benutzerbezogen, keine Administratorrechte nötig).</summary>
internal static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "IPSwitch";

    private static string Command => $"\"{Environment.ProcessPath ?? Application.ExecutablePath}\" {Program.AutostartSwitch}";

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
            return key?.GetValue(ValueName) is string value && value.Length > 0;
        }
    }

    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
        if (enabled)
        {
            key.SetValue(ValueName, Command, RegistryValueKind.String);
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }

        Log.Info($"Autostart {(enabled ? "aktiviert" : "deaktiviert")}.");
    }

    /// <summary>
    /// Aktualisiert den Pfad, falls die eingetragene EXE nicht mehr existiert (Anwendung wurde verschoben).
    /// Eine an anderer Stelle gestartete (portable) Kopie überschreibt einen gültigen Eintrag nicht.
    /// </summary>
    public static void RefreshPathIfEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key?.GetValue(ValueName) is string value && !File.Exists(ExtractPath(value)))
            {
                key.SetValue(ValueName, Command, RegistryValueKind.String);
            }
        }
        catch (Exception ex)
        {
            Log.Warn("Autostart-Pfad konnte nicht aktualisiert werden: " + ex.Message);
        }
    }

    private static string ExtractPath(string command)
    {
        var trimmed = command.Trim();
        if (trimmed.StartsWith('"'))
        {
            var end = trimmed.IndexOf('"', 1);
            return end > 1 ? trimmed[1..end] : trimmed.Trim('"');
        }

        var space = trimmed.IndexOf(' ');
        return space > 0 ? trimmed[..space] : trimmed;
    }
}
