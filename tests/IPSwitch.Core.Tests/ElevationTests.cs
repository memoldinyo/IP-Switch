using System.Xml.Linq;
using IPSwitch.Core.Elevation;
using IPSwitch.Core.Network;

namespace IPSwitch.Core.Tests;

public class ElevationTests
{
    private const string Sid = "S-1-5-21-1111111111-2222222222-3333333333-1001";

    /// <summary>Zeichnet schtasks-Aufrufe auf und liest die übergebene XML-Datei, solange sie existiert.</summary>
    private sealed class RecordingRunner : ICommandRunner
    {
        public List<string> Calls { get; } = [];

        public string? XmlContent { get; private set; }

        public string? XmlPath { get; private set; }

        public Func<string, int> ExitCode { get; set; } = _ => 0;

        public CommandResult Run(string fileName, IReadOnlyList<string> arguments, TimeSpan timeout)
        {
            var line = string.Join(' ', arguments);
            Calls.Add(line);
            var xmlIndex = arguments.ToList().IndexOf("/XML");
            if (xmlIndex >= 0)
            {
                XmlPath = arguments[xmlIndex + 1];
                XmlContent = File.ReadAllText(XmlPath);
            }

            return new CommandResult(ExitCode(line), "Ausgabe");
        }
    }

    [Fact]
    public void BuildXml_ContainsHighestRunLevelNoTimeLimitAndBatterySettings()
    {
        var xml = XDocument.Parse(AdminAutostartTask.BuildXml(Sid, @"C:\Program Files & Co\IP-Switch\IPSwitch.exe"));
        XNamespace ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";
        string Value(string name) => xml.Descendants(ns + name).Single().Value;

        Assert.Equal("HighestAvailable", Value("RunLevel"));
        Assert.Equal("InteractiveToken", Value("LogonType"));
        Assert.Equal("PT0S", Value("ExecutionTimeLimit"));
        Assert.Equal("false", Value("DisallowStartIfOnBatteries"));
        Assert.Equal("false", Value("StopIfGoingOnBatteries"));
        Assert.Equal(@"C:\Program Files & Co\IP-Switch\IPSwitch.exe", Value("Command"));
        Assert.Equal("--autostart", Value("Arguments"));
        Assert.All(xml.Descendants(ns + "UserId"), e => Assert.Equal(Sid, e.Value));
    }

    [Fact]
    public void Enable_CreatesTaskFromXmlAndDeletesTempFile()
    {
        var runner = new RecordingRunner();
        var exe = typeof(ElevationTests).Assembly.Location;

        var result = new AdminAutostartTask(runner, "schtasks.exe").Enable(Sid, exe);

        Assert.True(result.Success, result.Message);
        var call = Assert.Single(runner.Calls);
        Assert.StartsWith($"/Create /TN {AdminAutostartTask.TaskName(Sid)} /XML ", call);
        Assert.EndsWith(" /F", call);
        Assert.Contains("HighestAvailable", runner.XmlContent);
        Assert.False(File.Exists(runner.XmlPath));
    }

    [Fact]
    public void Enable_FailureIsReported()
    {
        var runner = new RecordingRunner { ExitCode = _ => 1 };
        var result = new AdminAutostartTask(runner, "schtasks.exe").Enable(Sid, typeof(ElevationTests).Assembly.Location);
        Assert.False(result.Success);
        Assert.Contains("nicht angelegt", result.Message);
    }

    [Fact]
    public void Enable_RejectsInvalidInputWithoutCallingSchtasks()
    {
        var runner = new RecordingRunner();
        var task = new AdminAutostartTask(runner, "schtasks.exe");

        Assert.False(task.Enable("kein-sid", typeof(ElevationTests).Assembly.Location).Success);
        Assert.False(task.Enable(Sid, @"C:\gibt\es\nicht.exe").Success);
        Assert.Empty(runner.Calls);
    }

    [Fact]
    public void Disable_MissingTaskCountsAsRemoved()
    {
        // Löschen schlägt fehl, Abfrage zeigt: Aufgabe existiert nicht → Erfolg.
        var runner = new RecordingRunner { ExitCode = _ => 1 };
        var result = new AdminAutostartTask(runner, "schtasks.exe").Disable(Sid);

        Assert.True(result.Success);
        Assert.Equal(["/Delete /TN " + AdminAutostartTask.TaskName(Sid) + " /F", "/Query /TN " + AdminAutostartTask.TaskName(Sid)], runner.Calls);
    }

    [Fact]
    public void Disable_FailsWhenTaskStillExists()
    {
        var runner = new RecordingRunner { ExitCode = line => line.StartsWith("/Delete") ? 1 : 0 };
        Assert.False(new AdminAutostartTask(runner, "schtasks.exe").Disable(Sid).Success);
    }

    [Fact]
    public void TaskName_IsPerUser() =>
        Assert.NotEqual(AdminAutostartTask.TaskName(Sid), AdminAutostartTask.TaskName("S-1-5-21-1-2-3-1002"));

    [Theory]
    [InlineData(Sid, true)]
    [InlineData("S-1-5-32-544", true)]
    [InlineData("S-1-x", false)]
    [InlineData("", false)]
    [InlineData("S-1-5-21-1-2-3-1001 & calc", false)]
    public void IsValidSid(string sid, bool expected) => Assert.Equal(expected, ElevationInfo.IsValidSid(sid));

    [Fact]
    public void CurrentUser_IsResolvable()
    {
        Assert.True(ElevationInfo.IsValidSid(ElevationInfo.CurrentUserSid));
        if (ElevationInfo.IsElevated)
        {
            Assert.True(ElevationInfo.IsAdministratorAccount);
        }
    }

    [Fact]
    public void Protocol_AdminAutostartArguments()
    {
        Assert.Equal($"--admin-autostart enable {Sid} de \"C:\\x\\r.json\"",
            ElevatedProtocol.BuildAdminAutostartArguments(true, Sid, "de", @"C:\x\r.json"));
        Assert.StartsWith("--admin-autostart disable ", ElevatedProtocol.BuildAdminAutostartArguments(false, Sid, "de", @"C:\x\r.json"));
    }
}
