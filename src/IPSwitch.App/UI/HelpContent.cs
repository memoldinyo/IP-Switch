using IPSwitch.App.Infrastructure;

namespace IPSwitch.App.UI;

/// <summary>Baustein eines Hilfethemas.</summary>
internal abstract record HelpBlock;

internal sealed record HelpHeading(string Text) : HelpBlock;

internal sealed record HelpParagraph(string Text) : HelpBlock;

internal sealed record HelpBullets(params string[] Items) : HelpBlock;

/// <summary>Zweispaltige Tabelle, z. B. Tastenkürzel oder Felder.</summary>
internal sealed record HelpTable(params (string Key, string Value)[] Rows) : HelpBlock;

internal sealed record HelpTopic(string Id, string Title, string Glyph, IReadOnlyList<HelpBlock> Blocks);

/// <summary>
/// Inhalt der eingebauten Hilfe (entspricht inhaltlich dem Benutzerhandbuch). Hilfe und Handbuch gibt es auf
/// Deutsch und Englisch; für alle anderen Oberflächensprachen wird die englische Fassung angezeigt.
/// </summary>
internal static partial class HelpContent
{
    public const string Overview = "overview";
    public const string Apply = "apply";
    public const string About = "about";

    /// <summary>true = deutsche Hilfe und deutsches Handbuch, sonst Englisch.</summary>
    public static bool UseGerman => IPSwitch.Core.Localization.Loc.Current == "de";

    public static IReadOnlyList<HelpTopic> Topics(string dataDirectory) =>
        UseGerman ? GermanTopics(dataDirectory) : EnglishTopics(dataDirectory);

    private static IReadOnlyList<HelpTopic> GermanTopics(string dataDirectory) =>
    [
        new(Overview, "Überblick", Modern.Glyphs.Info,
        [
            new HelpParagraph($"IP-Switch speichert IPv4-Konfigurationen von Netzwerkadaptern als benannte Profile und wendet sie per Klick an – statisch oder per DHCP. Entwickelt von {AppInfo.Author}."),
            new HelpHeading("Das Wichtigste in Kürze"),
            new HelpBullets(
                "IP-Switch läuft im Infobereich der Taskleiste (Tray). Ein Klick auf das Symbol öffnet das Menü.",
                "Profile enthalten IP-Adresse, Subnetzmaske, Gateway, DNS-Server oder DHCP.",
                "Profile lassen sich einem Adapter zuordnen und erscheinen dann im Tray als Schnellzugriff.",
                "Vor jeder Änderung wird die bisherige Konfiguration gesichert. Schlägt das Anwenden fehl, wird sie automatisch wiederhergestellt.",
                "Administratorrechte werden nur für die Änderung selbst angefordert (UAC).",
                "Alle Daten bleiben lokal auf diesem Rechner – keine Cloud, keine Telemetrie."),
            new HelpHeading("Das Verwaltungsfenster"),
            new HelpTable(
                ("Oben links", "Titel und Aktionen: Neues Profil, Bearbeiten, Duplizieren, Löschen, Zahnrad (Import, Export, Hilfe)."),
                ("Links", "Profile als Kacheln mit Suchfeld. Das ⋮-Menü jeder Kachel enthält alle Profilaktionen."),
                ("Rechts", "„Profil zuweisen auf:“ – Zieladapter, aktuelle Konfiguration, DHCP aktivieren, Profil jetzt anwenden."),
                ("Unten", "Alle Netzwerkadapter als Kacheln. Ein Klick wählt den Zieladapter.")),
        ]),
        new("start", "Erste Schritte", Modern.Glyphs.Play,
        [
            new HelpBullets(
                "1. Tray-Symbol anklicken → „Verwaltung öffnen …“.",
                "2. Unten den Adapter wählen, den Sie konfigurieren möchten.",
                "3. Rechts „Als Profil speichern“ wählen, um den aktuellen Zustand als Profil „Ausgangszustand“ festzuhalten.",
                "4. „Neues Profil“ → Name, IP-Adresse, Subnetzmaske usw. eintragen → Speichern.",
                "5. Profil links auswählen → „Profil jetzt anwenden“. Windows fragt ggf. per UAC nach Administratorrechten.",
                "6. Über das ⋮-Menü → „Zuweisen an“ den Adapter festlegen. Das Profil steht dann im Tray-Menü als Schnellzugriff bereit."),
            new HelpParagraph("Tipp: Beispielprofile liegen im Installationsordner unter „samples\\beispielprofile.json“ und lassen sich über das Zahnrad → „Profile importieren …“ laden."),
        ]),
        new("profiles", "Profile anlegen und bearbeiten", Modern.Glyphs.Edit,
        [
            new HelpParagraph("Neue Profile entstehen über „Neues Profil“, „+ Neu“ oder „Als Profil speichern“ (übernimmt die Werte des gewählten Adapters). Bearbeiten per Doppelklick, Enter oder ⋮ → „Bearbeiten …“."),
            new HelpHeading("Felder"),
            new HelpTable(
                ("Profilname", "Pflicht, eindeutig, höchstens 64 Zeichen."),
                ("Beschreibung", "Optional, höchstens 500 Zeichen."),
                ("Netzwerkadapter", "Optionale Zuordnung (siehe „Adapter zuordnen“)."),
                ("IP-Bezug", "„Automatisch (DHCP)“ oder „Folgende IP-Adresse verwenden“."),
                ("IPv4-Adresse", "Bei statischer Konfiguration Pflicht, z. B. 192.168.1.10."),
                ("Subnetzmaske / Präfix", "255.255.255.0, 24 oder /24 – alle drei Schreibweisen sind erlaubt."),
                ("Standardgateway", "Optional; muss im selben Subnetz wie die IP-Adresse liegen."),
                ("DNS-Server", "Bevorzugt und alternativ. Bei DHCP leer lassen, um auch DNS per DHCP zu beziehen.")),
            new HelpHeading("Prüfungen"),
            new HelpParagraph("Eingaben werden schon während der Eingabe geprüft. Ein rotes Symbol neben dem Feld zeigt den Fehler, z. B. ungültige Adresse, Netz- oder Broadcast-Adresse, nicht zusammenhängende Subnetzmaske, Gateway außerhalb des Subnetzes, doppelte DNS-Server oder ein bereits vergebener Profilname."),
            new HelpParagraph("„Speichern und anwenden“ speichert das Profil und wendet es sofort auf den zugeordneten Adapter an."),
        ]),
        new(Apply, "Profile anwenden", Modern.Glyphs.Accept,
        [
            new HelpHeading("Im Verwaltungsfenster"),
            new HelpParagraph("Profil links auswählen, Zieladapter unten oder rechts wählen und „Profil jetzt anwenden“ klicken. Ist das Profil einem anderen Adapter zugeordnet, fragt IP-Switch nach."),
            new HelpHeading("Im Tray-Menü"),
            new HelpBullets(
                "Schnellzugriff: Profile mit Adapterzuordnung stehen ganz oben („Profil → Adapter“) – ein Klick wendet an.",
                "Pro Adapter: Untermenü mit Status, zugeordneten Profilen, „Anderes Profil anwenden“ und „DHCP aktivieren“."),
            new HelpHeading("DHCP aktivieren"),
            new HelpParagraph("Stellt IP-Adresse und DNS-Server des Zieladapters auf automatischen Bezug um. Dabei werden auch alte statische Adressen und Gateways entfernt, die Windows sonst stehen lassen würde."),
            new HelpHeading("Was beim Anwenden passiert"),
            new HelpBullets(
                "Die Eingaben werden erneut geprüft, auch ob die Adresse bereits auf einem anderen Adapter eingetragen ist.",
                "Die aktuelle Konfiguration wird gesichert – ohne Sicherung keine Änderung.",
                "Die Änderung wird ausgeführt und anschließend überprüft.",
                "Bei einem Fehler wird die Sicherung automatisch zurückgespielt. „Details anzeigen“ im Fehlerdialog listet alle Schritte."),
        ]),
        new("assign", "Adapter zuordnen", Modern.Glyphs.Link,
        [
            new HelpParagraph("Eine Zuordnung legt fest, für welchen Adapter ein Profil gedacht ist. Zugeordnete Profile erscheinen im Tray als Schnellzugriff, und beim Auswählen des Profils wird der passende Adapter automatisch als Ziel gesetzt."),
            new HelpBullets(
                "Im Verwaltungsfenster: ⋮ an der Profilkachel → „Zuweisen an“ → Adapter oder „Keine Zuordnung“.",
                "Im Tray: „Schnellzugriff einrichten“ → Profil → Adapter.",
                "Im Profileditor: Feld „Netzwerkadapter“."),
            new HelpParagraph("Die Zuordnung erfolgt über die interne Kennung des Adapters. Benennen Sie einen Adapter in Windows um, bleibt sie erhalten. Erhält ein Adapter nach einer Treiber-Neuinstallation eine neue Kennung, wird er über seine MAC-Adresse wiedererkannt."),
        ]),
        new("backups", "Sicherungen", Modern.Glyphs.History,
        [
            new HelpParagraph("Vor jeder Änderung speichert IP-Switch die bisherige Konfiguration des Adapters (bis zu 20 Sicherungen je Adapter)."),
            new HelpBullets(
                "Verwaltungsfenster: rechts „Sicherungen“ → Sicherung wählen → „Wiederherstellen“.",
                "Tray: Adapter-Untermenü → „Letzte Sicherung wiederherstellen“."),
            new HelpParagraph("Wiederherstellen ist selbst eine Änderung und erzeugt wiederum eine Sicherung – ein versehentliches Zurückspielen lässt sich also ebenfalls rückgängig machen."),
        ]),
        new("admin", "Administratorrechte", Modern.Glyphs.Shield,
        [
            new HelpParagraph("Netzwerkeinstellungen dürfen in Windows nur mit Administratorrechten geändert werden. IP-Switch selbst läuft mit Standardrechten und fordert sie nur für die Änderung an (UAC-Abfrage). Wird die Abfrage abgelehnt, ändert sich nichts."),
            new HelpHeading("Ohne ständige UAC-Abfragen"),
            new HelpParagraph("Tray → Einstellungen → „Mit Administratorrechten starten (ohne UAC-Abfragen)“ richtet eine geplante Aufgabe ein, die IP-Switch bei der Anmeldung mit Administratorrechten startet (einmalige UAC-Bestätigung). Danach werden Profile ohne Abfrage angewendet. Für die laufende Sitzung genügt „Jetzt mit Administratorrechten neu starten“."),
            new HelpParagraph("Hinweis: Die Anwendung läuft dann dauerhaft mit erhöhten Rechten. Die Option ist nur für Administratorkonten verfügbar."),
        ]),
        new("transfer", "Import und Export", Modern.Glyphs.Export,
        [
            new HelpBullets(
                "Zahnrad oder ⋯ → „Profile importieren …“ / „Alle Profile exportieren …“.",
                "Einzelnes Profil: ⋮ an der Kachel → „Exportieren …“."),
            new HelpParagraph("Exportdateien sind JSON-Dateien. Beim Import werden ungültige Einträge mit Begründung übersprungen und Namenskonflikte durch Umbenennen gelöst („Name (2)“)."),
        ]),
        new("settings", "Einstellungen", Modern.Glyphs.Settings,
        [
            new HelpParagraph("Alle Einstellungen finden Sie im Tray-Menü unter „Einstellungen“."),
            new HelpTable(
                ("Alle Adapter anzeigen", "Zeigt auch virtuelle Hilfsadapter (Wi-Fi Direct, WAN Miniport usw.)."),
                ("Erfolgsmeldungen anzeigen", "Benachrichtigung nach erfolgreichem Anwenden."),
                ("Mit Windows starten", "Startet IP-Switch bei der Anmeldung."),
                ("Mit Administratorrechten starten", "Siehe „Administratorrechte“."),
                ("Darstellung", "Wie Windows, Hell oder Dunkel."),
                ("Sprache", "Wie Windows oder eine von acht Sprachen: Deutsch, English, Türkçe, Français, Italiano, العربية (von rechts nach links), 中文, Русский. Hilfe und Handbuch gibt es auf Deutsch und Englisch."),
                ("Datenordner öffnen", $"Öffnet {dataDirectory}.")),
        ]),
        new("keys", "Tastenkürzel", Modern.Glyphs.Info,
        [
            new HelpTable(
                ("F1", "Diese Hilfe öffnen"),
                ("F5", "Adapter neu einlesen"),
                ("Strg+N", "Neues Profil"),
                ("Strg+F", "Profile durchsuchen"),
                ("Pfeiltasten", "Zwischen Profilkacheln wechseln"),
                ("Enter / Doppelklick", "Profil bearbeiten"),
                ("Entf", "Profil löschen"),
                ("Kontextmenütaste / Umschalt+F10", "⋮-Menü des Profils öffnen")),
        ]),
        new("trouble", "Fehlerbehebung", Modern.Glyphs.Warning,
        [
            new HelpTable(
                ("Kein Tray-Symbol", "Pfeil ^ im Infobereich öffnen; unter Windows-Einstellungen → Taskleiste → Andere Symbole IP-Switch aktivieren."),
                ("„Abgebrochen – keine Administratorrechte“", "Die UAC-Abfrage wurde abgelehnt. Erneut anwenden und mit „Ja“ bestätigen."),
                ("„Adresse bereits auf anderem Adapter eingetragen“", "Windows erlaubt dieselbe IP nicht auf zwei Adaptern. Auf dem genannten Adapter zuerst „DHCP aktivieren“."),
                ("Anwenden fehlgeschlagen, Konfiguration wiederhergestellt", "„Details anzeigen“ im Dialog und das Protokoll prüfen. Häufige Ursachen: Gruppenrichtlinien, VPN- oder Sicherheitssoftware."),
                ("Adapter fehlt", "Nur Adapter mit IPv4 werden angezeigt; deaktivierte Adapter meldet Windows nicht. Virtuelle Adapter: Einstellungen → Alle Adapter anzeigen."),
                ("Zuordnung „(nicht vorhanden)“", "Adapter entfernt oder neu installiert – Profil über ⋮ → „Zuweisen an“ neu zuordnen.")),
            new HelpParagraph($"Das Protokoll mit allen ausgeführten Befehlen liegt unter {Path.Combine(dataDirectory, "logs")}."),
        ]),
        new(About, "Über IP-Switch", Modern.Glyphs.Info,
        [
            new HelpHeading($"{AppInfo.Name} {AppInfo.Version}"),
            new HelpParagraph($"Entwickelt von {AppInfo.Author}."),
            new HelpParagraph("IP-Switch ist eine Windows-Anwendung zur Verwaltung von IPv4-Konfigurationen für Netzwerkadapter."),
            new HelpHeading("Datenschutz"),
            new HelpBullets(
                $"Profile, Einstellungen, Sicherungen und Protokoll liegen ausschließlich lokal unter {dataDirectory}.",
                "Keine Cloud-Anbindung, keine Telemetrie, keine Datenübertragung."),
            new HelpParagraph(AppInfo.Copyright),
        ]),
    ];
}
