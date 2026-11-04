using IPSwitch.Core.Localization;
using IPSwitch.Core.Diagnostics;
using IPSwitch.Core.Models;

namespace IPSwitch.Core.Network;

/// <summary>
/// Aktiviert oder deaktiviert einen Netzwerkadapter (wie „Aktivieren/Deaktivieren“ in den Netzwerkverbindungen).
/// Zuerst über <c>netsh interface set interface</c>, ersatzweise über Enable-/Disable-NetAdapter; anschließend wird
/// der Gerätezustand geprüft. Die IP-Konfiguration bleibt unverändert. Benötigt Administratorrechte.
/// </summary>
public sealed class AdapterStateChanger
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(60);

    private readonly ICommandRunner _runner;
    private readonly IAdapterLocator _locator;
    private readonly Func<string, bool> _isDisabled;
    private readonly string _netshPath;
    private readonly string _powerShellPath;
    private readonly int _verifyAttempts;
    private readonly TimeSpan _verifyDelay;

    public AdapterStateChanger(
        ICommandRunner runner,
        IAdapterLocator locator,
        Func<string, bool> isDisabled,
        string? netshPath = null,
        string? powerShellPath = null,
        int verifyAttempts = 30,
        TimeSpan? verifyDelay = null)
    {
        _runner = runner;
        _locator = locator;
        _isDisabled = isDisabled;
        _netshPath = netshPath ?? Path.Combine(Environment.SystemDirectory, "netsh.exe");
        _powerShellPath = powerShellPath ?? Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe");
        _verifyAttempts = Math.Max(1, verifyAttempts);
        _verifyDelay = verifyDelay ?? TimeSpan.FromMilliseconds(300);
    }

    public static AdapterStateChanger CreateDefault() =>
        new(new ProcessCommandRunner(), new AdapterService(), AdapterDevice.IsDisabled);

    public ApplyResult SetEnabled(string interfaceId, bool enable)
    {
        if (!Guid.TryParse(interfaceId, out var guid))
        {
            return ApplyResult.Invalid(Loc.T("adapterState.invalidId"));
        }

        var adapter = _locator.Find(interfaceId, null);
        if (adapter is null)
        {
            return ApplyResult.Invalid(Loc.T("apply.adapterNotFound"));
        }

        var result = new ApplyResult();
        if (adapter.IsDisabled != enable)
        {
            result.Message = Loc.T(enable ? "adapterState.alreadyEnabled" : "adapterState.alreadyDisabled", adapter.Name);
            return result;
        }

        Log.Info($"{(enable ? "Aktiviere" : "Deaktiviere")} {adapter.Name} ({adapter.Id})");
        var netsh = Run(_netshPath, ["interface", "set", "interface", adapter.Name, enable ? "admin=enabled" : "admin=disabled"], result.Log);
        if (!netsh.Succeeded || !WaitForState(adapter.Id, enable))
        {
            // Rückfall, z. B. wenn netsh den Namen nicht findet. Die GUID ist geprüft, daher ist das Skript unbedenklich.
            var cmdlet = enable ? "Enable-NetAdapter" : "Disable-NetAdapter";
            var script = $"$a = Get-NetAdapter -IncludeHidden | Where-Object InterfaceGuid -eq '{guid.ToString("B").ToUpperInvariant()}'; " +
                         $"if (-not $a) {{ exit 2 }}; $a | {cmdlet} -Confirm:$false -ErrorAction Stop";
            var fallback = Run(_powerShellPath, ["-NoProfile", "-NonInteractive", "-Command", script], result.Log);
            if (!WaitForState(adapter.Id, enable))
            {
                result.Outcome = ApplyOutcome.FailedNoChanges;
                var output = fallback.Succeeded ? netsh.Output : fallback.Output;
                result.Message = Loc.T(enable ? "adapterState.enableFailed" : "adapterState.disableFailed", adapter.Name, FirstLines(output));
                Log.Warn(result.Message);
                return result;
            }
        }

        result.Message = Loc.T(enable ? "adapterState.enabled" : "adapterState.disabled", adapter.Name);
        result.Log.Add(result.Message);
        Log.Info(result.Message);
        return result;
    }

    /// <summary>Wartet (mit Wiederholungen), bis der Gerätezustand dem Ziel entspricht.</summary>
    private bool WaitForState(string interfaceId, bool enable)
    {
        for (var attempt = 0; attempt < _verifyAttempts; attempt++)
        {
            if (_isDisabled(interfaceId) != enable)
            {
                return true;
            }

            if (_verifyDelay > TimeSpan.Zero)
            {
                Thread.Sleep(_verifyDelay);
            }
        }

        return false;
    }

    private CommandResult Run(string fileName, IReadOnlyList<string> arguments, List<string> log)
    {
        CommandResult output;
        try
        {
            output = _runner.Run(fileName, arguments, CommandTimeout);
        }
        catch (Exception ex)
        {
            output = new CommandResult(-1, ex.Message);
        }

        var entry = $"{Path.GetFileNameWithoutExtension(fileName)} {string.Join(' ', arguments)} → Code {output.ExitCode}" +
                    (string.IsNullOrWhiteSpace(output.Output) ? string.Empty : $": {output.Output.ReplaceLineEndings(" ").Trim()}");
        log.Add(entry);
        Log.Info(entry);
        return output;
    }

    private static string FirstLines(string text)
    {
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return lines.Length == 0 ? Loc.T("apply.noOutput") : string.Join(" ", lines.Take(3));
    }
}
