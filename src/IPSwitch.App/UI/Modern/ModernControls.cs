using IPSwitch.Core.Localization;
using System.Drawing.Drawing2D;

namespace IPSwitch.App.UI.Modern;

/// <summary>Karte mit abgerundeten Ecken. Kinder übernehmen die Kartenfarbe als Hintergrund.</summary>
internal class CardPanel : Panel
{
    public CardPanel()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Current.Card;
        Padding = new Padding(20);
    }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]

    public int Radius { get; set; } = 8;

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]

    public bool ShowBorder { get; set; } = true;

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.Current.Background);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var radius = LogicalToDeviceUnits(Radius);
        using var path = Theme.RoundedRect(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), radius);
        using var fill = new SolidBrush(BackColor);
        g.FillPath(fill, path);
        if (ShowBorder)
        {
            using var pen = new Pen(Theme.Current.Border);
            g.DrawPath(pen, path);
        }
    }
}

internal enum ButtonKind
{
    Secondary,
    Primary,
    Success,
    Warning,
    Danger,
    Ghost,

    /// <summary>"Profil jetzt anwenden" (Himmelblau).</summary>
    Apply,

    /// <summary>"DHCP aktivieren" (Bordeaux).</summary>
    Dhcp,
}

/// <summary>
/// Abgerundete Schaltfläche mit optionalem Symbol. Erbt von <see cref="Button"/>, damit Tastatur, AcceptButton
/// und Barrierefreiheit wie gewohnt funktionieren – nur das Zeichnen ist eigen.
/// </summary>
internal sealed class ModernButton : Button
{
    private bool _hover;
    private bool _pressed;

    public ModernButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Cursor = Cursors.Hand;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Margin = new Padding(4, 0, 0, 0);
    }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]

    public ButtonKind Kind { get; set; } = ButtonKind.Secondary;

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]

    public string? Glyph { get; set; }

    /// <summary>Mindesthöhe in logischen Pixeln (96 dpi).</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int MinimumHeight { get; set; } = 34;

    public override Size GetPreferredSize(Size proposedSize)
    {
        var height = LogicalToDeviceUnits(MinimumHeight);
        var padding = LogicalToDeviceUnits(string.IsNullOrEmpty(Text) ? 0 : 14);
        var width = padding * 2;
        if (!string.IsNullOrEmpty(Glyph))
        {
            width += GlyphSize();
        }

        if (!string.IsNullOrEmpty(Text))
        {
            width += TextRenderer.MeasureText(Text, Font).Width + (string.IsNullOrEmpty(Glyph) ? 0 : LogicalToDeviceUnits(8));
        }

        return new Size(Math.Max(width, height), height);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hover = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = _pressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs mevent)
    {
        _pressed = mevent.Button == MouseButtons.Left;
        Invalidate();
        base.OnMouseDown(mevent);
    }

    protected override void OnMouseUp(MouseEventArgs mevent)
    {
        _pressed = false;
        Invalidate();
        base.OnMouseUp(mevent);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        Invalidate();
        base.OnEnabledChanged(e);
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        var g = pevent.Graphics;
        var p = Theme.Current;
        var parentBack = Parent?.BackColor ?? p.Background;
        g.Clear(parentBack);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var (back, fore, border) = ResolveColors(p, parentBack);
        var radius = LogicalToDeviceUnits(6);
        using (var path = Theme.RoundedRect(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), radius))
        {
            using var fill = new SolidBrush(back);
            g.FillPath(fill, path);
            if (border is { } b)
            {
                using var pen = new Pen(b);
                g.DrawPath(pen, path);
            }

            if (Focused && ShowFocusCues)
            {
                using var focus = new Pen(p.Text, 1.5f);
                using var inner = Theme.RoundedRect(new RectangleF(2f, 2f, Width - 4.5f, Height - 4.5f), Math.Max(1, radius - 2));
                g.DrawPath(focus, inner);
            }
        }

        var glyphWidth = string.IsNullOrEmpty(Glyph) ? 0 : GlyphSize();
        var textWidth = string.IsNullOrEmpty(Text) ? 0 : TextRenderer.MeasureText(Text, Font).Width;
        var gap = glyphWidth > 0 && textWidth > 0 ? LogicalToDeviceUnits(8) : 0;
        var available = Width - LogicalToDeviceUnits(16);
        if (textWidth > 0 && glyphWidth + gap + textWidth > available)
        {
            // Zu schmal: zuerst auf das Symbol verzichten, dann den Text mit "…" kürzen.
            glyphWidth = gap = 0;
            textWidth = Math.Min(textWidth, Math.Max(0, available));
        }

        var x = (Width - (glyphWidth + gap + textWidth)) / 2;

        if (glyphWidth > 0)
        {
            using var glyphFont = new Font(Theme.GlyphFamily, Font.SizeInPoints * 1.05f, GraphicsUnit.Point);
            TextRenderer.DrawText(g, Glyph, glyphFont, new Rectangle(x, 0, glyphWidth, Height), fore,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        if (textWidth > 0)
        {
            TextRenderer.DrawText(g, Text, Font, new Rectangle(x + glyphWidth + gap, 0, textWidth + 2, Height), fore,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        }
    }

    private int GlyphSize() => LogicalToDeviceUnits((int)Math.Ceiling(Font.SizeInPoints * 1.6));

    private (Color Back, Color Fore, Color? Border) ResolveColors(Palette p, Color parentBack)
    {
        var (normal, hover, fore, border) = Kind switch
        {
            ButtonKind.Primary => (p.Accent, p.AccentHover, p.OnAccent, (Color?)null),
            ButtonKind.Success => (p.Success, p.SuccessHover, p.OnSuccess, null),
            ButtonKind.Warning => (p.Warning, p.WarningHover, p.OnWarning, null),
            ButtonKind.Danger => (p.Danger, p.DangerHover, Color.White, null),
            ButtonKind.Ghost => (parentBack, p.Subtle, p.Text, null),
            ButtonKind.Apply => (p.Apply, p.ApplyHover, p.OnApply, null),
            ButtonKind.Dhcp => (p.Dhcp, p.DhcpHover, p.OnDhcp, null),
            _ => (p.Subtle, p.SubtleHover, p.Text, p.Border),
        };

        if (!Enabled)
        {
            var disabledBack = Kind == ButtonKind.Ghost ? parentBack : Theme.Blend(normal, parentBack, 0.55);
            return (disabledBack, Theme.Blend(p.TextSecondary, parentBack, 0.35), border);
        }

        var back = _pressed ? Theme.Blend(hover, Color.Black, 0.12) : _hover ? hover : normal;
        return (back, fore, border);
    }
}

/// <summary>Einzelnes Symbol als Label.</summary>
internal sealed class GlyphLabel : Label
{
    public GlyphLabel(string glyph, float size, Color color)
    {
        Text = glyph;
        Font = new Font(Theme.GlyphFamily, size, GraphicsUnit.Point);
        ForeColor = color;
        AutoSize = true;
        UseMnemonic = false;
        Margin = new Padding(0, 0, 8, 0);
    }
}

/// <summary>Abgerundetes Suchfeld mit Lupe und Platzhaltertext.</summary>
internal sealed class SearchBox : CardPanel
{
    public SearchBox()
    {
        BackColor = Theme.Current.Input;
        Padding = new Padding(10, 9, 10, 5);
        Height = 36;
        Radius = 6;

        var icon = new GlyphLabel(Glyphs.Search, 9.5f, Theme.Current.TextSecondary) { Dock = DockStyle.Left, Margin = Padding.Empty, Padding = new Padding(0, 1, 6, 0) };
        Box = new TextBox
        {
            BorderStyle = BorderStyle.None,
            BackColor = Theme.Current.Input,
            ForeColor = Theme.Current.Text,
            PlaceholderText = Loc.T("main.searchPlaceholder"),
            Dock = DockStyle.Fill,
        };
        Controls.Add(Box);
        Controls.Add(icon);
    }

    public TextBox Box { get; }
}
