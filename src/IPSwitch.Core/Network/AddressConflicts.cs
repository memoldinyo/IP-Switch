using IPSwitch.Core.Localization;
using IPSwitch.Core.Models;

namespace IPSwitch.Core.Network;

/// <summary>
/// Windows erlaubt dieselbe IPv4-Adresse nicht auf zwei Adaptern ("Das Objekt ist bereits vorhanden").
/// Diese Prüfung erkennt den Konflikt vor einer Änderung und erklärt ihn verständlich.
/// </summary>
public static class AddressConflicts
{
    /// <returns>Beschreibung des Konflikts oder null.</returns>
    public static string? Find(IEnumerable<AdapterInfo> adapters, string targetInterfaceId, AdapterConfiguration target)
    {
        if (target.DhcpEnabled)
        {
            return null;
        }

        foreach (var address in target.Addresses.Select(a => a.Address))
        {
            foreach (var other in adapters.Where(a => !AdapterService.SameId(a.Id, targetInterfaceId)))
            {
                var persistent = other.PersistentConfiguration;
                var residual = persistent?.ResidualAddresses.Any(a => a.Address == address) == true;
                var used = residual ||
                           other.Addresses.Any(a => a.Address == address) ||
                           persistent?.Addresses.Any(a => a.Address == address) == true;
                if (!used)
                {
                    continue;
                }

                return Loc.T(residual ? "conflict.residual" : "conflict.inUse", address, other.Name);
            }
        }

        return null;
    }
}
