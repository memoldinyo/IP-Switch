namespace IPSwitch.Core.Models;

/// <summary>
/// Auftrag an den (erhöhten) Hilfsprozess: Konfiguration auf einen Adapter anwenden.
/// </summary>
public sealed class ApplyRequest
{
    public string InterfaceId { get; set; } = string.Empty;

    /// <summary>MAC-Adresse als Rückfallebene, falls die GUID nicht mehr existiert.</summary>
    public string? MacAddress { get; set; }

    public AdapterConfiguration Target { get; set; } = new();

    /// <summary>Beschreibung des Vorgangs für Sicherung und Protokoll.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Verzeichnis, in das die Sicherung geschrieben wird.</summary>
    public string BackupDirectory { get; set; } = string.Empty;

    /// <summary>Oberflächensprache für Meldungen des Hilfsprozesses (z. B. "de").</summary>
    public string? Language { get; set; }
}

public enum ApplyOutcome
{
    Success = 0,
    FailedRolledBack = 1,
    FailedRollbackFailed = 2,
    InvalidRequest = 3,
    FailedNoChanges = 4,
}

public sealed class ApplyResult
{
    public ApplyOutcome Outcome { get; set; }

    public bool Success => Outcome == ApplyOutcome.Success;

    public string Message { get; set; } = string.Empty;

    public string? BackupFile { get; set; }

    /// <summary>Ausgeführte Befehle mit Exit-Code und Ausgabe (für Protokoll/Details).</summary>
    public List<string> Log { get; set; } = [];

    public static ApplyResult Invalid(string message) => new() { Outcome = ApplyOutcome.InvalidRequest, Message = message };
}
