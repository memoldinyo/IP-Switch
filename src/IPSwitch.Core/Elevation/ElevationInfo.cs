using IPSwitch.Core.Localization;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace IPSwitch.Core.Elevation;

/// <summary>Auskunft über die Rechte des aktuellen Prozesses bzw. Benutzerkontos.</summary>
public static class ElevationInfo
{
    private const int TokenElevationTypeClass = 18;

    private enum TokenElevationType
    {
        Default = 1,
        Full = 2,
        Limited = 3,
    }

    /// <summary>true, wenn der Prozess mit Administratorrechten läuft.</summary>
    public static bool IsElevated
    {
        get
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    /// <summary>
    /// true, wenn das angemeldete Konto ein Administratorkonto ist (UAC-Bestätigung ohne fremde Anmeldedaten möglich).
    /// Bei Standardkonten bringt ein Start "mit höchsten Rechten" nichts.
    /// </summary>
    public static bool IsAdministratorAccount
    {
        get
        {
            if (IsElevated)
            {
                return true;
            }

            using var identity = WindowsIdentity.GetCurrent();
            return GetTokenInformation(identity.Token, TokenElevationTypeClass, out var type, sizeof(int), out _) &&
                   type == (int)TokenElevationType.Limited;
        }
    }

    /// <summary>SID des aktuellen Benutzers (z. B. "S-1-5-21-…").</summary>
    public static string CurrentUserSid
    {
        get
        {
            using var identity = WindowsIdentity.GetCurrent();
            return identity.User?.Value ?? throw new InvalidOperationException(Loc.T("elevation.noSid"));
        }
    }

    public static bool IsValidSid(string? sid)
    {
        if (string.IsNullOrWhiteSpace(sid) || !sid.StartsWith("S-1-", StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            _ = new SecurityIdentifier(sid);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool GetTokenInformation(IntPtr tokenHandle, int tokenInformationClass, out int tokenInformation, int tokenInformationLength, out int returnLength);
}
