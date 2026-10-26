using IPSwitch.Core.Localization;
using System.ComponentModel;
using System.Diagnostics;
using IPSwitch.App.Infrastructure;
using IPSwitch.App.UI.Modern;
using IPSwitch.Core.Diagnostics;
using IPSwitch.Core.Storage;

namespace IPSwitch.App.UI;

/// <summary>
/// Eingebaute Hilfe: Themenliste links, formatierter Inhalt rechts. Es gibt höchstens ein Hilfefenster;
/// ein weiterer Aufruf holt es nach vorn und springt zum gewünschten Thema.
/// </summary>
internal sealed class HelpForm : Form
{
    private static HelpForm? _instance;

    private readonly Palette _p = Theme.Current;
    private readonly IReadOnlyList<HelpTopic> _topics;
    private readonly ListBox _list;
    private readonly RichTextBox _text;
    private readonly Font _titleFont = new("Segoe UI Semibold", 17f);
    private readonly Font _headingFont = new("Segoe UI Semibold", 11.5f);
    private readonly Font _bodyFont = new("Segoe UI", 10.25f);
    private readonly Font _boldFont = new("Segoe UI Semibold", 10.25f);
    private readonly Font _topicFont = new("Segoe UI", 10f);
    private readonly Font _glyphFont = new(Theme.GlyphFamily, 11f, GraphicsUnit.Point);

    private HelpForm()
    {
        _topics = HelpContent.Topics(AppPaths.Default.DataDirectory);

        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 9.75f);
        Text = Loc.T("help.title");
        Icon = AppIcons.Window;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(980, 680);
        MinimumSize = new Size(720, 480);
        BackColor = _p.Background;
        ForeColor = _p.Text;
        Padding = new Padding(20, 18, 20, 16);
        KeyPreview = true;
        Theme.ApplyTitleBar(this);

        // Kein Rechts-nach-links-Layout: Hilfeinhalte gibt es nur auf Deutsch und Englisch.

        _list = new ListBox
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            DrawMode = DrawMode.OwnerDrawFixed,
            IntegralHeight = false,
            BackColor = _p.Card,
            ForeColor = _p.Text,
            ItemHeight = 34,
        };
        _list.DrawItem += DrawTopic;
        _list.SelectedIndexChanged += (_, _) => RenderSelected();
        foreach (var topic in _topics)
        {
            _list.Items.Add(topic);
        }

        Theme.ApplyNativeTheme(_list);

        var listCard = new CardPanel { Dock = DockStyle.Left, Width = 280, Padding = new Padding(8) };
        listCard.Controls.Add(_list);

        _text = new RichTextBox
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            ReadOnly = true,
            BackColor = _p.Card,
            ForeColor = _p.Text,
            DetectUrls = false,
            TabStop = false,
            ScrollBars = RichTextBoxScrollBars.Vertical,
            ShortcutsEnabled = true,

            // Keine automatische Schriftumschaltung: sonst übernimmt Text nach Sonderzeichen wie "⋮" eine falsche Schrift.
            LanguageOption = 0,
        };
        Theme.ApplyNativeTheme(_text);
        var textCard = new CardPanel { Dock = DockStyle.Fill, Padding = new Padding(28, 22, 16, 16) };
        textCard.Controls.Add(_text);
        var spacer = new Panel { Dock = DockStyle.Left, Width = 16 };

        // Fußzeile: Programm und Autor, Handbuch, Schließen
        var footerText = new Label
        {
            Text = $"{AppInfo.Name} {AppInfo.Version}  ·  {Loc.T("about.developedBy", AppInfo.Author)}",
            AutoSize = true,
            ForeColor = _p.TextSecondary,
            Anchor = AnchorStyles.Left,
            Margin = Padding.Empty,
        };
        var manual = new ModernButton { Text = Loc.T("help.openManual"), Glyph = Glyphs.Book, Margin = new Padding(8, 0, 0, 0) };
        manual.Click += (_, _) => OpenManual();
        manual.Visible = File.Exists(ManualPath);
        var close = new ModernButton { Text = Loc.T("common.close"), Kind = ButtonKind.Primary, Margin = new Padding(8, 0, 0, 0) };
        close.Click += (_, _) => Close();
        CancelButton = close;

        var footer = new TableLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, ColumnCount = 3, Padding = new Padding(0, 14, 0, 0) };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.Controls.Add(footerText, 0, 0);
        footer.Controls.Add(manual, 1, 0);
        footer.Controls.Add(close, 2, 0);

        Controls.Add(textCard);
        Controls.Add(spacer);
        Controls.Add(listCard);
        Controls.Add(footer);
        ResumeLayout(false);
        PerformLayout();

        _list.SelectedIndex = 0;
    }

    /// <summary>Pfad des mitgelieferten Benutzerhandbuchs.</summary>
    public static string ManualPath => Path.Combine(AppContext.BaseDirectory, "docs",
        HelpContent.UseGerman ? "BENUTZERHANDBUCH.md" : "USER_MANUAL.md");

    /// <summary>Schließt ein offenes Hilfefenster (z. B. nach einem Sprach- oder Designwechsel).</summary>
    public static void CloseIfOpen()
    {
        if (_instance is { IsDisposed: false })
        {
            _instance.Close();
        }
    }

    /// <summary>Öffnet die Hilfe (bzw. holt sie nach vorn) und zeigt optional ein bestimmtes Thema.</summary>
    public static void ShowTopic(string? topicId)
    {
        if (_instance is null || _instance.IsDisposed)
        {
            _instance = new HelpForm();
            _instance.FormClosed += (_, _) => _instance = null;
            _instance.Show();
        }
        else if (_instance.WindowState == FormWindowState.Minimized)
        {
            _instance.WindowState = FormWindowState.Normal;
        }

        if (topicId is not null)
        {
            _instance.SelectTopic(topicId);
        }

        _instance.TopMost = true;
        _instance.Activate();
        _instance.TopMost = false;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode is Keys.Up or Keys.Down && !_list.Focused)
        {
            _list.Focus();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _titleFont.Dispose();
            _headingFont.Dispose();
            _bodyFont.Dispose();
            _boldFont.Dispose();
            _topicFont.Dispose();
            _glyphFont.Dispose();
        }

        base.Dispose(disposing);
    }

    private void SelectTopic(string id)
    {
        for (var i = 0; i < _topics.Count; i++)
        {
            if (_topics[i].Id == id)
            {
                _list.SelectedIndex = i;
                return;
            }
        }
    }

    private void DrawTopic(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || _list.Items[e.Index] is not HelpTopic topic)
        {
            return;
        }

        var selected = (e.State & DrawItemState.Selected) != 0;
        using (var back = new SolidBrush(selected ? _p.CardSelected : _p.Card))
        {
            e.Graphics.FillRectangle(back, e.Bounds);
        }

        if (selected)
        {
            using var accent = new SolidBrush(_p.Accent);
            e.Graphics.FillRectangle(accent, e.Bounds.X, e.Bounds.Y + 7, 3, e.Bounds.Height - 14);
        }

        var glyphRect = new Rectangle(e.Bounds.X + 12, e.Bounds.Y, 22, e.Bounds.Height);
        TextRenderer.DrawText(e.Graphics, topic.Glyph, _glyphFont, glyphRect, selected ? _p.Accent : _p.TextSecondary,
            TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
        var textRect = new Rectangle(e.Bounds.X + 42, e.Bounds.Y, e.Bounds.Width - 46, e.Bounds.Height);
        TextRenderer.DrawText(e.Graphics, topic.Title, _topicFont, textRect, _p.Text,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }

    private void RenderSelected()
    {
        if (_list.SelectedItem is not HelpTopic topic)
        {
            return;
        }

        _text.SuspendLayout();
        _text.Clear();
        Append(topic.Title + "\n", _titleFont, _p.Text);
        Append("\n", _bodyFont, _p.Text);

        foreach (var block in topic.Blocks)
        {
            switch (block)
            {
                case HelpHeading heading:
                    Append("\n" + heading.Text + "\n", _headingFont, _p.Text);
                    break;
                case HelpParagraph paragraph:
                    Append(paragraph.Text + "\n\n", _bodyFont, _p.Text);
                    break;
                case HelpBullets bullets:
                    var numbered = bullets.Items.All(i => i.Length > 0 && char.IsDigit(i[0]));
                    foreach (var item in bullets.Items)
                    {
                        Indent(left: 14, hanging: numbered ? 18 : 14);
                        Append((numbered ? string.Empty : "•\t") + item + "\n", _bodyFont, _p.Text);
                    }

                    Indent(0, 0);
                    Append("\n", _bodyFont, _p.Text);
                    break;
                case HelpTable table:
                    foreach (var (key, value) in table.Rows)
                    {
                        Indent(left: 0, hanging: 230, tab: 230);
                        Append(key, _boldFont, _p.Text);
                        Append("\t" + value + "\n", _bodyFont, _p.TextSecondary);
                    }

                    Indent(0, 0);
                    Append("\n", _bodyFont, _p.Text);
                    break;
            }
        }

        _text.Select(0, 0);
        _text.ResumeLayout();
    }

    private void Append(string text, Font font, Color color)
    {
        _text.SelectionStart = _text.TextLength;
        _text.SelectionLength = 0;
        _text.SelectionFont = font;
        _text.SelectionColor = color;
        _text.AppendText(text);
    }

    private void Indent(int left, int hanging, int tab = 0)
    {
        _text.SelectionStart = _text.TextLength;
        _text.SelectionLength = 0;
        _text.SelectionIndent = LogicalToDeviceUnits(left);
        _text.SelectionHangingIndent = LogicalToDeviceUnits(hanging);
        _text.SelectionTabs = tab > 0 ? [LogicalToDeviceUnits(tab)] : hanging > 0 ? [LogicalToDeviceUnits(hanging)] : [];
    }

    private void OpenManual()
    {
        try
        {
            Process.Start(new ProcessStartInfo(ManualPath) { UseShellExecute = true });
        }
        catch (Win32Exception)
        {
            // Keine Anwendung für .md registriert: mit dem Editor öffnen.
            try
            {
                Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "notepad.exe")) { ArgumentList = { ManualPath } });
            }
            catch (Exception ex)
            {
                Log.Warn("Handbuch konnte nicht geöffnet werden: " + ex.Message);
                Dialogs.Error(this, Loc.T("help.manualFailed", ex.Message));
            }
        }
    }
}
