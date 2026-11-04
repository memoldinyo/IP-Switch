namespace IPSwitch.App.UI.Modern;

/// <summary>
/// Kontextmenüs im Farbschema. Ein Symbol (Glyph) kann über <see cref="ToolStripItem.Tag"/> als string mitgegeben werden.
/// </summary>
internal sealed class ModernMenuRenderer : ToolStripProfessionalRenderer
{
    public ModernMenuRenderer()
        : base(new MenuColors(Theme.Current)) => RoundedEdges = false;

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        var p = Theme.Current;
        e.TextColor = e.Item.Enabled ? p.Text : p.TextSecondary;
        if (e.Item.Tag is string glyph && e.ToolStrip is ToolStripDropDownMenu { ShowImageMargin: true })
        {
            using var font = new Font(Theme.GlyphFamily, e.TextFont?.SizeInPoints ?? 9f, GraphicsUnit.Point);
            var box = new Rectangle(2, 0, e.Item.Height + 4, e.Item.Height);
            TextRenderer.DrawText(e.Graphics, glyph, font, box, e.TextColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        base.OnRenderItemText(e);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = Theme.Current.Text;
        base.OnRenderArrow(e);
    }

    /// <summary>Häkchen als Symbol in Textfarbe – das Standardhäkchen ist immer schwarz und im dunklen Schema unsichtbar.</summary>
    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        var p = Theme.Current;
        var box = Rectangle.Inflate(e.ImageRectangle, 2, 2);
        using (var back = new SolidBrush(e.Item.Selected ? p.SubtleHover : p.Subtle))
        using (var path = Theme.RoundedRect(box, 3))
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.FillPath(back, path);
        }

        using var font = new Font(Theme.GlyphFamily, 8f, GraphicsUnit.Point);
        TextRenderer.DrawText(e.Graphics, Glyphs.CheckMark, font, box, e.Item.Enabled ? p.Text : p.TextSecondary,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    /// <summary>Setzt den Renderer auch für alle Untermenüs (sie verwenden sonst die hellen Windows-Farben).</summary>
    public static void ApplyTo(ToolStrip strip)
    {
        var renderer = new ModernMenuRenderer();
        strip.Renderer = renderer;
        Apply(strip.Items);

        void Apply(ToolStripItemCollection items)
        {
            foreach (var item in items.OfType<ToolStripMenuItem>().Where(i => i.HasDropDownItems))
            {
                item.DropDown.Renderer = renderer;
                Apply(item.DropDownItems);
            }
        }
    }

    private sealed class MenuColors(Palette p) : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => p.Card;

        public override Color ImageMarginGradientBegin => p.Card;

        public override Color ImageMarginGradientMiddle => p.Card;

        public override Color ImageMarginGradientEnd => p.Card;

        public override Color MenuBorder => p.Border;

        public override Color MenuItemBorder => p.Subtle;

        public override Color MenuItemSelected => p.Subtle;

        public override Color MenuItemSelectedGradientBegin => p.Subtle;

        public override Color MenuItemSelectedGradientEnd => p.Subtle;

        public override Color MenuItemPressedGradientBegin => p.SubtleHover;

        public override Color MenuItemPressedGradientEnd => p.SubtleHover;

        public override Color SeparatorDark => p.Border;

        public override Color SeparatorLight => p.Card;

        public override Color CheckBackground => p.Subtle;

        public override Color CheckSelectedBackground => p.SubtleHover;

        public override Color CheckPressedBackground => p.SubtleHover;
    }
}
