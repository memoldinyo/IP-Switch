namespace IPSwitch.Core.Models;

/// <summary>
/// Zuordnung eines Profils zu einem Adapter. Primär über die Interface-GUID, die sich bei einer
/// Umbenennung des Adapters nicht ändert. MAC-Adresse und Namen dienen als Rückfallebene bzw. zur Anzeige.
/// </summary>
public sealed class AdapterBinding
{
    /// <summary>Interface-GUID im Format "{xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx}".</summary>
    public string InterfaceId { get; set; } = string.Empty;

    /// <summary>MAC-Adresse im Format "AA-BB-CC-DD-EE-FF" (optional).</summary>
    public string? MacAddress { get; set; }

    /// <summary>Zuletzt bekannter Verbindungsname (nur zur Anzeige).</summary>
    public string? LastKnownName { get; set; }

    /// <summary>Zuletzt bekannte Gerätebeschreibung (nur zur Anzeige).</summary>
    public string? LastKnownDescription { get; set; }

    public AdapterBinding Clone() => new()
    {
        InterfaceId = InterfaceId,
        MacAddress = MacAddress,
        LastKnownName = LastKnownName,
        LastKnownDescription = LastKnownDescription,
    };

    public static AdapterBinding FromAdapter(AdapterInfo adapter) => new()
    {
        InterfaceId = adapter.Id,
        MacAddress = adapter.MacAddress,
        LastKnownName = adapter.Name,
        LastKnownDescription = adapter.Description,
    };
}
