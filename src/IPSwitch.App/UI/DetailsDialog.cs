using IPSwitch.Core.Localization;
namespace IPSwitch.App.UI;

/// <summary>Meldung mit ausklappbaren technischen Details (z. B. ausgeführte netsh-Befehle).</summary>
internal sealed class DetailsDialog : Form
{
    private readonly TextBox _details;
    private readonly Button _toggle;

    public DetailsDialog(string message, string details, MessageBoxIcon icon, bool expanded = false)
    {
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = SystemFonts.MessageBoxFont;
        Text = Dialogs.Title;
        Icon = Infrastructure.AppIcons.Window;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12);

        var picture = new PictureBox
        {
            Image = (icon switch
            {
                MessageBoxIcon.Error => SystemIcons.Error,
                MessageBoxIcon.Warning => SystemIcons.Warning,
                _ => SystemIcons.Information,
            }).ToBitmap(),
            SizeMode = PictureBoxSizeMode.AutoSize,
            Margin = new Padding(0, 0, 12, 0),
        };

        var text = new Label
        {
            Text = message,
            AutoSize = true,
            MaximumSize = new Size(520, 0),
            Margin = new Padding(0, 4, 0, 12),
        };

        _details = new TextBox
        {
            Text = details,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Font = new Font(FontFamily.GenericMonospace, 8.5f),
            Size = new Size(600, 200),
            Visible = false,
            Margin = new Padding(0, 0, 0, 8),
        };

        _toggle = new Button { Text = Loc.T("details.show"), AutoSize = true };
        _toggle.Click += (_, _) =>
        {
            _details.Visible = !_details.Visible;
            _toggle.Text = Loc.T(_details.Visible ? "details.hide" : "details.show");
        };

        var copy = new Button { Text = Loc.T("details.copy"), AutoSize = true };
        copy.Click += (_, _) => Clipboard.SetText(message + Environment.NewLine + Environment.NewLine + details);

        var ok = new Button { Text = "OK", AutoSize = true, DialogResult = DialogResult.OK, MinimumSize = new Size(88, 0) };
        AcceptButton = ok;
        CancelButton = ok;

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
        };
        buttons.Controls.AddRange([ok, copy, _toggle]);

        var layout = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, RowCount = 3, Dock = DockStyle.Fill };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.Controls.Add(picture, 0, 0);
        layout.Controls.Add(text, 1, 0);
        layout.Controls.Add(_details, 0, 1);
        layout.SetColumnSpan(_details, 2);
        layout.Controls.Add(buttons, 0, 2);
        layout.SetColumnSpan(buttons, 2);
        Controls.Add(layout);
        if (expanded)
        {
            _details.Visible = true;
            _details.Size = new Size(640, 380);
            _toggle.Text = Loc.T("details.hide");
        }

        Modern.Theme.Apply(this);
        ResumeLayout(false);
        PerformLayout();
    }
}
