using System.Text;

namespace IPSwitch.Core.Diagnostics;

/// <summary>
/// Minimales lokales Dateiprotokoll mit Größenbegrenzung. Es werden keine Daten übertragen.
/// </summary>
public static class Log
{
    private const long MaxSize = 1024 * 1024;
    private static readonly Lock Sync = new();
    private static string? _file;

    public static void Initialize(string directory, string fileName = "ip-switch.log")
    {
        try
        {
            Directory.CreateDirectory(directory);
            _file = Path.Combine(directory, fileName);
        }
        catch
        {
            _file = null;
        }
    }

    public static void Info(string message) => Write("INFO ", message);

    public static void Warn(string message) => Write("WARN ", message);

    public static void Error(string message, Exception? ex = null) =>
        Write("ERROR", ex is null ? message : $"{message}: {ex}");

    private static void Write(string level, string message)
    {
        var file = _file;
        if (file is null)
        {
            return;
        }

        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] [{Environment.ProcessId}] {message}{Environment.NewLine}";
        lock (Sync)
        {
            try
            {
                var info = new FileInfo(file);
                if (info.Exists && info.Length > MaxSize)
                {
                    File.Move(file, file + ".1", overwrite: true);
                }

                File.AppendAllText(file, line, Encoding.UTF8);
            }
            catch
            {
                // Protokollierung darf die Anwendung nie beeinträchtigen.
            }
        }
    }
}
