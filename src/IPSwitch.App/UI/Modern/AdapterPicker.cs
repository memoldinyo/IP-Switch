using System.Drawing.Drawing2D;
using IPSwitch.Core.Models;

namespace IPSwitch.App.UI.Modern;

/// <summary>Auswahlliste der Netzwerkadapter mit Statuspunkt und aktueller Adresse.</summary>
internal sealed class AdapterPicker : ComboBox
{
    public AdapterPicker()
    {
        DropDownStyle = ComboBoxStyle.DropDownList;
        DrawMode = DrawMode.OwnerDrawFixed;
        FlatStyle = FlatStyle.Standard;
        BackColor = Theme.Current.Input;
        ForeColor = Theme.Current.Text;
        Font = new Font(Font.FontFamily, 10f);
        MaxDropDownItems = 14;
        Theme.ApplyNativeTheme(this, "DarkMode_CFD", "CFD");
    }

    public AdapterInfo? SelectedAdapter => SelectedItem as AdapterInfo;

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ItemHeight = LogicalToDeviceUnits(28);
        DropDownWidth = Math.Max(Width, LogicalToDeviceUnits(420));
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (IsHandleCreated)
        {
            DropDownWidth = Math.Max(Width, LogicalToDeviceUnits(420));
        }
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        var p = Theme.Current;
        var g = e.Graphics;
        var selected = (e.State & DrawItemState.Selected) != 0 && (e.State & DrawItemState.ComboBoxEdit) == 0;
        using (var back = new SolidBrush(selected ? p.SubtleHover : p.Input))
        {
            g.FillRectangle(back, e.Bounds);
        }

        if (e.Index < 0 || e.Index >= Items.Count || Items[e.Index] is not AdapterInfo adapter)
        {
            return;
        }

        var dot = LogicalToDeviceUnits(8);
        var x = e.Bounds.X + LogicalToDeviceUnits(8);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var dotBrush = new SolidBrush(adapter.IsConnected ? p.Connected : p.Disconnected))
        {
            g.FillEllipse(dotBrush, x, e.Bounds.Y + ((e.Bounds.Height - dot) / 2), dot, dot);
        }

        x += dot + LogicalToDeviceUnits(10);
        var detail = $"{adapter.PrimaryAddressText}  ·  {adapter.ModeText}";
        var detailWidth = TextRenderer.MeasureText(detail, Font).Width;
        var nameRect = new Rectangle(x, e.Bounds.Y, Math.Max(0, e.Bounds.Right - x - detailWidth - LogicalToDeviceUnits(16)), e.Bounds.Height);
        TextRenderer.DrawText(g, adapter.Name, Font, nameRect, p.Text,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        TextRenderer.DrawText(g, detail, Font, new Rectangle(e.Bounds.Right - detailWidth - LogicalToDeviceUnits(8), e.Bounds.Y, detailWidth, e.Bounds.Height),
            p.TextSecondary, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);
    }
}
