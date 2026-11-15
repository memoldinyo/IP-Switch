# Changelog

All notable changes to IP-Switch are documented in this file. The release workflow uses the section of the tagged
version as release notes.

## [Unreleased]

## [1.6.1] - 2026-11-15

- **Double-click on the tray icon** opens the manager window directly; a single left or right click still opens the
  tray menu.

## [1.6.0] - 2026-11-04

- **Disable and enable adapters:** the selected network adapter can be turned off and on again in the manager window
  (*Disable adapter* / *Enable adapter*) and in the adapter's tray submenu – like in Windows Network Connections. Disabled
  adapters are now listed as well; their status dot is red (green = connected, grey = disconnected). Profiles cannot be
  applied to a disabled adapter.
- **Dark mode for messages:** confirmations, notes and error messages use the app's own dialog in the current colour
  scheme (light/dark) instead of the always-light Windows message box; buttons follow the UI language.
- **Dark mode for the tray menu:** the tray menu and all its submenus follow the colour scheme as well.
- **Fixed:** *Restart with administrator rights now* uses the scheduled task only if it starts this very program;
  previously it could start an older installation in a different folder. If the task was deleted outside IP-Switch,
  the setting is reset.
- **Lighter on resources:** adapter queries shortly after one another share one result, the tray menu reads only the
  newest backup, and memory is released after the manager window is closed.

## [1.5.0] - 2026-11-02

- **Microsoft Store package:** `build-msix.ps1` builds an MSIX package for the Microsoft Store (signed by Microsoft,
  updates itself). In the Store version Windows handles installation, updates and autostart
  (*Settings › Apps › Startup*); profiles and settings of an existing desktop installation are taken over on first
  start. Changes still ask for administrator rights via UAC.
- Automated build, packaging and code signing with GitHub Actions and SignPath; the Store package is built as a
  workflow artifact.
- Code signing policy, privacy policy and security policy.
- Screenshots in the README.

## [1.4.0] - 2026-10-26

First public release.

- Windows tray application to save IPv4 configurations as named profiles and apply them with one click:
  static address, subnet mask or prefix, gateway, DNS or DHCP.
- Profiles can be assigned to network adapters (by stable interface ID, MAC address as fallback) and appear as
  quick access in the tray menu.
- Backup before every change, verification afterwards and automatic rollback on errors.
- Administrator rights only for the actual change (UAC), optionally start at sign-in with administrator rights.
- Manager window with light and dark mode.
- User interface in English, German, Turkish, French, Italian, Arabic (right-to-left), Chinese (Simplified) and
  Russian. Help and user manual in English and German.
- Import and export of profiles (JSON), sample profiles.
- Per-user MSI installer and portable ZIP packages.

[Unreleased]: https://github.com/memoldinyo/IP-Switch/compare/v1.6.1...HEAD
[1.6.1]: https://github.com/memoldinyo/IP-Switch/compare/v1.6.0...v1.6.1
[1.6.0]: https://github.com/memoldinyo/IP-Switch/compare/v1.5.0...v1.6.0
[1.5.0]: https://github.com/memoldinyo/IP-Switch/compare/v1.4.0...v1.5.0
[1.4.0]: https://github.com/memoldinyo/IP-Switch/releases/tag/v1.4.0
