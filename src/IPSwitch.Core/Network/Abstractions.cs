using IPSwitch.Core.Models;

namespace IPSwitch.Core.Network;

public sealed record CommandResult(int ExitCode, string Output)
{
    public bool Succeeded => ExitCode == 0;
}

/// <summary>Führt externe Programme aus (in Tests durch eine Attrappe ersetzbar).</summary>
public interface ICommandRunner
{
    CommandResult Run(string fileName, IReadOnlyList<string> arguments, TimeSpan timeout);
}

/// <summary>Liest die persistente IPv4-Konfiguration eines Adapters.</summary>
public interface IConfigurationReader
{
    /// <returns>Die Konfiguration oder null, wenn sie nicht ermittelt werden kann.</returns>
    AdapterConfiguration? ReadConfiguration(string interfaceId);
}

/// <summary>Findet einen Adapter anhand seiner GUID (Rückfall: eindeutige MAC-Adresse).</summary>
public interface IAdapterLocator
{
    AdapterInfo? Find(string interfaceId, string? macAddress);

    /// <summary>Alle Adapter (inkl. virtueller Hilfsadapter), z. B. für die Prüfung auf Adresskonflikte.</summary>
    IReadOnlyList<AdapterInfo> GetAll();
}
