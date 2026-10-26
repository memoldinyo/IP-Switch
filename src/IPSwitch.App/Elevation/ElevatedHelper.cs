using IPSwitch.Core.Localization;
using IPSwitch.Core.Diagnostics;
using IPSwitch.Core.Elevation;
using IPSwitch.Core.Models;
using IPSwitch.Core.Network;
using IPSwitch.Core.Storage;

namespace IPSwitch.App.Elevation;

/// <summary>
/// Einstiegspunkt des erhöhten Hilfsprozesses. Liest den Auftrag von der Kommandozeile, prüft ihn, führt ihn aus
/// und schreibt das Ergebnis als JSON-Datei. Unterstützt:
/// <list type="bullet">
/// <item><c>--apply &lt;Auftrag&gt; "&lt;Ergebnis&gt;"</c> – Konfiguration anwenden (inkl. Sicherung und Rollback)</item>
/// <item><c>--admin-autostart enable|disable &lt;SID&gt; &lt;Sprache&gt; "&lt;Ergebnis&gt;"</c> – geplante Aufgabe anlegen/entfernen</item>
/// </list>
/// </summary>
internal static class ElevatedHelper
{
    public static int Run(string[] args)
    {
        var resultFile = args.Length > 0 ? args[^1] : null;
        if (args.Length is not (3 or 5) || !ElevatedProtocol.IsValidResultPath(resultFile))
        {
            MessageBox.Show(Loc.T("helper.invalidCall"), "IP-Switch", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return (int)ApplyOutcome.InvalidRequest;
        }

        // Ergebnisdatei liegt in %LOCALAPPDATA%\IPSwitch\runtime – das Protokoll daneben in ..\logs.
        Log.Initialize(Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(resultFile!)!)!, "logs"));

        ApplyResult result;
        try
        {
            // Meldungen in der Sprache der aufrufenden Anwendung erzeugen.
            var request = args[0] == ElevatedProtocol.ApplySwitch && args.Length == 3 ? ElevatedProtocol.DecodeRequest(args[1]) : null;
            Loc.SetLanguage(request?.Language ?? (args.Length == 5 ? args[3] : null));

            if (!ElevationInfo.IsElevated)
            {
                result = ApplyResult.Invalid(Loc.T("helper.needsAdmin"));
            }
            else if (args[0] == ElevatedProtocol.AdminAutostartSwitch && args.Length == 5)
            {
                result = RunAdminAutostart(args[1], args[2]);
            }
            else if (request is not null)
            {
                result = NetworkConfigurator.CreateDefault().Apply(request);
            }
            else
            {
                result = ApplyResult.Invalid(Loc.T("helper.unknownJob"));
            }
        }
        catch (Exception ex) when (ex is FormatException or System.Text.Json.JsonException)
        {
            result = ApplyResult.Invalid(Loc.T("helper.invalidJob", ex.Message));
        }
        catch (Exception ex)
        {
            Log.Error("Fehler im Hilfsprozess", ex);
            result = new ApplyResult
            {
                Outcome = ApplyOutcome.FailedRollbackFailed,
                Message = Loc.T("helper.unexpected", ex.Message),
            };
        }

        try
        {
            JsonFile.WriteAtomic(resultFile!, result);
        }
        catch (Exception ex)
        {
            Log.Error("Ergebnisdatei konnte nicht geschrieben werden", ex);
        }

        return (int)result.Outcome;
    }

    private static ApplyResult RunAdminAutostart(string mode, string userSid)
    {
        var task = new AdminAutostartTask();
        return mode switch
        {
            "enable" => task.Enable(userSid, Environment.ProcessPath ?? Application.ExecutablePath),
            "disable" => task.Disable(userSid),
            _ => ApplyResult.Invalid(Loc.T("helper.unknownJob")),
        };
    }
}
