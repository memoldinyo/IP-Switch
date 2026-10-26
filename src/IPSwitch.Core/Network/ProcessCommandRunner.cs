using IPSwitch.Core.Localization;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace IPSwitch.Core.Network;

/// <summary>Startet Prozesse ohne Shell und ohne Fenster und liest deren Ausgabe.</summary>
public sealed class ProcessCommandRunner : ICommandRunner
{
    private static readonly Encoding ConsoleEncoding = GetConsoleEncoding();

    public CommandResult Run(string fileName, IReadOnlyList<string> arguments, TimeSpan timeout)
    {
        var psi = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = ConsoleEncoding,
            StandardErrorEncoding = ConsoleEncoding,
            WorkingDirectory = Environment.SystemDirectory,
        };
        foreach (var argument in arguments)
        {
            psi.ArgumentList.Add(argument);
        }

        using var process = Process.Start(psi)
                            ?? throw new InvalidOperationException($"{fileName} konnte nicht gestartet werden.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit(timeout))
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // Prozess wurde inzwischen beendet.
            }

            return new CommandResult(-1, Loc.T("process.timeout", (int)timeout.TotalSeconds));
        }

        process.WaitForExit();
        var output = (stdout.Result + Environment.NewLine + stderr.Result).Trim();
        return new CommandResult(process.ExitCode, output);
    }

    private static Encoding GetConsoleEncoding()
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage);
        }
        catch (Exception)
        {
            return Encoding.UTF8;
        }
    }
}
