# Build-Anleitung

**IP-Switch** · Autor: Mehmet Aslandag

## Voraussetzungen

| Komponente | Version | Hinweis |
|---|---|---|
| Windows | 10 (1809+) oder 11, x64 | Zum Bauen und Ausführen |
| .NET SDK | 10.0 (getestet mit 10.0.402) | <https://dotnet.microsoft.com/download/dotnet/10.0> |
| Internetzugang zu nuget.org | nur beim ersten Restore | für xUnit (Tests) und WiX Toolset 5 (MSI) |
| Entwicklungsumgebung (optional) | Visual Studio 2026, VS Code mit C# Dev Kit oder JetBrains Rider | |

Prüfen:

```powershell
dotnet --list-sdks
```

## Schnellstart

```powershell
git clone <repo> IP-Switch
cd IP-Switch
.\build.ps1
```

Danach liegt in `artifacts\`:

| Datei | Beschreibung |
|---|---|
| `IPSwitch-1.0.0-win-x64.zip` | Eigenständige Single-File-EXE (keine .NET-Installation nötig) mit Installationsskript, README und Beispielprofilen |
| `IPSwitch-1.0.0-win-x64-net10.zip` | Kleine Variante, benötigt die *.NET 10 Desktop Runtime* |
| `IPSwitch-1.0.0-x64.msi` | Benutzerbezogenes MSI (keine Adminrechte für die Installation) |
| `SHA256SUMS.txt` | Prüfsummen |

Optionen: `-SkipTests`, `-SkipMsi` (z. B. ohne nuget.org-Zugang), `-Configuration Debug`.

`-Stage` teilt den Ablauf für die Code-Signierung: `Publish` (Tests und EXE-Dateien nach `artifacts\publish`),
danach die EXE-Dateien signieren, dann `Package` (ZIPs, MSI und Prüfsummen aus `artifacts\publish`). Ohne Angabe
(`All`) läuft beides direkt hintereinander.

> Falls die Ausführung von Skripten gesperrt ist:
> `powershell -ExecutionPolicy Bypass -File .\build.ps1`

## Einzelne Schritte

```powershell
# Bauen (Debug)
dotnet build IPSwitch.sln

# Tests
dotnet test tests\IPSwitch.Core.Tests\IPSwitch.Core.Tests.csproj

# Anwendung direkt starten (Debug)
dotnet run --project src\IPSwitch.App

# Eigenständige Single-File-EXE veröffentlichen
dotnet publish src\IPSwitch.App\IPSwitch.App.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -o artifacts\publish\self-contained

# MSI aus der veröffentlichten EXE erzeugen
dotnet build installer\IPSwitch.Installer.wixproj -c Release `
  -p:PublishDir=$PWD\artifacts\publish\self-contained -p:ProductVersion=1.0.0
```

> `EnableCompressionInSingleFile` wird bewusst nicht verwendet. Komprimierte Bundles werden beim Start in den
> Arbeitsspeicher entpackt. Gemessen waren das ca. 120 MB statt ca. 53 MB Working Set (privat 52 MB statt 13 MB).

## In der IDE

- **Visual Studio:** `IPSwitch.sln` öffnen, `IPSwitch.App` als Startprojekt festlegen, F5. Die Tests erscheinen im
  Test-Explorer.
- **VS Code:** Ordner öffnen, Erweiterung *C# Dev Kit* installieren, `dotnet run --project src\IPSwitch.App`.

Hinweis zum Debuggen: Ohne Administratorrechte startet sich die Anwendung beim Anwenden eines Profils als
**separater, erhöhter Prozess** neu (`IPSwitch.exe --apply …`). Breakpoints in `NetworkConfigurator` werden dann
im Tray-Prozess nicht getroffen. Startet man Visual Studio als Administrator, läuft auch IP-Switch erhöht und wendet
direkt im eigenen Prozess an, sodass die Breakpoints greifen. Alternativ lässt sich die Logik über die Unit-Tests
(`FakeNetsh`) prüfen.

## Version ändern

Die Version steht zentral in `Directory.Build.props` (`<Version>`). `build.ps1` verwendet sie für Dateinamen und MSI.
Das MSI nutzt `MajorUpgrade`: Eine neuere Version ersetzt die ältere automatisch.

## Programmsymbol neu erzeugen

```powershell
powershell -ExecutionPolicy Bypass -File tools\New-AppIcon.ps1
```

## Tests

Die Tests in `tests/IPSwitch.Core.Tests` laufen ohne Administratorrechte und ändern keine Systemeinstellungen:

- `IpValidatorTests`, `ProfileValidatorTests`: Adress-, Masken-, Gateway- und DNS-Regeln, Profilregeln, Umwandlung
  in die Zielkonfiguration.
- `StorageTests`: Speichern/Laden, Duplizieren, Löschen, beschädigte Dateien, Import/Export (inkl. Beispielprofile),
  Sicherungen, Einstellungen.
- `NetworkTests`: erzeugte `netsh`-Befehle. Mit dem Simulator `FakeNetsh` werden erfolgreiches Anwenden, Fehler mit
  Rollback, fehlgeschlagene Überprüfung, fehlgeschlagener Rollback, ungültige Aufträge, fehlende Adapter und nicht
  schreibbare Sicherungen geprüft. Außerdem Registry-Parser, Adapterzuordnung (GUID/MAC) und das Protokoll zum
  Hilfsprozess.
- `ElevationTests`: Aufgaben-XML für den Start mit Administratorrechten (Rechteebene, keine Laufzeitbegrenzung,
  Akkubetrieb, XML-Escaping), `schtasks`-Aufrufe zum Anlegen und Entfernen, SID-Prüfung, Hilfsprozess-Argumente.

Das tatsächliche Anwenden über `netsh` benötigt Administratorrechte. Es ist deshalb nicht Teil der automatischen Tests
und muss manuell geprüft werden (siehe [INSTALLATION.md](INSTALLATION.md#manueller-funktionstest)).

## Continuous Integration und Code-Signatur

Der Workflow [`.github/workflows/build.yml`](../.github/workflows/build.yml) baut und testet jeden Push und Pull
Request. Bei einem Versions-Tag `v<Version>` signiert er EXE und MSI über SignPath und erstellt das GitHub-Release.
Einrichtung und Release-Ablauf: [SIGNING.md](SIGNING.md).

Lokal gebaute Dateien sind nicht signiert; Windows SmartScreen zeigt daher beim ersten Start einen Hinweis. Wer mit
einem eigenen Zertifikat signieren möchte (z. B. im Unternehmen), signiert zwischen den Stufen:

```powershell
.\build.ps1 -Stage Publish
Get-ChildItem artifacts\publish\*\IPSwitch.exe | ForEach-Object {
    signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 /a $_.FullName }
.\build.ps1 -Stage Package
signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 /a (Get-ChildItem artifacts\*.msi).FullName
```

Nach dem Signieren des MSI ändert sich dessen Prüfsumme; `SHA256SUMS.txt` dann neu erzeugen.
