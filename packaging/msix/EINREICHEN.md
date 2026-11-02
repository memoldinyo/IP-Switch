# IP-Switch im Microsoft Store einreichen

App: **IP-Switch**, Store-ID `9N2Z0ZZSHM6S`, Paketfamilie `MkMone.IP-Switch_wfjt2twyn1pza`,
Herausgeber `MkMone` (dasselbe Partner-Center-Konto wie Kahraman). Identität und reservierter App-Name stehen in
[`identity.json`](identity.json) – aus *Partner Center › Product management › Product identity*.

## Paket bauen

```powershell
.\build.ps1
.\build-msix.ps1
```

Ergebnis: `artifacts\IPSwitch-<Version>-x64.msix` (unsigniert – der Store signiert es selbst).
Dasselbe Paket baut GitHub Actions bei jedem Push (Artefakt `IPSwitch-<Version>-msix` im Lauf, eine ZIP-Datei mit
der MSIX darin). `tools\Test-Msix.ps1` registriert das Paket zum Ausprobieren als Test-App (Entwicklermodus),
`-Remove` entfernt sie wieder.

## Was im Store-Paket anders ist

- Windows übernimmt Installation, Updates und Deinstallation; MSI und Installationsskript entfallen.
- **Autostart** über den StartupTask des Pakets (nach dem ersten Start eingeschaltet). Abschalten unter
  *Windows-Einstellungen › Apps › Autostart*; im Tray-Menü führt *Einstellungen › Autostart verwalten* dorthin.
  Beim Autostart bleibt IP-Switch ohne Fenster im Infobereich.
- **Start mit Administratorrechten** (geplante Aufgabe) gibt es im Paket nicht. Jede Änderung fragt per UAC
  (Berechtigung `allowElevation`); *Jetzt mit Administratorrechten neu starten* funktioniert weiterhin.
- **Daten** liegen im Paketordner (`%LOCALAPPDATA%\Packages\<Paketfamilie>\LocalState`). Beim ersten Start werden
  Profile und Einstellungen einer vorhandenen Desktop-Installation (`%LOCALAPPDATA%\IPSwitch`) übernommen.

## Einreichung im Partner Center

*Apps and games › IP-Switch › Start submission*

1. **Pricing and availability** – Markets: alle; Visibility: *Public audience*; Schedule: *as soon as possible*;
   Pricing: **Free**.
2. **Properties**
   - Category: **Utilities & tools** (Subcategory leer).
   - Privacy policy: *Yes* mit `https://github.com/memoldinyo/IP-Switch/blob/main/PRIVACY.md`
   - Website: `https://github.com/memoldinyo/IP-Switch`, Support: `https://github.com/memoldinyo/IP-Switch/issues`
   - Product declarations: das Häkchen bei „record and broadcast clips“ entfernen (nur für Spiele).
   - System requirements: nichts angeben.
3. **Age ratings** – IARC-Fragebogen, App Type *All Other App Types*, alle Fragen **No**
   (keine Nutzerinteraktion, kein Standort, keine Käufe, kein freier Internetzugang) → 3+ / USK 0 / PEGI 3.
4. **Packages** – `IPSwitch-<Version>-x64.msix` hochladen. Device family: nur **Windows 10/11 Desktop**.
   Bleibt der Upload bei „0 Bytes“ oder „Paused“ stehen, liegt es am Firmennetz: von zu Hause oder über einen
   Handy-Hotspot hochladen.
5. **Store listings** – je Sprache die Texte aus [`STORE-LISTING.md`](STORE-LISTING.md) einfügen.
   Screenshots (1920×1080) aus [`screenshots/`](screenshots/) (auch auf GitHub herunterladbar): `1-light-<Sprache>.png`, `2-dark-<Sprache>.png`,
   `3-tray-<Sprache>.png`. Store-Logos, Trailer und Xbox-Bilder leer lassen.
6. **Submission options › Restricted capabilities** – Begründung (auf Englisch) einfügen:

   ```
   IP-Switch is a classic Win32 desktop application (Windows Forms, .NET 10) packaged with the Desktop Bridge. It changes the IPv4 configuration of network adapters (static IP, subnet mask, gateway, DNS, DHCP) on the user's request.
   runFullTrust: it runs as a full-trust desktop app with a notification-area (tray) icon, reads the network adapter configuration through the IP Helper API and the TCP/IP registry keys, and starts netsh.exe to apply settings.
   allowElevation: changing network adapter settings with netsh requires administrator rights. When the user applies a profile, IP-Switch starts a short-lived helper process of its own executable with a UAC prompt; this process backs up the current configuration, applies the change, verifies it and exits. Nothing is changed if the user declines the UAC prompt. The app has no network code and collects no data.
   ```

   **Additional Testing Information** (Hinweis für die Prüfer):

   ```
   No account or test data needed. IP-Switch lives in the notification area (tray icon "IP"); the manager window opens on first start. Select a network adapter at the bottom, then e.g. "Save as profile" to capture its current settings, or "New profile" to create one. "Apply profile now" changes the adapter's settings and shows a UAC prompt (administrator rights are needed by Windows for network changes); before every change the previous configuration is backed up and restored automatically on failure. Right-click the tray icon for quick access, settings and help. The app has no network code.
   ```

7. **Submit for certification.** Die Prüfung dauert meist 1–3 Werktage, beim ersten Mal wegen der eingeschränkten
   Berechtigungen (`runFullTrust`, `allowElevation`) manchmal länger. Das Ergebnis kommt per E-Mail.

## Neue Version

`<Version>` in `Directory.Build.props` erhöhen (der Store verlangt eine höhere Version als die letzte),
`CHANGELOG.md` ergänzen, Pakete bauen, im Partner Center *Update* → neue `.msix` hochladen, „What's new“ ausfüllen,
einreichen.
