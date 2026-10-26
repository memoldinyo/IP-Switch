using IPSwitch.App.Infrastructure;

namespace IPSwitch.App.UI;

/// <summary>Englische Fassung der eingebauten Hilfe (auch für alle Sprachen außer Deutsch).</summary>
internal static partial class HelpContent
{
    private static IReadOnlyList<HelpTopic> EnglishTopics(string dataDirectory) =>
    [
        new(Overview, "Overview", Modern.Glyphs.Info,
        [
            new HelpParagraph($"IP-Switch stores IPv4 configurations of network adapters as named profiles and applies them with one click – static or via DHCP. Developed by {AppInfo.Author}."),
            new HelpHeading("The essentials"),
            new HelpBullets(
                "IP-Switch runs in the notification area of the taskbar (tray). Click the icon to open the menu.",
                "Profiles contain IP address, subnet mask, gateway, DNS servers or DHCP.",
                "Profiles can be assigned to an adapter and then appear in the tray as quick access.",
                "Before every change the previous configuration is backed up. If applying fails, it is restored automatically.",
                "Administrator rights are requested only for the change itself (UAC).",
                "All data stays local on this computer – no cloud, no telemetry."),
            new HelpHeading("The manager window"),
            new HelpTable(
                ("Top left", "Title and actions: New profile, Edit, Duplicate, Delete, gear (Import, Export, Help)."),
                ("Left", "Profiles as tiles with a search box. The ⋮ menu of each tile contains all profile actions."),
                ("Right", "“Assign profile to:” – target adapter, current configuration, Enable DHCP, Apply profile now."),
                ("Bottom", "All network adapters as tiles. A click selects the target adapter.")),
        ]),
        new("start", "Getting started", Modern.Glyphs.Play,
        [
            new HelpBullets(
                "1. Click the tray icon → “Open manager …”.",
                "2. At the bottom, select the adapter you want to configure.",
                "3. On the right, choose “Save as profile” to keep the current state as a profile, e.g. “Original state”.",
                "4. “New profile” → enter name, IP address, subnet mask etc. → Save.",
                "5. Select the profile on the left → “Apply profile now”. Windows may ask for administrator rights (UAC).",
                "6. Use the ⋮ menu → “Assign to” to choose the adapter. The profile is then available in the tray menu as quick access."),
            new HelpParagraph("Tip: Sample profiles are located in the installation folder under “samples\\beispielprofile.json” and can be loaded via the gear → “Import profiles …”."),
        ]),
        new("profiles", "Creating and editing profiles", Modern.Glyphs.Edit,
        [
            new HelpParagraph("New profiles are created with “New profile”, “+ New” or “Save as profile” (takes over the values of the selected adapter). Edit with a double-click, Enter or ⋮ → “Edit …”."),
            new HelpHeading("Fields"),
            new HelpTable(
                ("Profile name", "Required, unique, at most 64 characters."),
                ("Description", "Optional, at most 500 characters."),
                ("Network adapter", "Optional assignment (see “Assigning adapters”)."),
                ("IP assignment", "“Obtain automatically (DHCP)” or “Use the following IP address”."),
                ("IPv4 address", "Required for a static configuration, e.g. 192.168.1.10."),
                ("Subnet mask / prefix", "255.255.255.0, 24 or /24 – all three notations are accepted."),
                ("Default gateway", "Optional; must be in the same subnet as the IP address."),
                ("DNS servers", "Preferred and alternate. Leave empty with DHCP to obtain DNS via DHCP as well.")),
            new HelpHeading("Validation"),
            new HelpParagraph("Entries are checked while you type. A red icon next to the field shows the problem, e.g. an invalid address, a network or broadcast address, a non-contiguous subnet mask, a gateway outside the subnet, duplicate DNS servers or a profile name that is already in use."),
            new HelpParagraph("“Save and apply” saves the profile and immediately applies it to the assigned adapter."),
        ]),
        new(Apply, "Applying profiles", Modern.Glyphs.Accept,
        [
            new HelpHeading("In the manager window"),
            new HelpParagraph("Select a profile on the left, choose the target adapter at the bottom or on the right and click “Apply profile now”. If the profile is assigned to a different adapter, IP-Switch asks for confirmation."),
            new HelpHeading("In the tray menu"),
            new HelpBullets(
                "Quick access: profiles with an adapter assignment are listed at the top (“Profile → Adapter”) – one click applies them.",
                "Per adapter: a submenu with status, assigned profiles, “Apply another profile” and “Enable DHCP”."),
            new HelpHeading("Enable DHCP"),
            new HelpParagraph("Switches IP address and DNS servers of the target adapter to automatic. Old static addresses and gateways that Windows would otherwise keep are removed as well."),
            new HelpHeading("What happens when applying"),
            new HelpBullets(
                "The entries are checked again, including whether the address is already assigned to another adapter.",
                "The current configuration is backed up – no backup, no change.",
                "The change is carried out and verified afterwards.",
                "On an error the backup is restored automatically. “Show details” in the error dialog lists every step."),
        ]),
        new("assign", "Assigning adapters", Modern.Glyphs.Link,
        [
            new HelpParagraph("An assignment defines which adapter a profile is meant for. Assigned profiles appear in the tray as quick access, and selecting the profile automatically sets the matching adapter as target."),
            new HelpBullets(
                "In the manager window: ⋮ on the profile tile → “Assign to” → adapter or “No assignment”.",
                "In the tray: “Set up quick access” → profile → adapter.",
                "In the profile editor: field “Network adapter”."),
            new HelpParagraph("The assignment uses the adapter's internal identifier. Renaming an adapter in Windows keeps it intact. If an adapter receives a new identifier after a driver reinstallation, it is recognised by its MAC address."),
        ]),
        new("backups", "Backups", Modern.Glyphs.History,
        [
            new HelpParagraph("Before every change IP-Switch saves the adapter's previous configuration (up to 20 backups per adapter)."),
            new HelpBullets(
                "Manager window: “Backups” on the right → select a backup → “Restore”.",
                "Tray: adapter submenu → “Restore latest backup”."),
            new HelpParagraph("A restore is itself a change and creates another backup – so an accidental restore can be undone as well."),
        ]),
        new("admin", "Administrator rights", Modern.Glyphs.Shield,
        [
            new HelpParagraph("Windows allows network settings to be changed only with administrator rights. IP-Switch itself runs with standard rights and requests them only for the change (UAC prompt). If you decline the prompt, nothing is changed."),
            new HelpHeading("Without constant UAC prompts"),
            new HelpParagraph("Tray → Settings → “Start with administrator rights (no UAC prompts)” sets up a scheduled task that starts IP-Switch at sign-in with administrator rights (one-time UAC confirmation). Profiles are then applied without a prompt. For the current session, “Restart with administrator rights now” is enough."),
            new HelpParagraph("Note: the application then runs permanently with elevated rights. The option is available for administrator accounts only."),
        ]),
        new("transfer", "Import and export", Modern.Glyphs.Export,
        [
            new HelpBullets(
                "Gear or ⋯ → “Import profiles …” / “Export all profiles …”.",
                "Single profile: ⋮ on the tile → “Export …”."),
            new HelpParagraph("Export files are JSON files. When importing, invalid entries are skipped with a reason and name conflicts are resolved by renaming (“Name (2)”)."),
        ]),
        new("settings", "Settings", Modern.Glyphs.Settings,
        [
            new HelpParagraph("All settings are found in the tray menu under “Settings”."),
            new HelpTable(
                ("Show all adapters", "Also shows virtual helper adapters (Wi-Fi Direct, WAN Miniport etc.)."),
                ("Show success notifications", "Notification after a successful change."),
                ("Start with Windows", "Starts IP-Switch at sign-in."),
                ("Start with administrator rights", "See “Administrator rights”."),
                ("Appearance", "Same as Windows, Light or Dark."),
                ("Language", "Same as Windows or one of eight languages: Deutsch, English, Türkçe, Français, Italiano, العربية (right-to-left), 中文, Русский. Help and manual are available in German and English."),
                ("Open data folder", $"Opens {dataDirectory}.")),
        ]),
        new("keys", "Keyboard shortcuts", Modern.Glyphs.Info,
        [
            new HelpTable(
                ("F1", "Open this help"),
                ("F5", "Reload adapters"),
                ("Ctrl+N", "New profile"),
                ("Ctrl+F", "Search profiles"),
                ("Arrow keys", "Move between profile tiles"),
                ("Enter / double-click", "Edit profile"),
                ("Del", "Delete profile"),
                ("Menu key / Shift+F10", "Open the ⋮ menu of the profile")),
        ]),
        new("trouble", "Troubleshooting", Modern.Glyphs.Warning,
        [
            new HelpTable(
                ("No tray icon", "Open the ^ arrow in the notification area; enable IP-Switch under Windows Settings → Taskbar → Other system tray icons."),
                ("“Cancelled – administrator rights were not granted”", "The UAC prompt was declined. Apply again and confirm with “Yes”."),
                ("“Address already assigned to another adapter”", "Windows does not allow the same IP on two adapters. Enable DHCP on the named adapter first."),
                ("Applying failed, configuration restored", "Check “Show details” in the dialog and the log. Common causes: group policies, VPN or security software."),
                ("Adapter missing", "Only adapters with IPv4 are shown; Windows does not report disabled adapters. Virtual adapters: Settings → Show all adapters."),
                ("Assignment “(not present)”", "The adapter was removed or reinstalled – reassign the profile via ⋮ → “Assign to”.")),
            new HelpParagraph($"The log with every executed command is located at {Path.Combine(dataDirectory, "logs")}."),
        ]),
        new(About, "About IP-Switch", Modern.Glyphs.Info,
        [
            new HelpHeading($"{AppInfo.Name} {AppInfo.Version}"),
            new HelpParagraph($"Developed by {AppInfo.Author}."),
            new HelpParagraph("IP-Switch is a Windows application for managing IPv4 configurations of network adapters."),
            new HelpHeading("Privacy"),
            new HelpBullets(
                $"Profiles, settings, backups and the log are stored locally only under {dataDirectory}.",
                "No cloud connection, no telemetry, no data transfer."),
            new HelpParagraph(AppInfo.Copyright),
        ]),
    ];
}
