using IPSwitch.Core.Localization;
using IPSwitch.App.Infrastructure;
using IPSwitch.Core.Models;

namespace IPSwitch.App.UI;

/// <summary>Zeigt die Sicherungen eines Adapters und lässt eine davon zur Wiederherstellung auswählen.</summary>
internal sealed class BackupPickerForm : Form
{
    private readonly ListView _list;
    private readonly Button _restore;

    public BackupPickerForm(AppController controller, AdapterInfo adapter)
    {
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = SystemFonts.MessageBoxFont;
        Text = Loc.T("backups.title", adapter.Name);
        Icon = AppIcons.Window;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(760, 380);
        MinimumSize = new Size(520, 260);
        Padding = new Padding(12);

        var info = new Label
        {
            Text = Loc.T("backups.info"),
            AutoSize = true,
            MaximumSize = new Size(720, 0),
            Dock = DockStyle.Top,
            Padding = new Padding(0, 0, 0, 8),
        };

        _list = new ListView
        {
            View = View.Details,
            FullRowSelect = true,
            MultiSelect = false,
            HideSelection = false,
            Dock = DockStyle.Fill,
        };
        _list.Columns.Add(Loc.T("backups.colTime"), 140);
        _list.Columns.Add(Loc.T("backups.colReason"), 220);
        _list.Columns.Add(Loc.T("backups.colConfig"), 360);

        foreach (var backup in controller.Backups.List(adapter.Id))
        {
            _list.Items.Add(new ListViewItem([
                backup.CreatedAt.LocalDateTime.ToString("g"),
                backup.Reason ?? string.Empty,
                backup.Configuration.Summary,
            ]) { Tag = backup, ToolTipText = backup.FilePath });
        }

        _restore = new Button { Text = Loc.T("backups.restore"), AutoSize = true, Enabled = false };
        var openFolder = new Button { Text = Loc.T("backups.openFolder"), AutoSize = true };
        var close = new Button { Text = Loc.T("common.close"), AutoSize = true, DialogResult = DialogResult.Cancel, MinimumSize = new Size(88, 0) };
        CancelButton = close;

        _restore.Click += (_, _) => Choose();
        _list.DoubleClick += (_, _) => Choose();
        _list.SelectedIndexChanged += (_, _) => _restore.Enabled = _list.SelectedItems.Count == 1;
        openFolder.Click += (_, _) => Dialogs.OpenFolder(controller.Backups.Directory);

        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Bottom, Padding = new Padding(0, 8, 0, 0) };
        buttons.Controls.AddRange([close, _restore, openFolder]);

        if (_list.Items.Count == 0)
        {
            info.Text = Loc.T("backups.empty");
        }
        else
        {
            _list.Items[0].Selected = true;
        }

        Controls.Add(_list);
        Controls.Add(info);
        Controls.Add(buttons);
        Modern.Theme.Apply(this);
        ResumeLayout(false);
        PerformLayout();
    }

    public ConfigurationBackup? SelectedBackup { get; private set; }

    private void Choose()
    {
        if (_list.SelectedItems.Count != 1)
        {
            return;
        }

        SelectedBackup = (ConfigurationBackup)_list.SelectedItems[0].Tag!;
        DialogResult = DialogResult.OK;
        Close();
    }
}
