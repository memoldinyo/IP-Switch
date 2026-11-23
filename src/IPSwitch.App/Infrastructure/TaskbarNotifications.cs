using System.Runtime.InteropServices;
using IPSwitch.Core.Diagnostics;

namespace IPSwitch.App.Infrastructure;

/// <summary>
/// Sorgt dafür, dass das Tray-Symbol wieder erscheint, wenn die Taskleiste (neu) entsteht – etwa wenn IP-Switch beim
/// Anmelden vor dem Explorer startet oder der Explorer neu startet. Der Explorer meldet das allen Fenstern mit der
/// Nachricht „TaskbarCreated“, auf die <see cref="NotifyIcon"/> das Symbol neu anlegt. Läuft IP-Switch mit
/// Administratorrechten, blockiert Windows (UIPI) diese Nachricht vom Explorer mit Standardrechten; sie muss
/// ausdrücklich zugelassen werden.
/// </summary>
internal static class TaskbarNotifications
{
    private const uint MsgFltAdd = 1;

    /// <summary>Vor dem Anlegen des Tray-Symbols aufrufen.</summary>
    public static void AllowTaskbarCreated()
    {
        try
        {
            var message = RegisterWindowMessage("TaskbarCreated");
            if (message == 0 || !ChangeWindowMessageFilter(message, MsgFltAdd))
            {
                Log.Warn("Nachricht „TaskbarCreated“ konnte nicht zugelassen werden (Fehler " + Marshal.GetLastWin32Error() + ").");
            }
        }
        catch (Exception ex)
        {
            Log.Warn("Nachricht „TaskbarCreated“ konnte nicht zugelassen werden: " + ex.Message);
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint RegisterWindowMessage(string message);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ChangeWindowMessageFilter(uint message, uint flag);
}
