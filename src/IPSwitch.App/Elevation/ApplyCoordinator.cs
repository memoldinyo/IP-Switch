using IPSwitch.Core.Localization;
using System.ComponentModel;
using System.Diagnostics;
using IPSwitch.Core.Diagnostics;
using IPSwitch.Core.Elevation;
using IPSwitch.Core.Models;
using IPSwitch.Core.Network;
using IPSwitch.Core.Storage;

namespace IPSwitch.App.Elevation;

/// <summary>
/// Führt Änderungen aus, die Administratorrechte benötigen. Läuft die Anwendung bereits mit Administratorrechten,
/// geschieht das direkt im Prozess (ohne UAC-Abfrage). Andernfalls wird der Hilfsprozess mit UAC-Abfrage ("runas")
/// gestartet – Administratorrechte werden dann nur für den Moment der Änderung angefordert.
/// </summary>
internal sealed class ApplyCoordinator
{
    private const int ErrorCancelled = 1223;
    private readonly AppPaths _paths;

    public ApplyCoordinator(AppPaths paths) => _paths = paths;

    public bool IsBusy { get; private set; }

    /// <returns>Ergebnis oder null, wenn der Benutzer die UAC-Abfrage abgelehnt hat.</returns>
    public async Task<ApplyResult?> ApplyAsync(AdapterInfo adapter, AdapterConfiguration target, string label)
    {
        if (IsBusy)
        {
            return new ApplyResult
            {
                Outcome = ApplyOutcome.InvalidRequest,
                Message = Loc.T("apply.busy"),
            };
        }

        var request = new ApplyRequest
        {
            InterfaceId = adapter.Id,
            MacAddress = adapter.MacAddress,
            Target = target,
            Label = label,
            BackupDirectory = _paths.BackupDirectory,
            Language = Loc.Current,
        };

        IsBusy = true;
        try
        {
            if (ElevationInfo.IsElevated)
            {
                Log.Info($"Administratorrechte vorhanden – \"{label}\" wird direkt angewendet.");
                return await Task.Run(() => NetworkConfigurator.CreateDefault().Apply(request)).ConfigureAwait(true);
            }

            return await RunElevatedAsync(resultFile => ElevatedProtocol.BuildArguments(request, resultFile), label).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Log.Error("Anwenden fehlgeschlagen", ex);
            return Failure(Loc.T("apply.failedGeneric", ex.Message));
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Legt die geplante Aufgabe für den Start mit Administratorrechten an bzw. entfernt sie.</summary>
    /// <returns>Ergebnis oder null, wenn der Benutzer die UAC-Abfrage abgelehnt hat.</returns>
    public async Task<ApplyResult?> SetAdminAutostartAsync(bool enable)
    {
        var sid = ElevationInfo.CurrentUserSid;
        try
        {
            if (ElevationInfo.IsElevated)
            {
                var exe = Environment.ProcessPath ?? Application.ExecutablePath;
                return await Task.Run(() => enable ? new AdminAutostartTask().Enable(sid, exe) : new AdminAutostartTask().Disable(sid))
                    .ConfigureAwait(true);
            }

            return await RunElevatedAsync(resultFile => ElevatedProtocol.BuildAdminAutostartArguments(enable, sid, Loc.Current, resultFile),
                Loc.T(enable ? "adminAutostart.enableLabel" : "adminAutostart.disableLabel")).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Log.Error("Autostart mit Administratorrechten konnte nicht geändert werden", ex);
            return Failure(Loc.T("settings.changeFailed", ex.Message));
        }
    }

    /// <summary>Entfernt liegen gebliebene Ergebnisdateien (z. B. nach einem Absturz).</summary>
    public void CleanupRuntimeDirectory()
    {
        if (!Directory.Exists(_paths.RuntimeDirectory))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(_paths.RuntimeDirectory, "result-*.json"))
        {
            if (File.GetLastWriteTimeUtc(file) < DateTime.UtcNow.AddHours(-1))
            {
                TryDelete(file);
            }
        }
    }

    /// <summary>Startet den Hilfsprozess per UAC und liest dessen Ergebnisdatei.</summary>
    private async Task<ApplyResult?> RunElevatedAsync(Func<string, string> buildArguments, string label)
    {
        Directory.CreateDirectory(_paths.RuntimeDirectory);
        var resultFile = Path.Combine(_paths.RuntimeDirectory, $"result-{Guid.NewGuid():N}.json");
        var startInfo = new ProcessStartInfo(Environment.ProcessPath ?? Application.ExecutablePath)
        {
            UseShellExecute = true,
            Verb = "runas",
            Arguments = buildArguments(resultFile),
            WorkingDirectory = AppContext.BaseDirectory,
        };

        Process? process;
        try
        {
            process = Process.Start(startInfo);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
        {
            Log.Info($"UAC-Abfrage für \"{label}\" wurde abgelehnt.");
            return null;
        }

        if (process is null)
        {
            return Failure(Loc.T("helper.startFailed"));
        }

        int exitCode;
        using (process)
        {
            await process.WaitForExitAsync().ConfigureAwait(true);
            exitCode = process.ExitCode;
        }

        if (!File.Exists(resultFile))
        {
            return Failure(Loc.T("helper.noResult", exitCode));
        }

        try
        {
            return JsonFile.Read<ApplyResult>(resultFile) ?? Failure(Loc.T("helper.emptyResult"));
        }
        finally
        {
            TryDelete(resultFile);
        }
    }

    private static ApplyResult Failure(string message) =>
        new() { Outcome = ApplyOutcome.FailedNoChanges, Message = message };

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex)
        {
            Log.Warn($"{path} konnte nicht gelöscht werden: {ex.Message}");
        }
    }
}
