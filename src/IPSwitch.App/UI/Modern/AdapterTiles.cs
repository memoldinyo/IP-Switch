using IPSwitch.Core.Localization;
using System.ComponentModel;
using System.Drawing.Drawing2D;
using IPSwitch.Core.Models;

namespace IPSwitch.App.UI.Modern;

/// <summary>
/// Adapter als Kacheln in einer Zeile. Was nicht mehr hineinpasst, steckt hinter der Kachel "+ N weitere".
/// Ein Klick wählt den Adapter als Ziel aus.
/// </summary>
internal sealed class AdapterTiles : Control
{
    private readonly List<(Rectangle Bounds, AdapterInfo? Adapter)> _tiles = [];
    private readonly List<AdapterInfo> _overflow = [];
    private readonly Font _nameFont;
    private readonly Font _monoFont;
    private readonly Font _smallFont;
    private readonly Font _glyphFont;
    private IReadOnlyList<AdapterInfo> _adapters = [];
    private string? _selectedId;
    private int _hover = -1;

    public AdapterTiles()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Current.Background;
        _nameFont = new Font("Segoe UI", 9.75f, FontStyle.Bold);
        _monoFont = new Font("Consolas", 9f);
        _smallFont = new Font("Segoe UI", 8.75f);
        _glyphFont = new Font(Theme.GlyphFamily, 11f, GraphicsUnit.Point);
    }

    /// <summary>Kachel angeklickt.</summary>
    public event EventHandler<AdapterInfo>? AdapterSelected;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? SelectedId
    {
        get => _selectedId;
        set
        {
            _selectedId = value;
            LayoutTiles();
            Invalidate();
        }
    }

    public void SetAdapters(IReadOnlyList<AdapterInfo> adapters, string? selectedId)
    {
        _adapters = adapters;
        _selectedId = selectedId;
        LayoutTiles();
        Invalidate();
    }

    protected override Size DefaultSize => new(600, 78);

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        LayoutTiles();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        for (var i = 0; i < _tiles.Count; i++)
        {
            var (bounds, adapter) = _tiles[i];
            if (adapter is null)
            {
                DrawOverflow(g, bounds, i == _hover);
            }
            else
            {
                DrawTile(g, bounds, adapter, adapter.Id == _selectedId, i == _hover);
            }
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var index = _tiles.FindIndex(t => t.Bounds.Contains(e.Location));
        if (index != _hover)
        {
            _hover = index;
            Cursor = index >= 0 ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = -1;
        Invalidate();
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        var index = _tiles.FindIndex(t => t.Bounds.Contains(e.Location));
        if (index < 0)
        {
            return;
        }

        var (bounds, adapter) = _tiles[index];
        if (adapter is not null)
        {
            AdapterSelected?.Invoke(this, adapter);
            return;
        }

        // "+ N weitere": restliche Adapter als Menü anbieten.
        var menu = new ContextMenuStrip { Renderer = new ModernMenuRenderer(), Font = Font, ShowImageMargin = true };
        foreach (var other in _overflow)
        {
            var item = new ToolStripMenuItem($"{other.Name}   {other.PrimaryAddressText} ({other.ModeText})")
            {
                Tag = Glyphs.ForAdapter(other),
                ToolTipText = $"{other.Description} – {other.StatusText}",
            };
            item.Click += (_, _) => AdapterSelected?.Invoke(this, other);
            menu.Items.Add(item);
        }

        menu.Closed += (_, _) => BeginInvoke(menu.Dispose);
        menu.Show(this, new Point(bounds.Left, bounds.Bottom + 4));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _nameFont.Dispose();
            _monoFont.Dispose();
            _smallFont.Dispose();
            _glyphFont.Dispose();
        }

        base.Dispose(disposing);
    }

    private void LayoutTiles()
    {
        _tiles.Clear();
        _overflow.Clear();
        if (_adapters.Count == 0 || Width <= 0)
        {
            return;
        }

        var gap = LogicalToDeviceUnits(8);
        var minWidth = LogicalToDeviceUnits(165);
        var maxWidth = LogicalToDeviceUnits(230);
        var fit = Math.Max(1, (Width + gap) / (minWidth + gap));

        List<AdapterInfo> shown;
        if (_adapters.Count <= fit)
        {
            shown = [.. _adapters];
        }
        else
        {
            shown = _adapters.Take(fit - 1).ToList();
            // Der ausgewählte Adapter bleibt immer sichtbar.
            if (_selectedId is not null && shown.All(a => a.Id != _selectedId) && _adapters.FirstOrDefault(a => a.Id == _selectedId) is { } selected && shown.Count > 0)
            {
                shown[^1] = selected;
            }

            _overflow.AddRange(_adapters.Where(a => !shown.Contains(a)));
        }

        var slots = shown.Count + (_overflow.Count > 0 ? 1 : 0);
        var width = Math.Min(maxWidth, (Width - (gap * (slots - 1))) / slots);
        var x = 0;
        foreach (var adapter in shown)
        {
            _tiles.Add((new Rectangle(x, 0, width, Height - 1), adapter));
            x += width + gap;
        }

        if (_overflow.Count > 0)
        {
            _tiles.Add((new Rectangle(x, 0, width, Height - 1), null));
        }
    }

    private void DrawTile(Graphics g, Rectangle bounds, AdapterInfo adapter, bool selected, bool hover)
    {
        var p = Theme.Current;
        var radius = LogicalToDeviceUnits(8);
        using (var path = Theme.RoundedRect(new RectangleF(bounds.X + 1, bounds.Y + 1, bounds.Width - 2, bounds.Height - 2), radius))
        {
            // Auf der Karte liegend: abgesetzter Hintergrund wie die Konfigurationskarte rechts.
            using var fill = new SolidBrush(selected ? p.CardSelected : hover ? p.CardHover : p.Inner);
            using var border = new Pen(selected ? p.Accent : p.Border, selected ? 2f : 1f);
            g.FillPath(fill, path);
            g.DrawPath(border, path);
        }

        var pad = LogicalToDeviceUnits(12);
        var x = bounds.X + pad;
        var right = bounds.Right - pad;
        var y = bounds.Y + LogicalToDeviceUnits(9);
        var line = TextRenderer.MeasureText("Ag", _nameFont).Height;

        var glyphWidth = LogicalToDeviceUnits(20);
        TextRenderer.DrawText(g, Glyphs.ForAdapter(adapter), _glyphFont, new Rectangle(x, y, glyphWidth, line),
            adapter.IsConnected ? p.Accent : p.TextSecondary, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding);

        var dot = LogicalToDeviceUnits(8);
        using (var dotBrush = new SolidBrush(adapter.IsConnected ? p.Connected : p.Disconnected))
        {
            g.FillEllipse(dotBrush, right - dot, y + ((line - dot) / 2f), dot, dot);
        }

        TextRenderer.DrawText(g, adapter.Name, _nameFont, new Rectangle(x + glyphWidth + 4, y, right - dot - x - glyphWidth - 10, line), p.Text,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        y += line + LogicalToDeviceUnits(4);

        var mono = TextRenderer.MeasureText("Ag", _monoFont).Height;
        TextRenderer.DrawText(g, adapter.PrimaryAddressText, _monoFont, new Rectangle(x, y, right - x, mono), p.Text,
            TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        y += mono + LogicalToDeviceUnits(2);

        TextRenderer.DrawText(g, $"{adapter.ModeText} · {adapter.StatusText}", _smallFont,
            new Rectangle(x, y, right - x, TextRenderer.MeasureText("Ag", _smallFont).Height), p.TextSecondary,
            TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }

    private void DrawOverflow(Graphics g, Rectangle bounds, bool hover)
    {
        var p = Theme.Current;
        using (var path = Theme.RoundedRect(new RectangleF(bounds.X + 1, bounds.Y + 1, bounds.Width - 2, bounds.Height - 2), LogicalToDeviceUnits(8)))
        {
            if (hover)
            {
                using var fill = new SolidBrush(p.CardHover);
                g.FillPath(fill, path);
            }

            using var pen = new Pen(p.TextSecondary) { DashStyle = DashStyle.Dash };
            g.DrawPath(pen, path);
        }

        var line = TextRenderer.MeasureText("Ag", _nameFont).Height;
        var middle = bounds.Y + (bounds.Height / 2);
        TextRenderer.DrawText(g, Loc.T("tiles.more", _overflow.Count), _nameFont, new Rectangle(bounds.X, middle - line, bounds.Width, line), p.Text,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.Bottom);
        var subtitle = Loc.T(_overflow.All(a => !a.IsConnected) ? "tiles.moreDisconnected" : "tiles.moreClick");
        TextRenderer.DrawText(g, subtitle, _smallFont, new Rectangle(bounds.X, middle + 2, bounds.Width, line), p.TextSecondary,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.Top);
    }
}
