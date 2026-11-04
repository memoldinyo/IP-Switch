using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace IPSwitch.Core.Network;

/// <summary>
/// Gerätezustand eines Netzwerkadapters. Ein deaktivierter Adapter hat kein gebundenes IPv4 mehr; er wird über
/// die Netzwerk-Registry (Connection\PnPInstanceId) und den Configuration Manager erkannt. Benötigt keine Administratorrechte.
/// </summary>
public static class AdapterDevice
{
    private const string NetworkClassKey = @"SYSTEM\CurrentControlSet\Control\Network\{4D36E972-E325-11CE-BFC1-08002BE10318}";
    private const uint DnHasProblem = 0x400;
    private const uint ProblemDisabled = 22; // CM_PROB_DISABLED
    private const int CrSuccess = 0;

    /// <summary>true, wenn der Adapter (Gerät) deaktiviert ist – wie „Deaktivieren“ in den Netzwerkverbindungen.</summary>
    public static bool IsDisabled(string interfaceId)
    {
        if (!Guid.TryParse(interfaceId, out var guid))
        {
            return false;
        }

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey($@"{NetworkClassKey}\{guid.ToString("B").ToUpperInvariant()}\Connection");
            if (key?.GetValue("PnPInstanceId") is not string instanceId || instanceId.Length == 0)
            {
                return false;
            }

            return CM_Locate_DevNodeW(out var devInst, instanceId, 0) == CrSuccess &&
                   CM_Get_DevNode_Status(out var status, out var problem, devInst, 0) == CrSuccess &&
                   (status & DnHasProblem) != 0 && problem == ProblemDisabled;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException
                                       or DllNotFoundException or EntryPointNotFoundException)
        {
            return false;
        }
    }

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern int CM_Locate_DevNodeW(out uint devInst, string deviceId, uint flags);

    [DllImport("cfgmgr32.dll")]
    private static extern int CM_Get_DevNode_Status(out uint status, out uint problemNumber, uint devInst, uint flags);
}
