using IPSwitch.Core.Localization;
using IPSwitch.App.Elevation;
using IPSwitch.App.Infrastructure;
using IPSwitch.Core.Diagnostics;
using IPSwitch.Core.Elevation;
using IPSwitch.Core.Storage;

namespace IPSwitch.App;

internal static class Program
{
    public const string AutostartSwitch = "--autostart";

    [STAThread]
    private static int Main(string[] args)
    {
        // Erhöhter Hilfsprozess: führt einen Auftrag aus und beendet sich wieder (keine Tray-Oberfläche).
        if (args.Length > 0 && args[0] is ElevatedProtocol.ApplySwitch or ElevatedProtocol.AdminAutostartSwitch)
        {
            ApplicationConfiguration.Initialize();
            return ElevatedHelper.Run(args);
        }

        var paths = AppPaths.Default;
        try
        {
            paths.EnsureCreated();
        }
        catch (Exception ex)
        {
            MessageBox.Show(Loc.T("app.dataDirFailed", paths.DataDirectory, ex.Message),
                "IP-Switch", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }

        Log.Initialize(paths.LogDirectory);

        // Beim automatischen Start kurz warten: So kann eine Instanz, die sich gerade für einen Neustart mit
        // Administratorrechten beendet, ihren Platz freigeben. Bei doppeltem Autostart (Run-Schlüssel und
        // geplante Aufgabe) beendet sich der zweite Start still.
        var isAutostart = args.Contains(AutostartSwitch, StringComparer.OrdinalIgnoreCase);
        using var instance = new SingleInstance("IPSwitch", isAutostart ? TimeSpan.FromSeconds(15) : TimeSpan.Zero);
        if (!instance.IsFirstInstance)
        {
            if (!isAutostart)
            {
                // Manueller Start, obwohl bereits eine Instanz läuft: diese zeigt das Verwaltungsfenster.
                instance.SignalFirstInstance();
            }

            return 0;
        }

        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) =>
        {
            Log.Error("Unbehandelter Fehler", e.Exception);
            MessageBox.Show(Loc.T("app.unexpectedError", e.Exception.Message),
                "IP-Switch", MessageBoxButtons.OK, MessageBoxIcon.Error);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Error("Schwerwiegender Fehler", e.ExceptionObject as Exception);

        Log.Info($"IP-Switch {Application.ProductVersion} gestartet.");
        Log.Info($"IP-Switch läuft {(ElevationInfo.IsElevated ? "mit Administratorrechten" : "mit Standardrechten")}.");
        using var context = new TrayApplicationContext(paths, instance, showWindow: !isAutostart);
        Application.Run(context);
        Log.Info("IP-Switch beendet.");
        return 0;
    }
}
