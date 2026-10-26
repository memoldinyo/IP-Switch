# IP-Switch – Benutzerhandbuch

**Version 1.4** · Autor: **Mehmet Aslandag** · [English version](USER_MANUAL.md)

IP-Switch speichert IPv4-Konfigurationen von Netzwerkadaptern als benannte Profile und wendet sie per Klick an – mit
statischer Adresse oder per DHCP. Vor jeder Änderung wird die bisherige Konfiguration gesichert; schlägt eine
Änderung fehl, stellt IP-Switch den vorherigen Zustand automatisch wieder her.

Die Hilfe ist auch in der Anwendung verfügbar: **F1** im Verwaltungsfenster, das **?** oben rechts in der Kopfkarte
oder im Tray-Menü **Hilfe**.

---

## Inhalt

1. [Überblick](#1-überblick)
2. [Erste Schritte](#2-erste-schritte)
3. [Das Tray-Menü](#3-das-tray-menü)
4. [Das Verwaltungsfenster](#4-das-verwaltungsfenster)
5. [Profile anlegen und bearbeiten](#5-profile-anlegen-und-bearbeiten)
6. [Profile anwenden](#6-profile-anwenden)
7. [Adapter zuordnen und Schnellzugriff](#7-adapter-zuordnen-und-schnellzugriff)
8. [Sicherungen und Wiederherstellung](#8-sicherungen-und-wiederherstellung)
9. [Administratorrechte](#9-administratorrechte)
10. [Import und Export](#10-import-und-export)
11. [Einstellungen](#11-einstellungen)
12. [Tastenkürzel](#12-tastenkürzel)
13. [Fehlerbehebung](#13-fehlerbehebung)
14. [Datenschutz und Speicherorte](#14-datenschutz-und-speicherorte)
15. [Über IP-Switch](#15-über-ip-switch)

---

## 1. Überblick

| Begriff | Bedeutung |
|---|---|
| **Profil** | Benannte IPv4-Konfiguration: IP-Adresse, Subnetzmaske, Gateway, DNS-Server – oder DHCP. |
| **Adapter** | Netzwerkanschluss des Rechners, z. B. Ethernet, WLAN, USB-Dock oder virtueller Adapter. |
| **Zuordnung** | Verknüpfung eines Profils mit einem Adapter. Zugeordnete Profile erscheinen im Tray als Schnellzugriff. |
| **Zieladapter** | Der Adapter, auf den ein Profil im Verwaltungsfenster angewendet wird. |
| **Sicherung** | Automatisch gespeicherter Zustand eines Adapters vor jeder Änderung. |

IP-Switch läuft dauerhaft und ressourcenschonend im Infobereich der Taskleiste (Tray). Das Verwaltungsfenster wird
nur bei Bedarf geöffnet.

## 2. Erste Schritte

1. **IP-Switch starten.** Nach der Installation startet IP-Switch automatisch. Das Symbol **IP** erscheint im
   Infobereich – ggf. über den Pfeil **^** neben der Uhr einblenden und per Drag & Drop dauerhaft anheften.
2. **Verwaltung öffnen:** Tray-Symbol anklicken → **Verwaltung öffnen …**
3. **Ausgangszustand sichern:** Unten in der Adapterleiste den gewünschten Adapter anklicken, rechts
   **Als Profil speichern** wählen und z. B. „Ausgangszustand“ nennen.
4. **Eigenes Profil anlegen:** **Neues Profil** → Name und Werte eintragen → **Speichern**.
5. **Anwenden:** Profil links auswählen → **Profil jetzt anwenden**. Windows fragt ggf. per UAC nach
   Administratorrechten.
6. **Zuordnen:** Über **⋮** an der Profilkachel → **Zuweisen an** → Adapter. Ab jetzt reicht ein Klick im Tray-Menü.

> **Tipp:** Beispielprofile liegen im Installationsordner unter `samples\beispielprofile.json`. Sie lassen sich über das
> Zahnrad → **Profile importieren …** laden.

## 3. Das Tray-Menü

Ein Klick (links oder rechts) auf das Tray-Symbol öffnet das Menü:

```
IP-Switch
──────────────────────────────
Daimler  →  WLAN                         ← Schnellzugriff: ein Klick wendet an
Schnellzugriff einrichten            ▸   Profil ▸ Adapter zuordnen
──────────────────────────────
● WLAN   192.168.20.122/24 (DHCP)    ▸   Status, IP, Gateway, DNS
● Ethernet 10.0.0.5/24 (Statisch)    ▸     Profil „…“ anwenden
Weitere Adapter (7)                  ▸     Anderes Profil anwenden ▸
──────────────────────────────             DHCP aktivieren (IP und DNS)
Verwaltung öffnen …                        Aktuelle Konfiguration als Profil speichern …
Neues Profil …                             Letzte Sicherung wiederherstellen
──────────────────────────────
Einstellungen                        ▸
Hilfe
Über IP-Switch
──────────────────────────────
Beenden
```

- **Schnellzugriff:** Profile mit Adapterzuordnung stehen ganz oben. Ein Klick wendet das Profil an.
- **Adapter-Untermenüs:** Verbundene Adapter und Adapter mit zugeordneten Profilen stehen direkt im Menü, alle
  weiteren unter **Weitere Adapter**.
- Das Tooltip des Tray-Symbols zeigt die Adressen der verbundenen Adapter.

## 4. Das Verwaltungsfenster

| Bereich | Inhalt |
|---|---|
| **Kopfkarte (oben links)** | Titel, **Neues Profil**, Bearbeiten, Duplizieren, Löschen, Zahnrad (Import, Export, Datenordner, Hilfe, Über) und **?** (Hilfe). |
| **Profilzeile** | Anzahl der Profile, Suchfeld, **+ Neu** und **⋯** (Import, Export, Datenordner). |
| **Profilkacheln (links)** | Jede Kachel zeigt IP/Präfix, Gateway, Modus, DNS und – falls vorhanden – den zugeordneten Adapter. Rechts unten öffnet **⋮** das Profilmenü. |
| **Profil zuweisen auf: (rechts)** | Zieladapter, **Aktualisieren**, aktuelle Adapterkonfiguration (mit **Details**, **Als Profil speichern**, **Sicherungen**), **DHCP aktivieren** und **Profil jetzt anwenden**. |
| **Adapter (unten)** | Alle Adapter als Kacheln mit Status, Adresse und Modus. Ein Klick wählt den Zieladapter. Weitere Adapter stehen hinter **+ N weitere**. |

Das **⋮-Menü** einer Profilkachel (auch per Rechtsklick oder Kontextmenütaste) enthält:

- **Auf „Zieladapter“ anwenden**
- **Zuweisen an ▸** – Adapter wählen oder **Keine Zuordnung**
- **Bearbeiten …**, **Duplizieren**, **Exportieren …**
- **Löschen**

Das Fenster passt sich an: Bei geringer Breite werden die Profilkacheln einspaltig und die Schaltflächen der
Kopfkarte rutschen unter den Titel. **DHCP aktivieren** und **Profil jetzt anwenden** bleiben immer sichtbar.

## 5. Profile anlegen und bearbeiten

Neue Profile entstehen über **Neues Profil**, **+ Neu** oder **Als Profil speichern** (übernimmt die Werte des
gewählten Adapters). Bearbeiten per Doppelklick, **Enter** oder **⋮ → Bearbeiten …**.

| Feld | Pflicht | Hinweise |
|---|---|---|
| Profilname | ja | Eindeutig, höchstens 64 Zeichen. |
| Beschreibung | nein | Höchstens 500 Zeichen; erscheint auf der Kachel. |
| Netzwerkadapter | nein | Zuordnung für den Schnellzugriff. |
| IP-Bezug | ja | **Automatisch (DHCP)** oder **Folgende IP-Adresse verwenden**. |
| IPv4-Adresse | bei statisch | z. B. `192.168.1.10`. |
| Subnetzmaske / Präfix | bei statisch | `255.255.255.0`, `24` oder `/24`. Rechts daneben wird die jeweils andere Schreibweise angezeigt. |
| Standardgateway | nein | Muss im selben Subnetz wie die IP-Adresse liegen. |
| Bevorzugter / alternativer DNS-Server | nein | Bei DHCP leer lassen, um auch DNS per DHCP zu beziehen. |

**Prüfungen während der Eingabe** – ein rotes Symbol neben dem Feld zeigt den Grund, z. B.:

- ungültige Adresse oder führende Nullen (`192.168.01.1`),
- Netz- oder Broadcast-Adresse des Subnetzes,
- nicht zusammenhängende Subnetzmaske (`255.0.255.0`),
- Gateway außerhalb des Subnetzes,
- identische DNS-Server oder alternativer DNS-Server ohne bevorzugten,
- bereits vergebener Profilname.

**Speichern und anwenden** speichert das Profil und wendet es sofort auf den zugeordneten Adapter an.
**F1** im Editor öffnet die Hilfe zu diesem Thema.

## 6. Profile anwenden

**Im Verwaltungsfenster:** Profil auswählen, Zieladapter wählen (unten in der Adapterleiste oder rechts in der
Auswahlliste) und **Profil jetzt anwenden** klicken. Ist das Profil einem anderen Adapter zugeordnet, fragt IP-Switch
nach. Die Statuszeile darunter zeigt vorab, welches Profil auf welchen Adapter angewendet wird.

**Im Tray-Menü:** Schnellzugriff oder Adapter-Untermenü → Profil.

**DHCP aktivieren** stellt IP-Adresse und DNS-Server des Zieladapters auf automatischen Bezug um. Dabei entfernt
IP-Switch auch alte statische Adressen und Gateways, die Windows sonst aktiv lassen würde.

**Ablauf einer Änderung:**

1. Die Werte werden erneut geprüft – auch, ob die Adresse schon auf einem anderen Adapter eingetragen ist (Windows
   erlaubt dieselbe IP nicht auf zwei Adaptern).
2. Die aktuelle Konfiguration wird gesichert. Ohne erfolgreiche Sicherung wird nichts geändert.
3. Die Änderung wird ausgeführt und anschließend überprüft.
4. Bei einem Fehler wird die Sicherung automatisch zurückgespielt. Im Fehlerdialog zeigt **Details anzeigen** alle
   ausgeführten Schritte.

## 7. Adapter zuordnen und Schnellzugriff

Eine Zuordnung legt fest, für welchen Adapter ein Profil gedacht ist:

- **Verwaltungsfenster:** **⋮** an der Profilkachel → **Zuweisen an** → Adapter oder **Keine Zuordnung**.
- **Tray-Menü:** **Schnellzugriff einrichten** → Profil → Adapter.
- **Profileditor:** Feld **Netzwerkadapter**.

Zugeordnete Profile erscheinen im Tray-Menü ganz oben als Schnellzugriff („Profil → Adapter“). Wählen Sie im
Verwaltungsfenster ein zugeordnetes Profil aus, wird der passende Adapter automatisch als Ziel gesetzt.

Die Zuordnung erfolgt über die interne Kennung des Adapters (GUID). Benennen Sie den Adapter in Windows um, bleibt
sie erhalten. Erhält ein Adapter nach einer Treiber-Neuinstallation eine neue Kennung, erkennt IP-Switch ihn über
seine MAC-Adresse wieder. Ein Profil kann genau einem Adapter zugeordnet sein; ein Adapter kann mehrere Profile im
Schnellzugriff haben.

## 8. Sicherungen und Wiederherstellung

Vor jeder Änderung speichert IP-Switch die bisherige Konfiguration des Adapters – bis zu 20 Sicherungen je Adapter.

- **Verwaltungsfenster:** rechts **Sicherungen** → Sicherung wählen → **Wiederherstellen**.
- **Tray-Menü:** Adapter-Untermenü → **Letzte Sicherung wiederherstellen**.

Eine Wiederherstellung ist selbst eine Änderung und erzeugt ebenfalls eine Sicherung. Ein versehentliches
Zurückspielen lässt sich daher wieder rückgängig machen.

## 9. Administratorrechte

Netzwerkeinstellungen dürfen in Windows nur mit Administratorrechten geändert werden. IP-Switch läuft mit
Standardrechten und fordert Administratorrechte nur für die eigentliche Änderung an (UAC-Abfrage). Lehnen Sie die
Abfrage ab, wird nichts geändert.

**Ohne ständige UAC-Abfragen:** Tray → **Einstellungen** → **Mit Administratorrechten starten (ohne UAC-Abfragen)**.

1. Hinweis bestätigen und die UAC-Abfrage einmalig mit **Ja** beantworten. IP-Switch legt die geplante Aufgabe
   „IP-Switch Autostart (Administrator) …“ an und schaltet den normalen Autostart ab.
2. **Jetzt gleich mit Administratorrechten neu starten?** → **Ja**. Das Menü zeigt danach „IP-Switch (Administrator)“.

Für die laufende Sitzung genügt **Einstellungen → Jetzt mit Administratorrechten neu starten**. Die Option ist nur
für Administratorkonten verfügbar. Bitte beachten: Die Anwendung läuft dann dauerhaft mit erhöhten Rechten.

## 10. Import und Export

- **Zahnrad** oder **⋯** → **Profile importieren …** bzw. **Alle Profile exportieren …**
- Einzelnes Profil: **⋮** → **Exportieren …**

Exportdateien sind JSON-Dateien im Format `IPSwitch.Profiles`. Beim Import werden ungültige Einträge mit Begründung
übersprungen, Namenskonflikte durch Umbenennen gelöst („Name (2)“) und Kennungen neu vergeben.

## 11. Einstellungen

Tray-Menü → **Einstellungen**:

| Einstellung | Wirkung |
|---|---|
| Alle Adapter anzeigen | Zeigt auch virtuelle Hilfsadapter (Wi-Fi Direct, WAN Miniport usw.). |
| Erfolgsmeldungen anzeigen | Benachrichtigung nach erfolgreichem Anwenden. |
| Mit Windows starten | Startet IP-Switch bei der Anmeldung. |
| Mit Administratorrechten starten | Siehe [Administratorrechte](#9-administratorrechte). |
| Jetzt mit Administratorrechten neu starten | Neustart mit erhöhten Rechten für die laufende Sitzung. |
| Darstellung | **Wie Windows**, **Hell** oder **Dunkel**. |
| Sprache | **Wie Windows** oder eine von acht Sprachen: Deutsch, English, Türkçe, Français, Italiano, العربية (von rechts nach links), 中文 (vereinfacht), Русский. Hilfe und Handbuch gibt es auf Deutsch und Englisch; bei allen anderen Sprachen wird die englische Fassung angezeigt. |
| Datenordner öffnen | Öffnet `%LOCALAPPDATA%\IPSwitch` im Explorer. |

## 12. Tastenkürzel

| Taste | Funktion |
|---|---|
| **F1** | Hilfe öffnen (im Profileditor: Hilfe zu Profilen) |
| **F5** | Adapter neu einlesen |
| **Strg+N** | Neues Profil |
| **Strg+F** | Profile durchsuchen |
| **Pfeiltasten** | Zwischen Profilkacheln wechseln |
| **Enter** / Doppelklick | Profil bearbeiten |
| **Entf** | Profil löschen |
| **Kontextmenütaste** / **Umschalt+F10** | ⋮-Menü des Profils öffnen |

## 13. Fehlerbehebung

| Problem | Lösung |
|---|---|
| Kein Tray-Symbol sichtbar | Pfeil **^** im Infobereich öffnen; unter *Einstellungen → Personalisierung → Taskleiste → Andere Symbole der Taskleiste* IP-Switch aktivieren. |
| „Abgebrochen – es wurden keine Administratorrechte erteilt“ | Die UAC-Abfrage wurde abgelehnt. Erneut anwenden und mit **Ja** bestätigen. |
| „Die Adresse … ist bereits auf dem Adapter … eingetragen“ | Windows erlaubt dieselbe IP nicht auf zwei Adaptern. Auf dem genannten Adapter zuerst **DHCP aktivieren**. In den Adapterdetails erscheinen solche Reste als „Altlast“. |
| Anwenden fehlgeschlagen, Konfiguration wiederhergestellt | **Details anzeigen** im Fehlerdialog und das Protokoll prüfen. Häufige Ursachen: Gruppenrichtlinien, VPN- oder Sicherheitssoftware, deaktivierter Adapter. |
| Adapter fehlt in der Liste | Nur Adapter mit IPv4 werden angezeigt; deaktivierte Adapter meldet Windows nicht. Virtuelle Adapter: *Einstellungen → Alle Adapter anzeigen*. |
| Zuordnung „(nicht vorhanden)“ | Adapter entfernt oder neu installiert – Profil über **⋮ → Zuweisen an** neu zuordnen. |
| Beschädigte Profildatei | IP-Switch legt eine Kopie `profiles.json.defekt-…` an und startet mit leerer Liste. Profile ggf. aus einem Export wieder importieren. |

Das Protokoll mit allen ausgeführten Befehlen liegt unter `%LOCALAPPDATA%\IPSwitch\logs\ip-switch.log`.

## 14. Datenschutz und Speicherorte

IP-Switch speichert alle Daten ausschließlich lokal. Es gibt keine Cloud-Anbindung, keine Telemetrie und keine
Datenübertragung.

| Pfad | Inhalt |
|---|---|
| `%LOCALAPPDATA%\IPSwitch\profiles.json` | Profile |
| `%LOCALAPPDATA%\IPSwitch\settings.json` | Einstellungen |
| `%LOCALAPPDATA%\IPSwitch\backups\` | Sicherungen |
| `%LOCALAPPDATA%\IPSwitch\logs\` | Protokoll |

Bei einer Deinstallation bleiben diese Daten erhalten. Zum vollständigen Entfernen den Ordner
`%LOCALAPPDATA%\IPSwitch` löschen (bzw. `uninstall.ps1 -RemoveData` beim ZIP-Paket).

## 15. Über IP-Switch

**IP-Switch** – Verwaltung von IPv4-Konfigurationen für Netzwerkadapter.

- **Autor:** Mehmet Aslandag
- **Version:** siehe Tray-Menü → **Über IP-Switch**
- **Lizenz:** Apache License 2.0
- **Technik:** C# / .NET 10, Windows Forms; Konfiguration über `netsh interface ipv4`
- Weitere Dokumentation: [README](../README.md), [Installationsanleitung](INSTALLATION.md), [Build-Anleitung](BUILD.md)

Copyright © 2026 Mehmet Aslandag
