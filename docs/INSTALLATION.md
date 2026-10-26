# Installationsanleitung

**IP-Switch** · Autor: Mehmet Aslandag

IP-Switch wird **pro Benutzer** installiert. Für die Installation sind keine Administratorrechte nötig. Erst wenn eine
Netzwerkeinstellung geändert wird, fragt Windows über UAC nach einer Bestätigung. Ohne lokale Administratorrechte
werden dabei die Anmeldedaten eines Administrators abgefragt.

Systemvoraussetzungen: Windows 10 (1809 oder neuer) oder Windows 11, 64 Bit.

## Variante A: MSI (empfohlen)

1. `IPSwitch-1.0.0-x64.msi` doppelklicken.
2. Die Installation läuft ohne weitere Fragen nach `%LOCALAPPDATA%\Programs\IP-Switch`. Sie legt eine
   Startmenü-Verknüpfung an, aktiviert den Autostart und startet IP-Switch.
3. Das Symbol erscheint im Infobereich. Gegebenenfalls über den Pfeil `^` neben der Uhr einblenden und per
   Drag & Drop dauerhaft anheften.

Unbeaufsichtigte Installation (z. B. Softwareverteilung im Benutzerkontext):

```bat
msiexec /i IPSwitch-1.0.0-x64.msi /qn LAUNCHAPP=0 AUTOSTART=0
```

| Eigenschaft | Standard | Bedeutung |
|---|---|---|
| `LAUNCHAPP` | `1` | IP-Switch nach der Installation starten |
| `AUTOSTART` | `1` | IP-Switch bei der Anmeldung starten (in der App unter *Einstellungen* änderbar) |

**Update:** Die neuere MSI-Datei einfach installieren. Die alte Version wird ersetzt, Profile bleiben erhalten.
**Deinstallation:** *Einstellungen → Apps → Installierte Apps → IP-Switch → Deinstallieren* oder
`msiexec /x IPSwitch-1.0.0-x64.msi`. Profile und Sicherungen in `%LOCALAPPDATA%\IPSwitch` bleiben erhalten.

## Variante B: ZIP mit Installationsskript

1. `IPSwitch-1.0.0-win-x64.zip` entpacken. Falls Windows die Datei blockiert: Rechtsklick → *Eigenschaften* →
   *Zulassen*.
2. `Installieren.cmd` doppelklicken. Das Skript
   - kopiert `IPSwitch.exe` nach `%LOCALAPPDATA%\Programs\IP-Switch`,
   - legt eine Startmenü-Verknüpfung an,
   - trägt IP-Switch unter *Installierte Apps* ein,
   - aktiviert den Autostart und startet die Anwendung.

Alternativ in PowerShell mit Optionen:

```powershell
powershell -ExecutionPolicy Bypass -File .\install.ps1 [-Autostart] [-NoStart] [-InstallDir <Pfad>]
```

Deinstallation über *Installierte Apps* oder `Deinstallieren.cmd`. Mit `uninstall.ps1 -RemoveData` werden auch
Profile und Sicherungen gelöscht.

## Variante C: Portabel

`IPSwitch.exe` aus dem ZIP an einen beliebigen Ort kopieren und starten. Es ist keine Installation nötig. Daten liegen
trotzdem in `%LOCALAPPDATA%\IPSwitch`. Den Autostart kann man in der Anwendung unter *Einstellungen → Mit Windows
starten* einschalten.

## Variante mit .NET-Runtime (kleiner Download)

`IPSwitch-1.0.0-win-x64-net10.zip` enthält eine wenige MB große EXE. Sie setzt die
[.NET 10 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/10.0) voraus. Installation wie in
Variante B oder C.

## Ohne ständige UAC-Abfragen (Start mit Administratorrechten)

Standardmäßig fragt Windows bei jeder Änderung per UAC nach. Auf Administratorkonten lässt sich das abschalten:

1. Tray-Menü → *Einstellungen* → *Mit Administratorrechten starten (ohne UAC-Abfragen)*.
2. Den Hinweis bestätigen und die UAC-Abfrage einmalig mit *Ja* beantworten. IP-Switch legt die geplante Aufgabe
   „IP-Switch Autostart (Administrator) &lt;SID&gt;“ an und schaltet den normalen Autostart ab, damit nicht zwei
   Instanzen starten.
3. Die Frage „Jetzt gleich mit Administratorrechten neu starten?“ mit *Ja* beantworten. Ab dann zeigt das Menü
   „IP-Switch (Administrator)“, und Profile werden ohne Abfrage angewendet. Das gilt auch nach jeder weiteren
   Anmeldung.

Nur für die laufende Sitzung genügt *Einstellungen → Jetzt mit Administratorrechten neu starten* (eine UAC-Abfrage).

Zum Abschalten den Haken wieder entfernen (eine UAC-Abfrage). Danach ist der normale Autostart wieder aktiv. Wird
*Mit Windows starten* abgeschaltet, entfernt das auch den Start mit Administratorrechten.

Hinweise:
- Die Option ist für Standardkonten ausgegraut. Dort würde die Aufgabe ohnehin nur mit Standardrechten starten.
- Die Anwendung läuft dann dauerhaft mit erhöhten Rechten. Im Unternehmen sollte das mit der IT abgestimmt werden.
- Die geplante Aufgabe gehört nicht zum MSI. Vor einer Deinstallation per MSI die Option abschalten oder die Aufgabe
  anschließend in der *Aufgabenplanung* löschen. `uninstall.ps1` bzw. `Deinstallieren.cmd` entfernen sie automatisch.
- Trägt ein MSI-Update den normalen Autostart wieder ein, entfernt IP-Switch ihn beim nächsten Start selbst.

## Hinweis zu SmartScreen

Die Dateien sind nicht digital signiert. Beim ersten Start kann Windows SmartScreen „Der Computer wurde durch Windows
geschützt“ anzeigen. Über *Weitere Informationen → Trotzdem ausführen* lässt sich die Anwendung starten. Für die
Verteilung im Unternehmen sollten EXE und MSI signiert werden (siehe [BUILD.md](BUILD.md#code-signatur-empfohlen-für-die-verteilung)).

## Erste Schritte

1. Tray-Symbol anklicken → *Verwaltung öffnen…*
2. Optional Beispielprofile importieren: *Importieren…* → `samples\beispielprofile.json` im Installationsordner.
3. Eigenes Profil anlegen: *Neu…*. Alternativ links einen Adapter wählen → *Als Profil speichern…*, um die aktuelle
   Konfiguration zu übernehmen.
4. Im Profil einen Netzwerkadapter zuordnen. Das Profil erscheint dann im Tray-Menü als Schnellzugriff.
5. Profil anwenden. Windows fragt per UAC nach einer Bestätigung. Das Ergebnis wird als Benachrichtigung angezeigt.

## Manueller Funktionstest

Empfohlen nach der Installation an einem Adapter, der gerade nicht für die Verbindung benötigt wird, z. B. einem
USB-Ethernet-Adapter ohne Kabel:

1. Adapter wählen → *Als Profil speichern…* → Name „Ausgangszustand“ → *Speichern*.
2. Neues statisches Profil anlegen (z. B. `192.168.250.10/24`, ohne Gateway) und anwenden.
   Erwartung: UAC-Abfrage, danach eine Erfolgsmeldung. In den Details unter *Gespeicherte Konfiguration* steht die
   neue Adresse.
3. *DHCP aktivieren* → Erwartung: In den Details steht *IP-Bezug: DHCP* und *DNS-Server: per DHCP*.
4. *Sicherungen…* → die vorletzte Sicherung wählen → *Wiederherstellen*. Die statische Konfiguration aus Schritt 2
   ist wieder aktiv.
5. Profil „Ausgangszustand“ anwenden, um den ursprünglichen Zustand herzustellen.
6. Die UAC-Abfrage einmal mit *Nein* beantworten → Hinweis „Abgebrochen“, keine Änderung.

Das Protokoll (`%LOCALAPPDATA%\IPSwitch\logs\ip-switch.log`) enthält alle ausgeführten `netsh`-Befehle mit
Ergebnis.

## Fehlerbehebung

| Problem | Lösung |
|---|---|
| Kein Tray-Symbol sichtbar | Pfeil `^` im Infobereich öffnen. Unter *Einstellungen → Personalisierung → Taskleiste → Andere Symbole der Taskleiste* IP-Switch aktivieren. |
| „Abgebrochen – es wurden keine Administratorrechte erteilt“ | UAC-Abfrage mit *Ja* bestätigen bzw. Administrator-Anmeldedaten eingeben. |
| Anwenden schlägt fehl, Konfiguration wurde wiederhergestellt | *Details anzeigen* im Fehlerdialog bzw. das Protokoll prüfen. Häufige Ursachen: Gruppenrichtlinien, VPN-/Sicherheitssoftware, Adapter deaktiviert. |
| Adapter fehlt in der Liste | Nur Adapter mit gebundenem IPv4 werden angezeigt. Virtuelle Hilfsadapter erscheinen erst mit *Einstellungen → Alle Adapter anzeigen*. Deaktivierte Adapter (Geräte-Manager / Netzwerkverbindungen) werden von Windows nicht gemeldet. |
| Zuordnung „(nicht vorhanden)“ | Der Adapter ist entfernt, deaktiviert oder hat nach einer Treiber-Neuinstallation eine neue Kennung und eine geänderte MAC erhalten. Das Profil bearbeiten und den Adapter neu zuordnen. |
