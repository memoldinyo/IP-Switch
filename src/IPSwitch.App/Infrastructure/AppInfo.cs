using System.Reflection;
using IPSwitch.Core.Elevation;
using IPSwitch.Core.Localization;

namespace IPSwitch.App.Infrastructure;

/// <summary>
/// Programmangaben. Autor und Copyright stammen aus Directory.Build.props (Eigenschaft IPSwitchAuthor),
/// damit sie nur an einer Stelle gepflegt werden.
/// </summary>
internal static class AppInfo
{
    private static readonly Assembly Assembly = typeof(AppInfo).Assembly;

    public const string Name = "IP-Switch";

    public static string Author { get; } =
        Assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? "Mehmet Aslandag";

    public static string Copyright { get; } =
        Assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? $"Copyright © 2026 {Author}";

    public static string Version { get; } =
        (Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0").Split('+')[0];

    /// <summary>Text für "Über IP-Switch".</summary>
    public static string AboutText(string dataDirectory) =>
        $"{Name} {Version}\n" +
        Loc.T("about.developedBy", Author) + "\n\n" +
        Loc.T("about.description") + "\n\n" +
        Loc.T(ElevationInfo.IsElevated ? "about.rightsAdmin" : "about.rightsUser") + "\n\n" +
        Loc.T("about.privacy", dataDirectory) + "\n\n" +
        Copyright;
}
