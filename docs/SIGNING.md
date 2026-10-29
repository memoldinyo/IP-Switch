# Code-Signierung und Release-Ablauf

IP-Switch wird über die [SignPath Foundation](https://signpath.org) kostenlos signiert. Signiert werden nur Dateien,
die der GitHub-Actions-Workflow [`.github/workflows/build.yml`](../.github/workflows/build.yml) aus dem öffentlichen
Quellcode baut – lokal gebaute Dateien werden nie signiert.

## Ablauf im Workflow

| Auslöser | Ergebnis |
|---|---|
| Push auf `main`, Pull Request | Tests, unsignierte Pakete als Workflow-Artefakte |
| Tag `v<Version>` (z. B. `v1.5.0`) | zusätzlich Signierung und GitHub-Release |

Jobs bei einem Versions-Tag:

1. **Test and publish:** Tests, `build.ps1 -Stage Publish`. Prüft, dass der Tag zur Version in
   `Directory.Build.props` passt. Artefakt `unsigned-executables` (beide `IPSwitch.exe`).
2. **Sign executables:** Signieranfrage an SignPath mit der Artefaktkonfiguration `executables`.
3. **Package:** `build.ps1 -Stage Package` mit den signierten EXE-Dateien → ZIPs und MSI.
4. **Sign installer:** Signieranfrage mit der Artefaktkonfiguration `installer` (signiert das MSI).
5. **GitHub release:** `SHA256SUMS.txt` über die endgültigen Dateien, Release-Notizen aus `CHANGELOG.md` und
   [`.github/release-footer.md`](../.github/release-footer.md).

Solange SignPath nicht eingerichtet ist, werden die Schritte 2 und 4 übersprungen und das Release enthält
unsignierte Dateien (mit Hinweis in den Release-Notizen).

## Einmalige Einrichtung

### 1. Antrag bei der SignPath Foundation

Formular: <https://signpath.org/apply>

| Feld | Wert |
|---|---|
| Project Name | IP-Switch |
| Repository URL | https://github.com/memoldinyo/IP-Switch |
| Homepage URL | https://github.com/memoldinyo/IP-Switch |
| Download URL | https://github.com/memoldinyo/IP-Switch/releases |
| Privacy Policy URL | https://github.com/memoldinyo/IP-Switch/blob/main/PRIVACY.md |
| Tagline | Lightweight Windows tray app to switch IPv4 network profiles per adapter with one click. |
| Maintainer Type | Individual |
| Build System | GitHub Actions |

### 2. SignPath-Projekt anlegen (nach der Zusage)

1. In SignPath ein Projekt anlegen, Slug z. B. `IP-Switch`, Repository-URL angeben.
2. **Trusted Build System:** GitHub.com verknüpfen und dem Projekt zuordnen (SignPath-GitHub-App für das Repository
   installieren).
3. **Artefaktkonfigurationen** mit genau diesen Slugs anlegen, Inhalt aus dem Repository übernehmen:
   - `executables` ← [`.signpath/artifact-configurations/executables.xml`](../.signpath/artifact-configurations/executables.xml)
   - `installer` ← [`.signpath/artifact-configurations/installer.xml`](../.signpath/artifact-configurations/installer.xml)
4. **Signing Policy** `release-signing` mit dem Zertifikat der Foundation, manuelle Freigabe (Approver: memoldinyo).
5. Einen **CI-Benutzer** anlegen, als Submitter der Policy eintragen und dessen API-Token erzeugen.

### 3. GitHub-Repository konfigurieren

*Settings → Secrets and variables → Actions*:

| Typ | Name | Wert |
|---|---|---|
| Secret | `SIGNPATH_API_TOKEN` | API-Token des CI-Benutzers |
| Variable | `SIGNPATH_ORGANIZATION_ID` | Organisations-ID aus SignPath |
| Variable | `SIGNPATH_PROJECT_SLUG` | z. B. `IP-Switch` |
| Variable | `SIGNPATH_SIGNING_POLICY_SLUG` | `release-signing` |

Außerdem empfehlenswert: *Settings → Code security → Private vulnerability reporting* aktivieren (für
[SECURITY.md](../SECURITY.md)).

## Ein Release erstellen

1. Version in [`Directory.Build.props`](../Directory.Build.props) erhöhen (`<Version>`).
2. In [`CHANGELOG.md`](../CHANGELOG.md) den Abschnitt `## [Unreleased]` in `## [<Version>] - <Datum>` umbenennen
   und einen neuen leeren `## [Unreleased]`-Abschnitt anlegen.
3. Committen und pushen, dann den Tag setzen:

   ```powershell
   git tag v1.5.0
   git push origin v1.5.0
   ```

4. Die Signieranfragen in SignPath freigeben (Approve). Der Workflow wartet bis zu einer Stunde je Anfrage.
5. Nach Abschluss steht das Release mit signierten Dateien unter *Releases*.

## Signatur prüfen

Im Explorer: Datei → *Eigenschaften* → *Digitale Signaturen*. Oder in PowerShell:

```powershell
Get-AuthenticodeSignature .\IPSwitch-1.5.0-x64.msi | Format-List Status, SignerCertificate
```
