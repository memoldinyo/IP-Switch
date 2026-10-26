using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using IPSwitch.Core.Storage;
using Microsoft.Win32;

namespace IPSwitch.App.UI.Modern;

/// <summary>Farbpalette eines Farbschemas.</summary>
internal sealed record Palette(
    bool IsDark,
    Color Background,
    Color Card,
    Color CardHover,
    Color CardSelected,
    Color Inner,
    Color Border,
    Color Text,
    Color TextSecondary,
    Color Accent,
    Color AccentHover,
    Color OnAccent,
    Color Success,
    Color SuccessHover,
    Color OnSuccess,
    Color Warning,
    Color WarningHover,
    Color OnWarning,
    Color Danger,
    Color DangerHover,
    Color Input,
    Color Subtle,
    Color SubtleHover,
    Color Connected,
    Color Disconnected)
{
    /// <summary>"Profil jetzt anwenden": Himmelblau RGB 117, 183, 229 (in beiden Schemata).</summary>
    public Color Apply { get; } = Color.FromArgb(117, 183, 229);

    public Color ApplyHover { get; } = Color.FromArgb(141, 197, 236);

    public Color OnApply { get; } = Color.FromArgb(10, 32, 50);

    /// <summary>"DHCP aktivieren": Bordeaux RGB 90, 14, 39 (in beiden Schemata).</summary>
    public Color Dhcp { get; } = Color.FromArgb(90, 14, 39);

    public Color DhcpHover { get; } = Color.FromArgb(114, 26, 54);

    public Color OnDhcp { get; } = Color.White;
}

/// <summary>
/// Hell-/Dunkel-Schema im Stil von Windows 11 sowie Hilfen zum Einfärben von Standard-Steuerelementen,
/// Titelleiste und Bildlaufleisten.
/// </summary>
internal static class Theme
{
    private static readonly Palette DarkPalette = new(
        IsDark: true,
        Background: Color.FromArgb(32, 32, 32),
        Card: Color.FromArgb(43, 43, 43),
        CardHover: Color.FromArgb(50, 50, 50),
        CardSelected: Color.FromArgb(58, 58, 58),
        Inner: Color.FromArgb(37, 37, 37),
        Border: Color.FromArgb(62, 62, 62),
        Text: Color.FromArgb(255, 255, 255),
        TextSecondary: Color.FromArgb(184, 184, 184),
        Accent: Color.FromArgb(96, 205, 255),
        AccentHover: Color.FromArgb(124, 214, 255),
        OnAccent: Color.FromArgb(0, 0, 0),
        Success: Color.FromArgb(56, 158, 72),
        SuccessHover: Color.FromArgb(66, 175, 83),
        OnSuccess: Color.White,
        Warning: Color.FromArgb(247, 160, 30),
        WarningHover: Color.FromArgb(250, 176, 64),
        OnWarning: Color.FromArgb(20, 20, 20),
        Danger: Color.FromArgb(232, 72, 77),
        DangerHover: Color.FromArgb(240, 96, 100),
        Input: Color.FromArgb(45, 45, 45),
        Subtle: Color.FromArgb(58, 58, 58),
        SubtleHover: Color.FromArgb(70, 70, 70),
        Connected: Color.FromArgb(76, 194, 92),
        Disconnected: Color.FromArgb(128, 128, 128));

    private static readonly Palette LightPalette = new(
        IsDark: false,
        Background: Color.FromArgb(243, 243, 243),
        Card: Color.FromArgb(255, 255, 255),
        CardHover: Color.FromArgb(249, 249, 249),
        CardSelected: Color.FromArgb(234, 243, 251),
        Inner: Color.FromArgb(249, 249, 249),
        Border: Color.FromArgb(224, 224, 224),
        Text: Color.FromArgb(27, 27, 27),
        TextSecondary: Color.FromArgb(96, 96, 96),
        Accent: Color.FromArgb(0, 95, 184),
        AccentHover: Color.FromArgb(25, 113, 196),
        OnAccent: Color.White,
        Success: Color.FromArgb(46, 125, 50),
        SuccessHover: Color.FromArgb(56, 142, 60),
        OnSuccess: Color.White,
        Warning: Color.FromArgb(224, 138, 0),
        WarningHover: Color.FromArgb(236, 152, 20),
        OnWarning: Color.FromArgb(20, 20, 20),
        Danger: Color.FromArgb(196, 43, 28),
        DangerHover: Color.FromArgb(212, 63, 48),
        Input: Color.White,
        Subtle: Color.FromArgb(240, 240, 240),
        SubtleHover: Color.FromArgb(229, 229, 229),
        Connected: Color.FromArgb(46, 160, 67),
        Disconnected: Color.FromArgb(150, 150, 150));

    private static readonly Lazy<string> LazyGlyphFamily = new(() =>
    {
        using var fonts = new InstalledFontCollection();
        var names = fonts.Families.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return names.Contains("Segoe Fluent Icons") ? "Segoe Fluent Icons" : "Segoe MDL2 Assets";
    });

    public static Palette Current { get; private set; } = Resolve(ThemeMode.System);

    /// <summary>Schriftfamilie für Symbole (Windows 11: Segoe Fluent Icons, Windows 10: Segoe MDL2 Assets).</summary>
    public static string GlyphFamily => LazyGlyphFamily.Value;

    public static void Configure(ThemeMode mode) => Current = Resolve(mode);

    public static Palette Resolve(ThemeMode mode) => mode switch
    {
        ThemeMode.Dark => DarkPalette,
        ThemeMode.Light => LightPalette,
        _ => SystemUsesDarkMode() ? DarkPalette : LightPalette,
    };

    public static bool SystemUsesDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int light && light == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static Color Blend(Color a, Color b, double amount) => Color.FromArgb(
        (int)(a.R + ((b.R - a.R) * amount)),
        (int)(a.G + ((b.G - a.G) * amount)),
        (int)(a.B + ((b.B - a.B) * amount)));

    public static GraphicsPath RoundedRect(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        var d = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        if (d <= 1)
        {
            path.AddRectangle(bounds);
            return path;
        }

        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    /// <summary>
    /// Leserichtung der Oberflächensprache übernehmen: Bei Arabisch wird das ganze Fenster gespiegelt
    /// (Steuerelemente, eigene Zeichnungen und Menüs von rechts nach links).
    /// </summary>
    public static void ApplyDirection(Form form)
    {
        var rtl = IPSwitch.Core.Localization.Loc.IsRightToLeft;
        form.RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No;
        form.RightToLeftLayout = rtl;
    }

    /// <summary>Färbt Titelleiste (Windows 10 20H1+/11) passend zum Schema.</summary>
    public static void ApplyTitleBar(Form form)
    {
        void Apply()
        {
            var dark = Current.IsDark ? 1 : 0;
            if (DwmSetWindowAttribute(form.Handle, 20, ref dark, sizeof(int)) != 0)
            {
                DwmSetWindowAttribute(form.Handle, 19, ref dark, sizeof(int));
            }

            // Windows 11: Titelleiste in Hintergrundfarbe (wird auf älteren Versionen ignoriert).
            var caption = ColorTranslator.ToWin32(Current.Background);
            DwmSetWindowAttribute(form.Handle, 35, ref caption, sizeof(int));
        }

        if (form.IsHandleCreated)
        {
            Apply();
        }
        else
        {
            form.HandleCreated += (_, _) => Apply();
        }
    }

    /// <summary>Dunkle bzw. helle Bildlaufleisten und Auswahllisten für native Steuerelemente.</summary>
    public static void ApplyNativeTheme(Control control, string darkClass = "DarkMode_Explorer", string lightClass = "Explorer")
    {
        void Apply() => SetWindowTheme(control.Handle, Current.IsDark ? darkClass : lightClass, null);

        if (control.IsHandleCreated)
        {
            Apply();
        }
        else
        {
            control.HandleCreated += (_, _) => Apply();
        }
    }

    /// <summary>Färbt ein Fenster mit Standard-Steuerelementen (Dialoge) im aktuellen Schema ein.</summary>
    public static void Apply(Control root)
    {
        var p = Current;
        switch (root)
        {
            case Form form:
                form.BackColor = p.Background;
                form.ForeColor = p.Text;
                ApplyTitleBar(form);
                ApplyDirection(form);
                break;
            case ModernButton:
                break;
            case Button button:
                button.FlatStyle = FlatStyle.Flat;
                button.UseVisualStyleBackColor = false;
                button.BackColor = p.Subtle;
                button.ForeColor = p.Text;
                button.FlatAppearance.BorderColor = p.Border;
                button.FlatAppearance.MouseOverBackColor = p.SubtleHover;
                button.FlatAppearance.MouseDownBackColor = Blend(p.SubtleHover, p.Text, 0.08);
                break;
            case TextBox box:
                box.BackColor = p.Input;
                box.ForeColor = p.Text;
                box.BorderStyle = BorderStyle.FixedSingle;
                ApplyNativeTheme(box);
                break;
            case ComboBox combo:
                // Standardstil + "DarkMode_CFD": Windows zeichnet Rahmen und Pfeil selbst im passenden Schema.
                combo.FlatStyle = FlatStyle.Standard;
                combo.BackColor = p.Input;
                combo.ForeColor = p.Text;
                ApplyNativeTheme(combo, "DarkMode_CFD", "CFD");
                break;
            case ListView list:
                list.BackColor = p.Card;
                list.ForeColor = p.Text;
                list.BorderStyle = BorderStyle.FixedSingle;
                ThemeListViewHeader(list);
                ApplyNativeTheme(list);
                break;
            case Label label when label.ForeColor == SystemColors.GrayText:
                label.ForeColor = p.TextSecondary;
                break;
            case RadioButton or CheckBox:
                root.ForeColor = p.Text;
                break;
        }

        foreach (Control child in root.Controls)
        {
            Apply(child);
        }
    }

    private static void ThemeListViewHeader(ListView list)
    {
        if (list.View != View.Details || list.OwnerDraw)
        {
            return;
        }

        list.OwnerDraw = true;
        list.DrawItem += (_, e) => e.DrawDefault = true;
        list.DrawSubItem += (_, e) => e.DrawDefault = true;
        list.DrawColumnHeader += (_, e) =>
        {
            var p = Current;
            using var back = new SolidBrush(p.Inner);
            using var line = new Pen(p.Border);
            e.Graphics.FillRectangle(back, e.Bounds);
            e.Graphics.DrawLine(line, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            e.Graphics.DrawLine(line, e.Bounds.Right - 1, e.Bounds.Top + 4, e.Bounds.Right - 1, e.Bounds.Bottom - 5);
            var text = Rectangle.Inflate(e.Bounds, -6, 0);
            TextRenderer.DrawText(e.Graphics, e.Header?.Text, e.Font, text, p.TextSecondary,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        };
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hwnd, string? subAppName, string? subIdList);
}

/// <summary>Häufig verwendete Symbole (Segoe Fluent Icons / Segoe MDL2 Assets).</summary>
internal static class Glyphs
{
    public const string Add = "";
    public const string Edit = "";
    public const string Delete = "";
    public const string Copy = "";
    public const string Settings = "";
    public const string Refresh = "";
    public const string Play = "";
    public const string Accept = "";
    public const string Save = "";
    public const string History = "";
    public const string Info = "";
    public const string Search = "";
    public const string Ethernet = "";
    public const string Sync = "";
    public const string Import = "";
    public const string Export = "";
    public const string Folder = "";
    public const string Warning = "";
    public const string Shield = "";
    public const string Forward = "";
    public const string Wifi = "";
    public const string Bluetooth = "";
    public const string Link = "";
    public const string Help = "";
    public const string Book = "";

    public static string ForAdapter(IPSwitch.Core.Models.AdapterInfo adapter) => adapter.InterfaceType switch
    {
        System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211 => Wifi,
        _ when adapter.Description.Contains("Bluetooth", StringComparison.OrdinalIgnoreCase) => Bluetooth,
        _ => Ethernet,
    };
}
