using IPSwitch.Core.Localization;
using IPSwitch.Core.Diagnostics;
using IPSwitch.Core.Models;
using IPSwitch.Core.Storage;
using IPSwitch.Core.Validation;

namespace IPSwitch.Core.Network;

/// <summary>
/// Wendet eine Konfiguration auf einen Adapter an. Ablauf:
/// 1. Eingaben erneut prüfen, 2. aktuelle Konfiguration sichern, 3. netsh-Befehle ausführen,
/// 4. Ergebnis anhand der gespeicherten Konfiguration überprüfen, 5. bei Fehlern die Sicherung zurückspielen.
/// Benötigt Administratorrechte und läuft daher im erhöhten Hilfsprozess.
/// </summary>
public sealed class NetworkConfigurator
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(60);

    private readonly ICommandRunner _runner;
    private readonly IConfigurationReader _reader;
    private readonly IAdapterLocator _locator;
    private readonly string _netshPath;
    private readonly int _verifyAttempts;
    private readonly TimeSpan _verifyDelay;

    public NetworkConfigurator(
        ICommandRunner runner,
        IConfigurationReader reader,
        IAdapterLocator locator,
        string? netshPath = null,
        int verifyAttempts = 10,
        TimeSpan? verifyDelay = null)
    {
        _runner = runner;
        _reader = reader;
        _locator = locator;
        _netshPath = netshPath ?? Path.Combine(Environment.SystemDirectory, "netsh.exe");
        _verifyAttempts = Math.Max(1, verifyAttempts);
        _verifyDelay = verifyDelay ?? TimeSpan.FromMilliseconds(300);
    }

    public static NetworkConfigurator CreateDefault()
    {
        var reader = new TcpipRegistryReader();
        return new NetworkConfigurator(new ProcessCommandRunner(), reader, new AdapterService(reader));
    }

    public ApplyResult Apply(ApplyRequest request)
    {
        var errors = ConfigurationValidator.Validate(request.Target);
        if (errors.Count > 0)
        {
            return ApplyResult.Invalid(Loc.T("apply.invalidConfig") + Environment.NewLine + string.Join(Environment.NewLine, errors));
        }

        if (string.IsNullOrWhiteSpace(request.BackupDirectory) || !Path.IsPathFullyQualified(request.BackupDirectory))
        {
            return ApplyResult.Invalid(Loc.T("apply.noBackupDir"));
        }

        var adapter = _locator.Find(request.InterfaceId, request.MacAddress);
        if (adapter is null)
        {
            return ApplyResult.Invalid(Loc.T("apply.adapterNotFound"));
        }

        if (adapter.IPv4Index is not { } index)
        {
            return ApplyResult.Invalid(Loc.T("apply.noIpv4", adapter.Name));
        }

        if (AddressConflicts.Find(_locator.GetAll(), adapter.Id, request.Target) is { } conflict)
        {
            return ApplyResult.Invalid(conflict);
        }

        var result = new ApplyResult();

        // 1) Sicherung – ohne Sicherung keine Änderung.
        var previous = _reader.ReadConfiguration(adapter.Id) ?? FromActiveState(adapter);
        var backup = new ConfigurationBackup
        {
            InterfaceId = adapter.Id,
            AdapterName = adapter.Name,
            MacAddress = adapter.MacAddress,
            Reason = string.IsNullOrWhiteSpace(request.Label) ? Loc.T("backup.reasonDefault") : Loc.T("backup.reason", request.Label),
            Configuration = previous.Clone(),
        };
        try
        {
            result.BackupFile = new BackupStore(request.BackupDirectory).Save(backup);
            result.Log.Add(Loc.T("apply.log.backup", result.BackupFile));
            Log.Info($"Sicherung für {adapter.Name} ({adapter.Id}) erstellt: {previous.Summary}");
        }
        catch (Exception ex)
        {
            Log.Error("Sicherung fehlgeschlagen", ex);
            result.Outcome = ApplyOutcome.FailedNoChanges;
            result.Message = Loc.T("apply.backupFailed", ex.Message);
            return result;
        }

        // 2) Anwenden.
        Log.Info($"Wende auf {adapter.Name} (Index {index}) an: {request.Target.Summary}");
        var failure = Execute(NetshCommandBuilder.Build(index, request.Target, previous), result.Log);
        failure ??= Verify(adapter.Id, request.Target, result.Log);

        if (failure is null)
        {
            result.Outcome = ApplyOutcome.Success;
            result.Message = Loc.T("apply.success", adapter.Name, request.Target.Summary);
            Log.Info(result.Message);
            return result;
        }

        // 3) Fehler → vorherige Konfiguration wiederherstellen.
        Log.Warn($"Anwenden fehlgeschlagen: {failure}. Stelle vorherige Konfiguration wieder her.");
        result.Log.Add(Loc.T("apply.log.rollback"));
        string? rollbackFailure;
        var previousErrors = ConfigurationValidator.Validate(previous);
        if (previousErrors.Count > 0)
        {
            rollbackFailure = Loc.T("apply.backupIncomplete", string.Join(" ", previousErrors));
            result.Log.Add(rollbackFailure);
        }
        else
        {
            var afterFailure = _reader.ReadConfiguration(adapter.Id);
            rollbackFailure = ExecuteAll(NetshCommandBuilder.Build(index, previous, afterFailure), result.Log);
            rollbackFailure ??= Verify(adapter.Id, previous, result.Log);
        }

        if (rollbackFailure is null)
        {
            result.Outcome = ApplyOutcome.FailedRolledBack;
            result.Message = Loc.T("apply.failedRolledBack", failure);
        }
        else
        {
            result.Outcome = ApplyOutcome.FailedRollbackFailed;
            result.Message = Loc.T("apply.failedRollbackFailed", failure, rollbackFailure, result.BackupFile, previous.Summary);
        }

        Log.Warn(result.Message);
        return result;
    }

    /// <summary>Führt Befehle aus und bricht beim ersten nicht tolerierten Fehler ab.</summary>
    private string? Execute(IReadOnlyList<NetshCommand> commands, List<string> log)
    {
        foreach (var command in commands)
        {
            var output = Run(command, log);
            if (!output.Succeeded && !command.AllowFailure)
            {
                var hint = output.Output.Contains("bereits vorhanden", StringComparison.OrdinalIgnoreCase) ||
                           output.Output.Contains("already exists", StringComparison.OrdinalIgnoreCase)
                    ? " " + Loc.T("apply.hintInUse")
                    : string.Empty;
                return Loc.T("apply.commandFailed", command.Description, output.ExitCode, FirstLines(output.Output)) + hint;
            }
        }

        return null;
    }

    /// <summary>Führt alle Befehle aus (für die Wiederherstellung: möglichst viel zurückspielen).</summary>
    private string? ExecuteAll(IReadOnlyList<NetshCommand> commands, List<string> log)
    {
        var failures = new List<string>();
        foreach (var command in commands)
        {
            var output = Run(command, log);
            if (!output.Succeeded && !command.AllowFailure)
            {
                failures.Add($"{command.Description}: {FirstLines(output.Output)}");
            }
        }

        return failures.Count == 0 ? null : string.Join(Environment.NewLine, failures);
    }

    private CommandResult Run(NetshCommand command, List<string> log)
    {
        CommandResult output;
        try
        {
            output = _runner.Run(_netshPath, command.Arguments, CommandTimeout);
        }
        catch (Exception ex)
        {
            output = new CommandResult(-1, ex.Message);
        }

        var entry = $"{command.CommandLine} → Code {output.ExitCode}" +
                    (string.IsNullOrWhiteSpace(output.Output) ? string.Empty : $": {output.Output.ReplaceLineEndings(" ").Trim()}");
        log.Add(entry);
        Log.Info(entry);
        return output;
    }

    /// <summary>Prüft (mit Wiederholungen), ob die gespeicherte Konfiguration dem Ziel entspricht.</summary>
    private string? Verify(string interfaceId, AdapterConfiguration target, List<string> log)
    {
        string? mismatch = null;
        for (var attempt = 0; attempt < _verifyAttempts; attempt++)
        {
            var actual = _reader.ReadConfiguration(interfaceId);
            if (actual is null)
            {
                log.Add(Loc.T("verify.skipped"));
                return null;
            }

            mismatch = Compare(target, actual);
            if (mismatch is null)
            {
                log.Add(Loc.T("verify.ok"));
                return null;
            }

            if (_verifyDelay > TimeSpan.Zero)
            {
                Thread.Sleep(_verifyDelay);
            }
        }

        log.Add(Loc.T("verify.logFailed", mismatch));
        return Loc.T("verify.failed", mismatch);
    }

    /// <summary>Vergleicht Ziel- und Istkonfiguration; liefert eine Beschreibung der Abweichung oder null.</summary>
    internal static string? Compare(AdapterConfiguration target, AdapterConfiguration actual)
    {
        if (target.DhcpEnabled != actual.DhcpEnabled)
        {
            return Loc.T(target.DhcpEnabled ? "compare.dhcpOff" : "compare.dhcpOn");
        }

        if (!target.DhcpEnabled)
        {
            foreach (var address in target.Addresses)
            {
                var targetPrefix = IpValidator.MaskToPrefix(address.SubnetMask);
                if (!actual.Addresses.Any(a => a.Address == address.Address && IpValidator.MaskToPrefix(a.SubnetMask) == targetPrefix))
                {
                    return Loc.T("compare.addressMissing", $"{address.Address}/{targetPrefix}");
                }
            }

            if (!target.Gateways.ToHashSet().SetEquals(actual.Gateways))
            {
                return Loc.T("compare.gateway", Describe(target.Gateways), Describe(actual.Gateways));
            }
        }

        if (target.DnsFromDhcp != actual.DnsFromDhcp)
        {
            return Loc.T(target.DnsFromDhcp ? "compare.dnsNotDhcp" : "compare.dnsStillDhcp");
        }

        if (!target.DnsFromDhcp && !target.DnsServers.SequenceEqual(actual.DnsServers))
        {
            return Loc.T("compare.dns", Describe(target.DnsServers), Describe(actual.DnsServers));
        }

        return null;
    }

    /// <summary>Rückfall, wenn die persistente Konfiguration nicht gelesen werden kann.</summary>
    internal static AdapterConfiguration FromActiveState(AdapterInfo adapter)
    {
        var config = new AdapterConfiguration { DhcpEnabled = adapter.DhcpEnabled };
        if (!adapter.DhcpEnabled)
        {
            config.Addresses.AddRange(adapter.Addresses.Select(a => new StaticAddress(a.Address, a.SubnetMask)));
            config.Gateways.AddRange(adapter.Gateways);
            config.DnsServers.AddRange(adapter.DnsServers);
        }
        else
        {
            config.DnsFromDhcp = true;
        }

        return config;
    }

    private static string Describe(IEnumerable<string> values)
    {
        var list = values.ToList();
        return list.Count == 0 ? Loc.T("common.none") : string.Join(", ", list);
    }

    private static string FirstLines(string text)
    {
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return lines.Length == 0 ? Loc.T("apply.noOutput") : string.Join(" ", lines.Take(3));
    }
}
