using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;

namespace IPSwitch.App.Infrastructure;

/// <summary>
/// Gibt nach dem Schließen des Verwaltungsfensters Speicher frei: IP-Switch läuft meist stundenlang nur im
/// Infobereich, das Fenster mit seinen Steuerelementen und Schriften wird dann nicht mehr gebraucht.
/// </summary>
internal static class MemoryTrim
{
    private static System.Windows.Forms.Timer? _pending;

    /// <summary>Kurz verzögert, damit Aufräumarbeiten des Fensters abgeschlossen sind und ein schnelles Wiederöffnen nicht bremst.</summary>
    public static void Schedule()
    {
        _pending?.Dispose();
        _pending = new System.Windows.Forms.Timer { Interval = 3000 };
        _pending.Tick += (_, _) =>
        {
            _pending?.Dispose();
            _pending = null;
            Run();
        };
        _pending.Start();
    }

    private static void Run()
    {
        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // Nicht benötigte Seiten an Windows zurückgeben; sie werden bei Bedarf wieder eingelesen.
        using var self = Process.GetCurrentProcess();
        SetProcessWorkingSetSize(self.Handle, -1, -1);
    }

    [DllImport("kernel32.dll")]
    private static extern bool SetProcessWorkingSetSize(IntPtr process, nint minimumSize, nint maximumSize);
}
