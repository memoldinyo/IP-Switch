# Changelog

All notable changes to IP-Switch are documented in this file. The release workflow uses the section of the tagged
version as release notes.

## [Unreleased]

- Automated build, packaging and code signing with GitHub Actions and SignPath.
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

[Unreleased]: https://github.com/memoldinyo/IP-Switch/compare/v1.4.0...HEAD
[1.4.0]: https://github.com/memoldinyo/IP-Switch/releases/tag/v1.4.0
