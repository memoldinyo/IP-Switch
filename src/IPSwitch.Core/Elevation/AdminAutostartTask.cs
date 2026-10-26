using IPSwitch.Core.Localization;
using System.Text;
using System.Xml.Linq;
using IPSwitch.Core.Diagnostics;
using IPSwitch.Core.Models;
using IPSwitch.Core.Network;

namespace IPSwitch.Core.Elevation;

/// <summary>
/// Geplante Aufgabe, die IP-Switch bei der Anmeldung mit höchsten Rechten startet. Eine solche Aufgabe ist der
/// von Windows vorgesehene Weg, ein Programm ohne UAC-Abfrage erhöht zu starten (der Run-Schlüssel kann das nicht).
/// Anlegen und Entfernen erfordern Administratorrechte; verwendet wird schtasks.exe mit einer Aufgaben-XML.
/// </summary>
public sealed class AdminAutostartTask
{
    private static readonly XNamespace Ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private readonly ICommandRunner _runner;
    private readonly string _schtasks;

    public AdminAutostartTask(ICommandRunner? runner = null, string? schtasksPath = null)
    {
        _runner = runner ?? new ProcessCommandRunner();
        _schtasks = schtasksPath ?? Path.Combine(Environment.SystemDirectory, "schtasks.exe");
    }

    /// <summary>Aufgabenname, pro Benutzer eindeutig (Aufgaben sind rechnerweit sichtbar).</summary>
    public static string TaskName(string userSid) => $"IP-Switch Autostart (Administrator) {userSid}";

    /// <summary>
    /// Aufgabendefinition: Anmeldetrigger und Ausführung für genau diesen Benutzer, höchste Rechte, keine Laufzeitbegrenzung
    /// (Standard wären 72 Stunden), auch im Akkubetrieb.
    /// </summary>
    public static string BuildXml(string userSid, string executablePath, string runLevel = "HighestAvailable")
    {
        var document = new XDocument(
            new XDeclaration("1.0", "UTF-16", null),
            new XElement(Ns + "Task", new XAttribute("version", "1.2"),
                new XElement(Ns + "RegistrationInfo",
                    new XElement(Ns + "Author", "IP-Switch"),
                    new XElement(Ns + "Description", Loc.T("task.description"))),
                new XElement(Ns + "Triggers",
                    new XElement(Ns + "LogonTrigger",
                        new XElement(Ns + "Enabled", "true"),
                        new XElement(Ns + "UserId", userSid))),
                new XElement(Ns + "Principals",
                    new XElement(Ns + "Principal", new XAttribute("id", "Author"),
                        new XElement(Ns + "UserId", userSid),
                        new XElement(Ns + "LogonType", "InteractiveToken"),
                        new XElement(Ns + "RunLevel", runLevel))),
                new XElement(Ns + "Settings",
                    new XElement(Ns + "MultipleInstancesPolicy", "IgnoreNew"),
                    new XElement(Ns + "DisallowStartIfOnBatteries", "false"),
                    new XElement(Ns + "StopIfGoingOnBatteries", "false"),
                    new XElement(Ns + "AllowHardTerminate", "true"),
                    new XElement(Ns + "StartWhenAvailable", "false"),
                    new XElement(Ns + "RunOnlyIfNetworkAvailable", "false"),
                    new XElement(Ns + "IdleSettings",
                        new XElement(Ns + "StopOnIdleEnd", "false"),
                        new XElement(Ns + "RestartOnIdle", "false")),
                    new XElement(Ns + "AllowStartOnDemand", "true"),
                    new XElement(Ns + "Enabled", "true"),
                    new XElement(Ns + "Hidden", "false"),
                    new XElement(Ns + "RunOnlyIfIdle", "false"),
                    new XElement(Ns + "ExecutionTimeLimit", "PT0S"),
                    new XElement(Ns + "Priority", "7")),
                new XElement(Ns + "Actions", new XAttribute("Context", "Author"),
                    new XElement(Ns + "Exec",
                        new XElement(Ns + "Command", executablePath),
                        new XElement(Ns + "Arguments", "--autostart")))));

        using var writer = new StringWriter();
        document.Save(writer);
        return writer.ToString();
    }

    /// <summary>Legt die Aufgabe an bzw. ersetzt sie (benötigt Administratorrechte).</summary>
    public ApplyResult Enable(string userSid, string executablePath)
    {
        if (!ElevationInfo.IsValidSid(userSid))
        {
            return ApplyResult.Invalid(Loc.T("task.invalidSid"));
        }

        if (!Path.IsPathFullyQualified(executablePath) || !File.Exists(executablePath))
        {
            return ApplyResult.Invalid(Loc.T("task.exeMissing", executablePath));
        }

        var xmlFile = Path.Combine(Path.GetTempPath(), $"ipswitch-task-{Guid.NewGuid():N}.xml");
        try
        {
            File.WriteAllText(xmlFile, BuildXml(userSid, executablePath), Encoding.Unicode);
            var result = Run(["/Create", "/TN", TaskName(userSid), "/XML", xmlFile, "/F"]);
            return result.Succeeded
                ? Success(Loc.T("task.created"))
                : Failure(Loc.T("task.createFailed"), result);
        }
        finally
        {
            try
            {
                File.Delete(xmlFile);
            }
            catch (IOException)
            {
            }
        }
    }

    /// <summary>Entfernt die Aufgabe (benötigt Administratorrechte). Eine nicht vorhandene Aufgabe gilt als entfernt.</summary>
    public ApplyResult Disable(string userSid)
    {
        if (!ElevationInfo.IsValidSid(userSid))
        {
            return ApplyResult.Invalid(Loc.T("task.invalidSid"));
        }

        var result = Run(["/Delete", "/TN", TaskName(userSid), "/F"]);
        if (result.Succeeded || !Exists(userSid))
        {
            return Success(Loc.T("task.removed"));
        }

        return Failure(Loc.T("task.removeFailed"), result);
    }

    public bool Exists(string userSid) => Run(["/Query", "/TN", TaskName(userSid)]).Succeeded;

    /// <summary>Startet die Aufgabe sofort (z. B. für einen Neustart mit Administratorrechten).</summary>
    public bool TryStart(string userSid) => Run(["/Run", "/TN", TaskName(userSid)]).Succeeded;

    private CommandResult Run(IReadOnlyList<string> arguments)
    {
        try
        {
            var result = _runner.Run(_schtasks, arguments, Timeout);
            Log.Info($"schtasks {string.Join(' ', arguments)} → Code {result.ExitCode}");
            return result;
        }
        catch (Exception ex)
        {
            Log.Error("schtasks konnte nicht ausgeführt werden", ex);
            return new CommandResult(-1, ex.Message);
        }
    }

    private static ApplyResult Success(string message) => new() { Outcome = ApplyOutcome.Success, Message = message };

    private static ApplyResult Failure(string message, CommandResult result) => new()
    {
        Outcome = ApplyOutcome.FailedNoChanges,
        Message = $"{message} (Code {result.ExitCode}): {result.Output.Trim()}",
    };
}
