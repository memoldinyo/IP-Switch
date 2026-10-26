using IPSwitch.Core.Localization;
using System.Diagnostics;
using IPSwitch.Core.Models;

namespace IPSwitch.App.UI;

/// <summary>Einheitliche Meldungen. Ohne Besitzerfenster (Aufruf aus dem Tray) erscheinen sie im Vordergrund.</summary>
internal static class Dialogs
{
    public const string Title = "IP-Switch";

    /// <summary>Liefert das Fenster nur, wenn es noch existiert (es kann während eines await geschlossen worden sein).</summary>
    public static IWin32Window? Alive(IWin32Window? owner) =>
        owner is Control { IsDisposed: true } or Control { IsHandleCreated: false } ? null : owner;

    public static void Info(IWin32Window? owner, string message) =>
        WithOwner(owner, o => Show(o, message, MessageBoxButtons.OK, MessageBoxIcon.Information));

    public static void Warning(IWin32Window? owner, string message) =>
        WithOwner(owner, o => Show(o, message, MessageBoxButtons.OK, MessageBoxIcon.Warning));

    public static void Error(IWin32Window? owner, string message, string? details = null)
    {
        if (string.IsNullOrWhiteSpace(details))
        {
            WithOwner(owner, o => Show(o, message, MessageBoxButtons.OK, MessageBoxIcon.Error));
            return;
        }

        using var dialog = new DetailsDialog(message, details, MessageBoxIcon.Error);
        ShowModal(dialog, owner);
    }

    public static bool Confirm(IWin32Window? owner, string message, MessageBoxIcon icon = MessageBoxIcon.Question) =>
        WithOwner(owner, o => Show(o, message, MessageBoxButtons.YesNo, icon)) == DialogResult.Yes;

    public static DialogResult Ask(IWin32Window? owner, string message, MessageBoxButtons buttons) =>
        WithOwner(owner, o => Show(o, message, buttons, MessageBoxIcon.Question));

    /// <summary>Meldungsfenster; bei Arabisch von rechts nach links.</summary>
    private static DialogResult Show(IWin32Window owner, string message, MessageBoxButtons buttons, MessageBoxIcon icon) =>
        MessageBox.Show(owner, message, Title, buttons, icon, MessageBoxDefaultButton.Button1,
            Loc.IsRightToLeft ? MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign : 0);

    /// <summary>Zeigt einen modalen Dialog; ohne Besitzer zentriert und in den Vordergrund geholt.</summary>
    public static DialogResult ShowModal(Form form, IWin32Window? owner)
    {
        owner = Alive(owner);
        if (owner is null)
        {
            form.StartPosition = FormStartPosition.CenterScreen;
            form.ShowInTaskbar = true;
            form.Shown += (_, _) =>
            {
                form.TopMost = true;
                form.Activate();
                form.TopMost = false;
            };
        }
        else
        {
            form.StartPosition = FormStartPosition.CenterParent;
        }

        return form.ShowDialog(owner);
    }

    public static void ShowApplyResult(IWin32Window? owner, ApplyResult result, string logDirectory)
    {
        var icon = result.Outcome == ApplyOutcome.FailedRollbackFailed ? MessageBoxIcon.Error : MessageBoxIcon.Warning;
        var details = string.Join(Environment.NewLine, result.Log) +
                      $"{Environment.NewLine}{Environment.NewLine}{Loc.T("dialog.logLocation", logDirectory)}";
        using var dialog = new DetailsDialog(result.Message, details, icon);
        ShowModal(dialog, owner);
    }

    public static void OpenFolder(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"))
            {
                ArgumentList = { path },
                UseShellExecute = false,
            });
        }
        catch (Exception ex)
        {
            Error(null, Loc.T("dialog.folderFailed", path, ex.Message));
        }
    }

    private static T WithOwner<T>(IWin32Window? owner, Func<IWin32Window, T> show)
    {
        owner = Alive(owner);
        if (owner is not null)
        {
            return show(owner);
        }

        // Unsichtbares Hilfsfenster, damit die Meldung nicht hinter anderen Fenstern verschwindet.
        var screen = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 800, 600);
        using var host = new Form
        {
            FormBorderStyle = FormBorderStyle.None,
            ShowInTaskbar = false,
            StartPosition = FormStartPosition.Manual,
            Location = new Point(screen.Left + (screen.Width / 2), screen.Top + (screen.Height / 2)),
            Size = new Size(1, 1),
            Opacity = 0,
            TopMost = true,
        };
        host.Show();
        host.Activate();
        return show(host);
    }
}
