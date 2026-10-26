using System.Drawing.Drawing2D;

namespace IPSwitch.App.Infrastructure;

/// <summary>Programmsymbol und kleine Statusgrafiken (einmalig erzeugt).</summary>
internal static class AppIcons
{
    private static readonly Lazy<Icon> LazyTray = new(() => LoadIcon(SystemInformation.SmallIconSize));
    private static readonly Lazy<Icon> LazyWindow = new(() => LoadIcon(new Size(32, 32)));
    private static readonly Lazy<Image> LazyConnected = new(() => Dot(Color.FromArgb(46, 160, 67)));
    private static readonly Lazy<Image> LazyDisconnected = new(() => Dot(Color.FromArgb(150, 150, 150)));

    public static Icon Tray => LazyTray.Value;

    public static Icon Window => LazyWindow.Value;

    public static Image Connected => LazyConnected.Value;

    public static Image Disconnected => LazyDisconnected.Value;

    private static Icon LoadIcon(Size size)
    {
        using var stream = typeof(AppIcons).Assembly.GetManifestResourceStream("IPSwitch.App.app.ico");
        return stream is null ? (Icon)SystemIcons.Application.Clone() : new Icon(stream, size);
    }

    private static Bitmap Dot(Color color)
    {
        var size = Math.Max(10, SystemInformation.SmallIconSize.Width * 10 / 16);
        var bmp = new Bitmap(size, size);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var brush = new SolidBrush(color);
        g.FillEllipse(brush, 1, 1, size - 3, size - 3);
        return bmp;
    }
}
