namespace IPSwitch.Core.Models;

/// <summary>
/// Sicherung der Konfiguration eines Adapters, erstellt unmittelbar vor einer Änderung.
/// </summary>
public sealed class ConfigurationBackup
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    public string InterfaceId { get; set; } = string.Empty;

    public string? AdapterName { get; set; }

    public string? MacAddress { get; set; }

    /// <summary>Anlass der Sicherung, z. B. "Vor Anwenden von Profil 'Büro'".</summary>
    public string? Reason { get; set; }

    public AdapterConfiguration Configuration { get; set; } = new();

    /// <summary>Pfad der Datei, aus der die Sicherung geladen wurde (nicht serialisiert).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string? FilePath { get; set; }
}
