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
   `3-tray-<Sprache>.png`, `4-adapter-<Sprache>.png`. Store-Logos, Trailer und Xbox-Bilder leer lassen.
6. **Submission options › Restricted capabilities** – je Berechtigung ein eigenes Feld, höchstens 500 Zeichen.

   *runFullTrust*:

   ```
   IP-Switch is a classic Win32 desktop app (Windows Forms, .NET 10) packaged with the Desktop Bridge. It switches the IPv4 settings of network adapters (static IP, subnet mask, gateway, DNS, DHCP) on the user's request. It needs full trust to run as a desktop app with a tray icon, to read the adapter configuration through the IP Helper API and the TCP/IP registry keys, and to start netsh.exe to apply settings. It has no network code and collects no data.
   ```

   *allowElevation*:

   ```
   Windows requires administrator rights to change network adapter settings with netsh. When the user applies a profile, IP-Switch starts a short-lived helper process of its own executable with a UAC prompt. The helper backs up the current configuration, applies the change, verifies it and exits; on failure the backup is restored automatically. If the user declines the UAC prompt, nothing is changed. By default the app itself runs without elevation.
   ```

   **Additional Testing Information** (Hinweis für die Prüfer, ebenfalls unter 500 Zeichen):

   ```
   No account or test data needed. IP-Switch runs in the notification area (tray icon "IP"); the manager window opens on first start. Select a network adapter at the bottom and click "Save as profile" or "New profile". "Apply profile now" changes the adapter settings after a UAC prompt; the previous configuration is backed up first and restored automatically on failure. Right-click the tray icon for quick access, settings and help.
   ```

7. **Submit for certification.** Die Prüfung dauert meist 1–3 Werktage, beim ersten Mal wegen der eingeschränkten
   Berechtigungen (`runFullTrust`, `allowElevation`) manchmal länger. Das Ergebnis kommt per E-Mail.

## Update 1.6.0 (neue Funktionen)

Version 1.5.0 wurde am 02.11.2026 als erste Version eingereicht. 1.6.0 bringt: Netzwerkadapter aus- und einschalten
(roter Statuspunkt für deaktivierte Adapter), dunkle Meldungsfenster und dunkles Tray-Menü, Korrektur beim Neustart mit
Administratorrechten, geringeren Speicherbedarf.

1. **Abwarten oder ersetzen:** Solange eine Einreichung in der Zertifizierung ist, lässt sich keine zweite anlegen.
   - Empfohlen: warten, bis 1.5.0 veröffentlicht ist („In the Store“), dann *Application overview › Update*.
   - Schneller: in der laufenden Einreichung *Cancel certification*, dann bei *Packages* die 1.5.0-MSIX löschen und die
     1.6.0-MSIX hochladen, die Texte unten eintragen und neu einreichen (die Prüfung beginnt dann von vorn).
2. **Packages:** `IPSwitch-1.6.0-x64.msix` hochladen (Build siehe oben oder GitHub-Actions-Artefakt `IPSwitch-1.6.0-msix`).
   Die alte 1.5.0-MSIX entfernen; die Version muss höher sein als die zuletzt veröffentlichte.
3. **Store listings:** je Sprache nur „What's new in this version“ und die neue Zeile unter „Features“ aus
   [`STORE-LISTING.md`](STORE-LISTING.md) übernehmen, dazu den neuen Screenshot `4-adapter-<Sprache>.png` hinzufügen
   und die Bilder 1–3 durch die neuen Fassungen ersetzen. Alternativ *Export listings* → CSV ausfüllen → *Import listings*.
4. **Submission options:** Die Begründungen für `runFullTrust` und `allowElevation` bleiben; bei `allowElevation`
   ergänzen, dass der Hilfsprozess jetzt auch einen Adapter aktiviert bzw. deaktiviert (Text unten).
5. **Additional Testing Information:** Text unten (mit Hinweis auf die neue Funktion) einsetzen und *Submit for certification*.

Am 10.11.2026 lehnte Microsoft `allowElevation` ab (Richtlinie 10.6.3: Begründung zu knapp, Support-Kontakt angeben).
Am 11.11.2026 wurde 1.6.0 mit den folgenden Texten neu eingereicht; Website und Support-URL stehen unter
*Properties › Support info*.

Begründung *allowElevation* (483 Zeichen):

```
IP-Switch changes IPv4 settings (static IP, DNS, DHCP) and turns network adapters off/on. Windows allows this only for administrators; there is no API for standard users. Elevation happens only when the user clicks Apply or Disable: a short-lived helper (same exe) starts via a UAC prompt, runs netsh, verifies and exits. No auto-elevation, no service, no background task. Open source: https://github.com/memoldinyo/IP-Switch - Support: https://github.com/memoldinyo/IP-Switch/issues
```

Additional Testing Information (446 Zeichen):

```
No account or test data needed. IP-Switch runs in the notification area (tray icon "IP"). Select an adapter at the bottom and click "Save as profile"; "Apply profile now" changes the settings after a UAC prompt (backup first, automatic rollback). New in 1.6.0: "Disable adapter"/"Enable adapter" next to "Refresh" turns the adapter off and on (UAC prompt); disabled adapters show a red dot. Support: https://github.com/memoldinyo/IP-Switch/issues
```

Wird `allowElevation` erneut abgelehnt: Plan B ist die Einreichung als signierte MSI (Store für Win32-Installer,
Signatur über SignPath Foundation oder Microsoft Trusted Signing).

## Neue Version

`<Version>` in `Directory.Build.props` erhöhen (der Store verlangt eine höhere Version als die letzte),
`CHANGELOG.md` ergänzen, Pakete bauen, im Partner Center *Update* → neue `.msix` hochladen, „What's new“ ausfüllen,
einreichen.
