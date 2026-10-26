# IP-Switch – User Manual

**Version 1.4** · Author: **Mehmet Aslandag** · [Deutsche Fassung](BENUTZERHANDBUCH.md)

IP-Switch stores IPv4 configurations of network adapters as named profiles and applies them with one click – with a
static address or via DHCP. Before every change the previous configuration is backed up; if a change fails,
IP-Switch restores the previous state automatically.

Help is also available inside the application: **F1** in the manager window, the **?** at the top right of the
header card or **Help** in the tray menu.

---

## Contents

1. [Overview](#1-overview)
2. [Getting started](#2-getting-started)
3. [The tray menu](#3-the-tray-menu)
4. [The manager window](#4-the-manager-window)
5. [Creating and editing profiles](#5-creating-and-editing-profiles)
6. [Applying profiles](#6-applying-profiles)
7. [Assigning adapters and quick access](#7-assigning-adapters-and-quick-access)
8. [Backups and restore](#8-backups-and-restore)
9. [Administrator rights](#9-administrator-rights)
10. [Import and export](#10-import-and-export)
11. [Settings](#11-settings)
12. [Keyboard shortcuts](#12-keyboard-shortcuts)
13. [Troubleshooting](#13-troubleshooting)
14. [Privacy and storage locations](#14-privacy-and-storage-locations)
15. [About IP-Switch](#15-about-ip-switch)

---

## 1. Overview

| Term | Meaning |
|---|---|
| **Profile** | Named IPv4 configuration: IP address, subnet mask, gateway, DNS servers – or DHCP. |
| **Adapter** | Network connection of the computer, e.g. Ethernet, Wi-Fi, USB dock or virtual adapter. |
| **Assignment** | Link between a profile and an adapter. Assigned profiles appear in the tray as quick access. |
| **Target adapter** | The adapter a profile is applied to in the manager window. |
| **Backup** | Automatically saved state of an adapter before every change. |

IP-Switch runs permanently and resource-friendly in the notification area of the taskbar (tray). The manager window
is only opened when needed.

## 2. Getting started

1. **Start IP-Switch.** After installation IP-Switch starts automatically. The **IP** icon appears in the
   notification area – if necessary, show it via the **^** arrow next to the clock and pin it by drag & drop.
2. **Open the manager:** click the tray icon → **Open manager …**
3. **Save the original state:** click the desired adapter in the adapter bar at the bottom, choose **Save as
   profile** on the right and name it e.g. “Original state”.
4. **Create your own profile:** **New profile** → enter name and values → **Save**.
5. **Apply:** select the profile on the left → **Apply profile now**. Windows may ask for administrator rights (UAC).
6. **Assign:** use **⋮** on the profile tile → **Assign to** → adapter. From now on one click in the tray menu is
   enough.

> **Tip:** Sample profiles are located in the installation folder under `samples\beispielprofile.json`. They can be
> loaded via the gear → **Import profiles …**

## 3. The tray menu

A click (left or right) on the tray icon opens the menu:

```
IP-Switch
──────────────────────────────
Office  →  Wi-Fi                         ← quick access: one click applies
Set up quick access                  ▸   profile ▸ assign adapter
──────────────────────────────
● Wi-Fi     192.168.20.122/24 (DHCP)   ▸   status, IP, gateway, DNS
● Ethernet  10.0.0.5/24 (Static)       ▸     Apply profile “…”
More adapters (7)                    ▸     Apply another profile ▸
──────────────────────────────             Enable DHCP (IP and DNS)
Open manager …                             Save current configuration as profile …
New profile …                              Restore latest backup
──────────────────────────────
Settings                             ▸
Help
About IP-Switch
──────────────────────────────
Exit
```

- **Quick access:** profiles with an adapter assignment are listed at the top. One click applies the profile.
- **Adapter submenus:** connected adapters and adapters with assigned profiles are listed directly, all others under
  **More adapters**.
- The tooltip of the tray icon shows the addresses of the connected adapters.

## 4. The manager window

| Area | Content |
|---|---|
| **Header card (top left)** | Title, **New profile**, Edit, Duplicate, Delete, gear (Import, Export, data folder, Help, About) and **?** (Help). |
| **Profile row** | Number of profiles, search box, **+ New** and **⋯** (Import, Export, data folder). |
| **Profile tiles (left)** | Each tile shows IP/prefix, gateway, mode, DNS and – if present – the assigned adapter. **⋮** at the bottom right opens the profile menu. |
| **Assign profile to: (right)** | Target adapter, **Refresh**, current adapter configuration (with **Details**, **Save as profile**, **Backups**), **Enable DHCP** and **Apply profile now**. |
| **Adapters (bottom)** | All adapters as tiles with status, address and mode. A click selects the target adapter. Further adapters are behind **+ N more**. |

The **⋮ menu** of a profile tile (also via right-click or the menu key) contains:

- **Apply to “target adapter”**
- **Assign to ▸** – choose an adapter or **No assignment**
- **Edit …**, **Duplicate**, **Export …**
- **Delete**

The window adapts to its size: when narrow, the profile tiles become single-column and the buttons of the header
card move below the title. **Enable DHCP** and **Apply profile now** always stay visible.

## 5. Creating and editing profiles

New profiles are created with **New profile**, **+ New** or **Save as profile** (takes over the values of the
selected adapter). Edit with a double-click, **Enter** or **⋮ → Edit …**.

| Field | Required | Notes |
|---|---|---|
| Profile name | yes | Unique, at most 64 characters. |
| Description | no | At most 500 characters; shown on the tile. |
| Network adapter | no | Assignment for quick access. |
| IP assignment | yes | **Obtain automatically (DHCP)** or **Use the following IP address**. |
| IPv4 address | if static | e.g. `192.168.1.10`. |
| Subnet mask / prefix | if static | `255.255.255.0`, `24` or `/24`. The other notation is shown next to it. |
| Default gateway | no | Must be in the same subnet as the IP address. |
| Preferred / alternate DNS server | no | Leave empty with DHCP to obtain DNS via DHCP as well. |

**Validation while typing** – a red icon next to the field shows the reason, e.g.:

- invalid address or leading zeros (`192.168.01.1`),
- network or broadcast address of the subnet,
- non-contiguous subnet mask (`255.0.255.0`),
- gateway outside the subnet,
- identical DNS servers or an alternate DNS server without a preferred one,
- profile name already in use.

**Save and apply** saves the profile and immediately applies it to the assigned adapter.
**F1** in the editor opens the help on this topic.

## 6. Applying profiles

**In the manager window:** select a profile, choose the target adapter (in the adapter bar at the bottom or in the
list on the right) and click **Apply profile now**. If the profile is assigned to a different adapter, IP-Switch asks
for confirmation. The status line below shows in advance which profile will be applied to which adapter.

**In the tray menu:** quick access or adapter submenu → profile.

**Enable DHCP** switches IP address and DNS servers of the target adapter to automatic. IP-Switch also removes old
static addresses and gateways that Windows would otherwise keep active.

**Sequence of a change:**

1. The values are checked again – including whether the address is already assigned to another adapter (Windows
   does not allow the same IP on two adapters).
2. The current configuration is backed up. Without a successful backup nothing is changed.
3. The change is carried out and verified afterwards.
4. On an error the backup is restored automatically. **Show details** in the error dialog lists every executed step.

## 7. Assigning adapters and quick access

An assignment defines which adapter a profile is meant for:

- **Manager window:** **⋮** on the profile tile → **Assign to** → adapter or **No assignment**.
- **Tray menu:** **Set up quick access** → profile → adapter.
- **Profile editor:** field **Network adapter**.

Assigned profiles appear at the top of the tray menu as quick access (“Profile → Adapter”). When you select an
assigned profile in the manager window, the matching adapter is set as target automatically.

The assignment uses the adapter's internal identifier (GUID). Renaming the adapter in Windows keeps it intact. If an
adapter receives a new identifier after a driver reinstallation, IP-Switch recognises it by its MAC address. A
profile can be assigned to exactly one adapter; an adapter can have several profiles in quick access.

## 8. Backups and restore

Before every change IP-Switch saves the adapter's previous configuration – up to 20 backups per adapter.

- **Manager window:** **Backups** on the right → select a backup → **Restore**.
- **Tray menu:** adapter submenu → **Restore latest backup**.

A restore is itself a change and also creates a backup. An accidental restore can therefore be undone.

## 9. Administrator rights

Windows allows network settings to be changed only with administrator rights. IP-Switch runs with standard rights
and requests administrator rights only for the actual change (UAC prompt). If you decline the prompt, nothing is
changed.

**Without constant UAC prompts:** Tray → **Settings** → **Start with administrator rights (no UAC prompts)**.

1. Confirm the notice and answer the UAC prompt once with **Yes**. IP-Switch creates the scheduled task
   “IP-Switch Autostart (Administrator) …” and turns off the normal autostart.
2. **Restart with administrator rights now?** → **Yes**. The menu then shows “IP-Switch (Administrator)”.

For the current session, **Settings → Restart with administrator rights now** is enough. The option is available for
administrator accounts only. Please note: the application then runs permanently with elevated rights.

## 10. Import and export

- **Gear** or **⋯** → **Import profiles …** or **Export all profiles …**
- Single profile: **⋮** → **Export …**

Export files are JSON files in the `IPSwitch.Profiles` format. When importing, invalid entries are skipped with a
reason, name conflicts are resolved by renaming (“Name (2)”) and identifiers are reassigned.

## 11. Settings

Tray menu → **Settings**:

| Setting | Effect |
|---|---|
| Show all adapters | Also shows virtual helper adapters (Wi-Fi Direct, WAN Miniport etc.). |
| Show success notifications | Notification after a successful change. |
| Start with Windows | Starts IP-Switch at sign-in. |
| Start with administrator rights | See [Administrator rights](#9-administrator-rights). |
| Restart with administrator rights now | Restart with elevated rights for the current session. |
| Appearance | **Same as Windows**, **Light** or **Dark**. |
| Language | **Same as Windows** or one of eight languages: Deutsch, English, Türkçe, Français, Italiano, العربية (right-to-left), 中文 (Simplified), Русский. Help and manual are available in German and English; for all other languages the English version is shown. |
| Open data folder | Opens `%LOCALAPPDATA%\IPSwitch` in Explorer. |

## 12. Keyboard shortcuts

| Key | Function |
|---|---|
| **F1** | Open help (in the profile editor: help on profiles) |
| **F5** | Reload adapters |
| **Ctrl+N** | New profile |
| **Ctrl+F** | Search profiles |
| **Arrow keys** | Move between profile tiles |
| **Enter** / double-click | Edit profile |
| **Del** | Delete profile |
| **Menu key** / **Shift+F10** | Open the ⋮ menu of the profile |

## 13. Troubleshooting

| Problem | Solution |
|---|---|
| No tray icon visible | Open the **^** arrow in the notification area; enable IP-Switch under *Settings → Personalization → Taskbar → Other system tray icons*. |
| “Cancelled – administrator rights were not granted” | The UAC prompt was declined. Apply again and confirm with **Yes**. |
| “The address … is already assigned to the adapter …” | Windows does not allow the same IP on two adapters. Enable **DHCP** on the named adapter first. Such leftovers are shown as “Leftover” in the adapter details. |
| Applying failed, configuration restored | Check **Show details** in the error dialog and the log. Common causes: group policies, VPN or security software, disabled adapter. |
| Adapter missing from the list | Only adapters with IPv4 are shown; Windows does not report disabled adapters. Virtual adapters: *Settings → Show all adapters*. |
| Assignment “(not present)” | The adapter was removed or reinstalled – reassign the profile via **⋮ → Assign to**. |
| Corrupt profile file | IP-Switch creates a copy `profiles.json.defekt-…` and starts with an empty list. Re-import profiles from an export if necessary. |

The log with all executed commands is located at `%LOCALAPPDATA%\IPSwitch\logs\ip-switch.log`.

## 14. Privacy and storage locations

IP-Switch stores all data locally only. There is no cloud connection, no telemetry and no data transfer.

| Path | Content |
|---|---|
| `%LOCALAPPDATA%\IPSwitch\profiles.json` | Profiles |
| `%LOCALAPPDATA%\IPSwitch\settings.json` | Settings |
| `%LOCALAPPDATA%\IPSwitch\backups\` | Backups |
| `%LOCALAPPDATA%\IPSwitch\logs\` | Log |

These files are kept when uninstalling. To remove everything, delete the folder `%LOCALAPPDATA%\IPSwitch` (or use
`uninstall.ps1 -RemoveData` with the ZIP package).

## 15. About IP-Switch

**IP-Switch** – management of IPv4 configurations for network adapters.

- **Author:** Mehmet Aslandag
- **Version:** see tray menu → **About IP-Switch**
- **License:** Apache License 2.0
- **Technology:** C# / .NET 10, Windows Forms; configuration via `netsh interface ipv4`
- Further documentation: [README](../README.md), [Installation guide](INSTALLATION.md), [Build guide](BUILD.md)

Copyright © 2026 Mehmet Aslandag
