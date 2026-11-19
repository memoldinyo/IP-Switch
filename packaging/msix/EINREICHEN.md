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
- **Start mit Administratorrechten** (geplante Aufgabe) gibt es im Paket nicht. Jede Änderung fragt per UAC;
  *Jetzt mit Administratorrechten neu starten* funktioniert weiterhin. Das Paket braucht dafür **kein**
  `allowElevation` (ab 1.6.3 entfernt, siehe unten).
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
   `3-tray-<Sprache>.png`, `4-adapter-<Sprache>.png`. Store-Logos, Trailer und Xbox-Bilder leer lassen.
6. **Submission options › Restricted capabilities** – nur noch `runFullTrust`, höchstens 500 Zeichen (484):

   ```
   IP-Switch is a classic Win32 desktop app (Windows Forms, .NET 10) packaged with the Desktop Bridge. It switches IPv4 settings of network adapters (static IP, gateway, DNS, DHCP) on the user's request. Full trust is needed for the tray icon, to read the adapter configuration (IP Helper API) and to start a short-lived helper of its own exe with the standard UAC prompt, which runs netsh.exe. No network code, no data collection. Support: https://github.com/memoldinyo/IP-Switch/issues
   ```

   **Additional Testing Information** (*Supplemental info*, Hinweis für die Prüfer, 484 Zeichen):

   ```
   No account or test data needed. IP-Switch runs in the notification area (tray icon "IP"); double-click it to open the manager. Select an adapter at the bottom and click "Save as profile"; "Apply profile now" changes the settings after a standard UAC prompt (backup first, automatic rollback). "Disable adapter"/"Enable adapter" next to "Refresh" turns the adapter off and on. Gear menu: settings, Network Connections (ncpa.cpl). Support: https://github.com/memoldinyo/IP-Switch/issues
   ```

7. **Submit for certification.** Die Prüfung dauert meist 1–3 Werktage, wegen `runFullTrust` manchmal länger.
   Das Ergebnis kommt per E-Mail.

## Verlauf der Zertifizierung

| Datum | Einreichung | Ergebnis |
|---|---|---|
| 02.11.2026 | 1.5.0 mit `runFullTrust` + `allowElevation` | 10.11.: `allowElevation` abgelehnt (10.6.3) |
| 11.11.2026 | 1.6.0, ausführlichere Begründung, Support-URL | 18.11.: erneut abgelehnt, gleicher Text |
| 19.11.2026 | 1.6.3 **ohne** `allowElevation` | – |

Microsoft schlug selbst vor, die Berechtigung zu entfernen. Test am 19.11.2026 mit einem lokal registrierten Paket ohne
`allowElevation`: Die App läuft mit Standardrechten, der Hilfsprozess startet per UAC-Abfrage, Adapter aus/ein und
*Jetzt mit Administratorrechten neu starten* funktionieren. Ab 1.6.3 enthält das Paket daher nur noch `runFullTrust`.

**Dritter Versuch (1.6.3)** – in der abgelehnten Einreichung (*Application overview › Submission 1*):

1. **Packages:** die 1.6.0-MSIX entfernen (*Remove*), `IPSwitch-1.6.3-x64.msix` hochladen. Die gelbe Warnung nennt danach
   nur noch `runFullTrust`.
2. **Submission options:** Das Feld für `allowElevation` verschwindet; die Begründung für `runFullTrust` durch den Text
   oben (Schritt 6) ersetzen.
3. **Supplemental info › Additional Testing Information:** Text oben einsetzen, *Save description*.
4. **Store listings:** je Sprache „What's new in this version“ aus [`STORE-LISTING.md`](STORE-LISTING.md) (Version 1.6.3).
5. *Application overview* → **Resubmit for certification**.

Wird auch das abgelehnt: Plan B ist die Einreichung als signierte MSI (Store für Win32-Installer, Signatur über
SignPath Foundation oder Microsoft Trusted Signing).

## Neue Version

`<Version>` in `Directory.Build.props` erhöhen (der Store verlangt eine höhere Version als die letzte),
`CHANGELOG.md` ergänzen, Pakete bauen, im Partner Center *Update* → neue `.msix` hochladen, „What's new“ ausfüllen,
einreichen.
