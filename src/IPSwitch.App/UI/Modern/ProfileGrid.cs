using System.ComponentModel;
using System.Drawing.Drawing2D;
using IPSwitch.Core.Localization;
using IPSwitch.Core.Models;

namespace IPSwitch.App.UI.Modern;

/// <summary>
/// Profile als Kacheln (ab ausreichender Breite zweispaltig). Jede Kachel hat rechts unten ein ⋮-Menü.
/// Selbst gezeichnet, mit Bildlauf, Hover, Auswahl und Tastaturbedienung (Pfeiltasten, Enter, Kontextmenütaste).
/// </summary>
internal sealed class ProfileGrid : ScrollableControl
{
    private readonly List<IpProfile> _items = [];
    private readonly List<Rectangle> _cards = [];
    private readonly Font _nameFont;
    private readonly Font _smallFont;
    private readonly Font _monoFont;
    private readonly Font _glyphFont;
    private int _selected = -1;
    private int _hover = -1;
    private bool _hoverMenu;
    private int _columns = 1;

    public ProfileGrid()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        AutoScroll = true;
        TabStop = true;
        BackColor = Theme.Current.Background;
        ForeColor = Theme.Current.Text;
        _nameFont = new Font("Segoe UI", 11.5f, FontStyle.Bold);
        _smallFont = new Font("Segoe UI", 9f);
        _monoFont = new Font("Consolas", 9f);
        _glyphFont = new Font(Theme.GlyphFamily, 14f, GraphicsUnit.Point);
        Theme.ApplyNativeTheme(this);
    }

    /// <summary>Auswahl hat sich geändert.</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>Doppelklick oder Enter auf einer Kachel.</summary>
    public event EventHandler? ItemActivated;

    /// <summary>⋮, Rechtsklick oder Kontextmenütaste: Menü für das Profil an der Bildschirmposition anzeigen.</summary>
    public event EventHandler<ProfileMenuRequest>? MenuRequested;

    /// <summary>Liefert den Namen des zugeordneten Adapters (oder null).</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<IpProfile, string?>? AdapterName { get; set; }

    /// <summary>Text, wenn keine Kachel angezeigt wird.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string EmptyText { get; set; } = string.Empty;

    public IReadOnlyList<IpProfile> Items => _items;

    public IpProfile? SelectedProfile => _selected >= 0 && _selected < _items.Count ? _items[_selected] : null;

    public void SetItems(IEnumerable<IpProfile> items, Guid? selectId)
    {
        _items.Clear();
        _items.AddRange(items);
        _hover = -1;
        var index = selectId is { } id ? _items.FindIndex(p => p.Id == id) : -1;
        _selected = index >= 0 ? index : _items.Count > 0 ? 0 : -1;
        LayoutCards();
        Invalidate();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Select(Guid id)
    {
        var index = _items.FindIndex(p => p.Id == id);
        if (index >= 0)
        {
            SetSelected(index);
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        LayoutCards();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var p = Theme.Current;
        g.Clear(BackColor);

        if (_items.Count == 0)
        {
            TextRenderer.DrawText(g, EmptyText, Font, ClientRectangle, p.TextSecondary,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
            return;
        }

        g.TranslateTransform(AutoScrollPosition.X, AutoScrollPosition.Y);
        var visible = new Rectangle(-AutoScrollPosition.X, -AutoScrollPosition.Y, ClientSize.Width, ClientSize.Height);
        for (var i = 0; i < _items.Count; i++)
        {
            if (_cards[i].IntersectsWith(visible))
            {
                DrawCard(g, _cards[i], _items[i], i == _selected, i == _hover, i == _hover && _hoverMenu);
            }
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var (index, onMenu) = HitTest(e.Location);
        if (index != _hover || onMenu != _hoverMenu)
        {
            _hover = index;
            _hoverMenu = onMenu;
            Cursor = onMenu ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = -1;
        _hoverMenu = false;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        var (index, onMenu) = HitTest(e.Location);
        if (index < 0)
        {
            return;
        }

        SetSelected(index);
        if (e.Button == MouseButtons.Right)
        {
            RequestMenu(index, PointToScreen(e.Location));
        }
        else if (e.Button == MouseButtons.Left && onMenu)
        {
            var menu = MenuRect(Offset(_cards[index]));
            RequestMenu(index, PointToScreen(new Point(menu.Left, menu.Bottom + 2)));
        }
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        var (index, onMenu) = HitTest(e.Location);
        if (index >= 0 && !onMenu && e.Button == MouseButtons.Left)
        {
            ItemActivated?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override bool IsInputKey(Keys keyData) =>
        (keyData & Keys.KeyCode) is Keys.Up or Keys.Down or Keys.Left or Keys.Right || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_items.Count == 0)
        {
            return;
        }

        var current = Math.Max(0, _selected);
        var target = e.KeyCode switch
        {
            Keys.Left => current - 1,
            Keys.Right => current + 1,
            Keys.Up => current - _columns,
            Keys.Down => current + _columns,
            Keys.Home => 0,
            Keys.End => _items.Count - 1,
            _ => int.MinValue,
        };

        if (target != int.MinValue)
        {
            SetSelected(Math.Clamp(target, 0, _items.Count - 1));
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Enter && _selected >= 0)
        {
            ItemActivated?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
        else if ((e.KeyCode == Keys.Apps || (e.KeyCode == Keys.F10 && e.Shift)) && _selected >= 0)
        {
            var card = Offset(_cards[_selected]);
            RequestMenu(_selected, PointToScreen(new Point(card.Left + 24, card.Top + 24)));
            e.Handled = true;
        }
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _nameFont.Dispose();
            _smallFont.Dispose();
            _monoFont.Dispose();
            _glyphFont.Dispose();
        }

        base.Dispose(disposing);
    }

    private void SetSelected(int index)
    {
        if (index == _selected)
        {
            return;
        }

        _selected = index;
        EnsureVisible(index);
        Invalidate();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RequestMenu(int index, Point screen) =>
        MenuRequested?.Invoke(this, new ProfileMenuRequest(_items[index], screen));

    private void EnsureVisible(int index)
    {
        if (index < 0 || index >= _cards.Count)
        {
            return;
        }

        var card = _cards[index];
        var top = -AutoScrollPosition.Y;
        if (card.Top < top)
        {
            AutoScrollPosition = new Point(0, card.Top);
        }
        else if (card.Bottom > top + ClientSize.Height)
        {
            AutoScrollPosition = new Point(0, card.Bottom - ClientSize.Height);
        }
    }

    private Rectangle Offset(Rectangle card) => card with { X = card.X + AutoScrollPosition.X, Y = card.Y + AutoScrollPosition.Y };

    private (int Index, bool OnMenu) HitTest(Point client)
    {
        var point = new Point(client.X - AutoScrollPosition.X, client.Y - AutoScrollPosition.Y);
        for (var i = 0; i < _cards.Count; i++)
        {
            if (_cards[i].Contains(point))
            {
                return (i, MenuRect(_cards[i]).Contains(point));
            }
        }

        return (-1, false);
    }

    private Rectangle MenuRect(Rectangle card)
    {
        var size = LogicalToDeviceUnits(28);
        var inset = LogicalToDeviceUnits(8);
        return new Rectangle(card.Right - size - inset, card.Bottom - size - inset, size, size);
    }

    private int CardHeight(IpProfile profile) =>
        LogicalToDeviceUnits(string.IsNullOrWhiteSpace(profile.Description) ? 100 : 118);

    private void LayoutCards()
    {
        _cards.Clear();
        if (_items.Count == 0)
        {
            AutoScrollMinSize = Size.Empty;
            return;
        }

        var gap = LogicalToDeviceUnits(10);
        var width = ClientSize.Width;
        // Zwei Spalten erst, wenn jede Kachel breit genug für IP-Adresse samt Präfix ist.
        _columns = width >= LogicalToDeviceUnits(700) ? 2 : 1;
        var columnWidth = Math.Max(LogicalToDeviceUnits(200), (width - (gap * (_columns - 1))) / _columns);

        var y = 0;
        for (var row = 0; row * _columns < _items.Count; row++)
        {
            var rowItems = _items.Skip(row * _columns).Take(_columns).ToList();
            var height = rowItems.Max(CardHeight);
            for (var col = 0; col < rowItems.Count; col++)
            {
                _cards.Add(new Rectangle(col * (columnWidth + gap), y, columnWidth - 1, height));
            }

            y += height + gap;
        }

        AutoScrollMinSize = new Size(0, y);
    }

    private void DrawCard(Graphics g, Rectangle bounds, IpProfile profile, bool selected, bool hover, bool hoverMenu)
    {
        var p = Theme.Current;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var radius = LogicalToDeviceUnits(8);
        var card = new RectangleF(bounds.X + 0.5f, bounds.Y + 0.5f, bounds.Width - 1f, bounds.Height - 1f);
        using (var path = Theme.RoundedRect(card, radius))
        {
            using var fill = new SolidBrush(selected ? p.CardSelected : hover ? p.CardHover : p.Card);
            using var border = new Pen(selected ? p.Accent : p.Border, selected ? 1.5f : 1f);
            g.FillPath(fill, path);
            g.DrawPath(border, path);
        }

        if (selected)
        {
            var bar = LogicalToDeviceUnits(3);
            using var accentPath = Theme.RoundedRect(new RectangleF(card.X + bar, card.Y + (card.Height * 0.25f), bar, card.Height * 0.5f), bar / 2f);
            using var accent = new SolidBrush(p.Accent);
            g.FillPath(accent, accentPath);
        }

        if (selected && Focused && ShowFocusCues)
        {
            using var focus = new Pen(p.Text) { DashStyle = DashStyle.Dot };
            using var inner = Theme.RoundedRect(RectangleF.Inflate(card, -4, -4), Math.Max(1, radius - 3));
            g.DrawPath(focus, inner);
        }

        // Symbol im Kreis
        var iconSize = LogicalToDeviceUnits(34);
        var left = bounds.X + LogicalToDeviceUnits(12);
        var top = bounds.Y + LogicalToDeviceUnits(14);
        using (var circle = new SolidBrush(selected ? p.Card : p.Subtle))
        {
            g.FillEllipse(circle, left, top, iconSize, iconSize);
        }

        TextRenderer.DrawText(g, profile.UseDhcp ? Glyphs.Sync : Glyphs.Ethernet, _glyphFont, new Rectangle(left, top, iconSize, iconSize), p.Accent,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

        var x = left + iconSize + LogicalToDeviceUnits(10);
        var right = bounds.Right - LogicalToDeviceUnits(12);
        var y = top - LogicalToDeviceUnits(2);

        // Adapter-Chip rechts oben
        var chipLeft = right;
        if (AdapterName?.Invoke(profile) is { } adapter)
        {
            var chipText = "→ " + adapter;
            var size = TextRenderer.MeasureText(chipText, _smallFont);
            var chipWidth = Math.Min(size.Width + LogicalToDeviceUnits(14), Math.Max(LogicalToDeviceUnits(70), (right - x) / 2));
            var chipHeight = size.Height + LogicalToDeviceUnits(4);
            var chip = new Rectangle(right - chipWidth, y, chipWidth, chipHeight);
            using (var chipPath = Theme.RoundedRect(chip, chipHeight / 2f))
            using (var chipFill = new SolidBrush(selected ? p.Card : p.Subtle))
            {
                g.FillPath(chipFill, chipPath);
            }

            TextRenderer.DrawText(g, chipText, _smallFont, Rectangle.Inflate(chip, -LogicalToDeviceUnits(6), 0), p.TextSecondary,
                TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            chipLeft = chip.Left - LogicalToDeviceUnits(8);
        }

        var nameHeight = TextRenderer.MeasureText("Ag", _nameFont).Height;
        TextRenderer.DrawText(g, profile.Name, _nameFont, new Rectangle(x, y, chipLeft - x, nameHeight), p.Text,
            TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        y += nameHeight;

        if (!string.IsNullOrWhiteSpace(profile.Description))
        {
            var lineHeight = TextRenderer.MeasureText("Ag", _smallFont).Height;
            TextRenderer.DrawText(g, profile.Description.ReplaceLineEndings(" "), _smallFont, new Rectangle(x, y, right - x, lineHeight), p.TextSecondary,
                TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            y += lineHeight;
        }

        y += LogicalToDeviceUnits(4);

        // Werte zweispaltig: links IP und Gateway (wichtig, lang – werden nie gekürzt, solange Platz ist),
        // rechts Modus und DNS. Die zweite Zeile lässt rechts Platz für das ⋮-Menü.
        var menu = MenuRect(bounds);
        var address = AdapterText.ProfileAddress(profile);
        var gateway = profile.UseDhcp ? Loc.T("common.automatic") : profile.Gateway ?? "–";
        var column = Math.Max(MeasureMono(g, "IP " + address), MeasureMono(g, "GW " + gateway)) + LogicalToDeviceUnits(14);
        column = Math.Min(column, (int)((right - x) * 0.62));
        var monoHeight = TextRenderer.MeasureText("Ag", _monoFont).Height;

        // Rechte Schlüssel ("Modus", "DNS") gleich breit, damit die Werte untereinander stehen.
        var modeKey = Loc.T("grid.mode");
        var keyWidth = Math.Max(modeKey.Length, 3) + 1;
        DrawPair(g, "IP ", address, x, y, column);
        DrawPair(g, modeKey.PadRight(keyWidth), AdapterText.ProfileMode(profile), x + column, y, right - x - column);
        y += monoHeight;
        DrawPair(g, "GW ", gateway, x, y, column);
        DrawPair(g, "DNS".PadRight(keyWidth), AdapterText.ProfileDns(profile), x + column, y, menu.Left - LogicalToDeviceUnits(4) - x - column);

        // ⋮-Schaltfläche
        if (hoverMenu)
        {
            using var hoverPath = Theme.RoundedRect(menu, LogicalToDeviceUnits(4));
            using var hoverFill = new SolidBrush(p.SubtleHover);
            g.FillPath(hoverFill, hoverPath);
        }

        var dot = Math.Max(3, LogicalToDeviceUnits(3));
        var cx = menu.Left + (menu.Width / 2f) - (dot / 2f);
        var spacing = LogicalToDeviceUnits(5);
        using var dotBrush = new SolidBrush(hover || selected ? p.Text : p.TextSecondary);
        for (var i = -1; i <= 1; i++)
        {
            g.FillEllipse(dotBrush, cx, menu.Top + (menu.Height / 2f) - (dot / 2f) + (i * spacing), dot, dot);
        }
    }

    private int MeasureMono(Graphics g, string text) =>
        TextRenderer.MeasureText(g, text, _monoFont, Size.Empty, TextFormatFlags.NoPadding).Width;

    private void DrawPair(Graphics g, string key, string value, int x, int y, int width)
    {
        var p = Theme.Current;
        var keyWidth = TextRenderer.MeasureText(g, key, _monoFont, Size.Empty, TextFormatFlags.NoPadding).Width;
        var height = TextRenderer.MeasureText("Ag", _monoFont).Height;
        TextRenderer.DrawText(g, key, _monoFont, new Rectangle(x, y, keyWidth, height), p.TextSecondary, TextFormatFlags.NoPadding);
        TextRenderer.DrawText(g, value, _monoFont, new Rectangle(x + keyWidth, y, Math.Max(0, width - keyWidth - 4), height), p.Text,
            TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }
}

internal sealed class ProfileMenuRequest(IpProfile profile, Point screenLocation) : EventArgs
{
    public IpProfile Profile { get; } = profile;

    public Point ScreenLocation { get; } = screenLocation;
}
