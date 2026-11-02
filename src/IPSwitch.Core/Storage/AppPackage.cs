using System.Runtime.InteropServices;

namespace IPSwitch.Core.Storage;

/// <summary>
/// Erkennt, ob IP-Switch als Paket aus dem Microsoft Store (MSIX) läuft. Dann übernimmt Windows Installation,
/// Updates und Autostart, und die Daten liegen im Paketordner statt in %LOCALAPPDATA%\IPSwitch.
/// </summary>
public static class AppPackage
{
    private const int ErrorInsufficientBuffer = 122;

    /// <summary>Paketfamilienname (z. B. "Herausgeber.IPSwitch_abc123") oder null, wenn nicht als Paket gestartet.</summary>
    public static string? FamilyName { get; } = ReadFamilyName();

    public static bool IsPackaged => FamilyName is not null;

    private static string? ReadFamilyName()
    {
        try
        {
            var length = 0;
            if (GetCurrentPackageFamilyName(ref length, null) != ErrorInsufficientBuffer || length <= 0)
            {
                return null;
            }

            var buffer = new char[length];
            return GetCurrentPackageFamilyName(ref length, buffer) == 0 ? new string(buffer, 0, length - 1) : null;
        }
        catch (EntryPointNotFoundException)
        {
            return null;
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFamilyName(ref int packageFamilyNameLength, char[]? packageFamilyName);
}
