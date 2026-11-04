using System.Media;
using IPSwitch.App.UI.Modern;
using IPSwitch.Core.Localization;

namespace IPSwitch.App.UI;

/// <summary>
/// Meldungsfenster im Farbschema der Anwendung (hell/dunkel). Ersetzt MessageBox, die Windows immer hell zeichnet;
/// Schaltflächen in der Oberflächensprache, Strg+C kopiert den Text wie bei MessageBox.
/// </summary>
internal sealed class MessageDialog : Form
{
    private readonly string _message;
    private readonly MessageBoxIcon _icon;

    public MessageDialog(string message, MessageBoxButtons buttons, MessageBoxIcon icon)
    {
        _message = message;
        _icon = icon;
        var p = Theme.Current;

        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 9.75f);
        Text = Dialogs.Title;
        Icon = Infrastructure.AppIcons.Window;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        KeyPreview = true;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        MinimumSize = new Size(360, 0);

        var (glyph, color) = icon switch
        {
            MessageBoxIcon.Error => (Glyphs.Error, p.Danger),
            MessageBoxIcon.Warning => (Glyphs.Warning, p.Warning),
            MessageBoxIcon.Question => (Glyphs.Question, p.Accent),
            _ => (Glyphs.Info, p.Accent),
        };
        var symbol = new GlyphLabel(glyph, 22f, color) { Margin = new Padding(0, 0, 16, 0) };
        var text = new Label
        {
            Text = message,
            AutoSize = true,
            MaximumSize = new Size(480, 0),
            UseMnemonic = false,
            Margin = new Padding(0, 6, 0, 0),
        };

        var content = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, RowCount = 1, Dock = DockStyle.Fill, Padding = new Padding(24, 22, 24, 22), Margin = Padding.Empty, BackColor = p.Card };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        content.Controls.Add(symbol, 0, 0);
        content.Controls.Add(text, 1, 0);

        // Schaltflächenleiste wie bei Windows-11-Dialogen dunkler abgesetzt; die erste Schaltfläche ist die Standardaktion.
        var footer = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Dock = DockStyle.Top,
            WrapContents = false,
            BackColor = p.Background,
            Padding = new Padding(16, 14, 16, 14),
            Margin = Padding.Empty,
        };
        var definitions = Definitions(buttons);
        for (var i = definitions.Length - 1; i >= 0; i--)
        {
            var (label, result) = definitions[i];
            var button = new ModernButton
            {
                Text = label,
                DialogResult = result,
                Kind = i == 0 ? ButtonKind.Primary : ButtonKind.Secondary,
                MinimumWidth = 96,
                Margin = new Padding(8, 0, 0, 0),
            };
            footer.Controls.Add(button);
            if (i == 0)
            {
                AcceptButton = button;
            }
        }

        // Escape: "Nein"/"Abbrechen" bzw. "OK", wenn es nur eine Schaltfläche gibt.
        CancelButton = footer.Controls.OfType<ModernButton>().FirstOrDefault(b => b.DialogResult is DialogResult.Cancel)
                       ?? footer.Controls.OfType<ModernButton>().FirstOrDefault(b => b.DialogResult is DialogResult.No)
                       ?? (definitions.Length == 1 ? (IButtonControl?)AcceptButton : null);

        var layout = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, RowCount = 2, Dock = DockStyle.Fill, Margin = Padding.Empty, Padding = Padding.Empty };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.Controls.Add(content, 0, 0);
        layout.Controls.Add(footer, 0, 1);
        footer.Dock = DockStyle.Fill;
        Controls.Add(layout);

        Theme.Apply(this);
        ResumeLayout(false);
        PerformLayout();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        (_icon switch
        {
            MessageBoxIcon.Error => SystemSounds.Hand,
            MessageBoxIcon.Warning => SystemSounds.Exclamation,
            MessageBoxIcon.Question => SystemSounds.Question,
            _ => SystemSounds.Asterisk,
        }).Play();
        (AcceptButton as Control)?.Focus();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Control && e.KeyCode is Keys.C or Keys.Insert)
        {
            try
            {
                Clipboard.SetText(_message);
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                // Zwischenablage gerade von einem anderen Programm belegt.
            }

            e.Handled = true;
        }
    }

    private static (string Label, DialogResult Result)[] Definitions(MessageBoxButtons buttons) => buttons switch
    {
        MessageBoxButtons.YesNo => [(Loc.T("common.yes"), DialogResult.Yes), (Loc.T("common.no"), DialogResult.No)],
        MessageBoxButtons.YesNoCancel => [(Loc.T("common.yes"), DialogResult.Yes), (Loc.T("common.no"), DialogResult.No), (Loc.T("common.cancel"), DialogResult.Cancel)],
        MessageBoxButtons.OKCancel => [("OK", DialogResult.OK), (Loc.T("common.cancel"), DialogResult.Cancel)],
        _ => [("OK", DialogResult.OK)],
    };
}
