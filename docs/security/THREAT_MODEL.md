# Threat Model & Mitigationen – ProcessMonitorService

---

Leitfragen zum Finden von Bedrohungen: `security/THREAT_MODEL_GUIDE.md` (vom Template gepflegt).
Öffentliches oder potenziell öffentliches Repo: keine internen Hosts, Versionen oder Betroffenheiten konkreter Systeme —
die gehören nach `docs/local/` (`CONVENTIONS_COMMON.md` 3.1).

**Stand:** 2026-10-11 (Init, Code-Analyse ohne Ausführung). Angreifermodell: lokaler Standardbenutzer auf einem
überwachten Rechner (will Rechte ausweiten, Überwachung umgehen oder fremde Daten lesen) sowie Administratoren, die
Logs auswerten (werden Ziel von Inhalten, die Benutzer in Prozessnamen/Kommandozeilen schreiben).
Schützenswert: Integrität des als **LocalSystem** laufenden Dienstes, Vertraulichkeit der Logs (Kommandozeilen, SIDs),
Vollständigkeit der Überwachung.

## 1. Dienst-Binary und Konfiguration aus einem für Nicht-Admins schreibbaren Verzeichnis

**Gefahr:** `install.ps1` registriert den Dienst ohne Prüfung mit dem Pfad aus `-ExePath`, Default = Verzeichnis des
Skripts (`install.ps1` Zeile 2, `sc.exe create … binPath=` Zeile 154); Dienstkonto ist mangels `obj=` LocalSystem.
Wird `install.ps1` direkt aus dem Build-/Publish-Ordner unter einem Benutzerprofil oder Entwicklerlaufwerk ausgeführt
(README „Dienst installieren“ beschreibt genau das), kann jeder Benutzer mit Schreibrecht dort
`ProcessMonitorService.exe`, eine der mitgelieferten DLLs oder `appsettings.json` austauschen → Codeausführung als
LocalSystem beim nächsten Dienststart bzw. Abschalten der Überwachung über die zur Laufzeit neu geladene
`appsettings.json` (`Program.cs` Zeile 62, `reloadOnChange: true`). Der Remote-Weg (`Deploy-ProcessMonitorService.ps1`)
installiert nach `C:\Program Files\ProcessMonitorService` und ist davon nicht betroffen.

**Mitigation:**
- I2: `install.ps1` bricht ab, wenn das Binary-Verzeichnis für andere als Administratoren/SYSTEM/TrustedInstaller
  schreibbar ist; README empfiehlt `C:\Program Files\ProcessMonitorService`.
- Positiv: Der `binPath` ist korrekt in Anführungszeichen gesetzt (kein „Unquoted Service Path“).

**Restrisiko:** Hoch – bis I2 umgesetzt ist; danach Gering (Rest: Administratoren, die die Prüfung bewusst umgehen).

---

## 2. Datenverzeichnis: Besitzübernahme, ACL erst nach dem ersten Dienststart

**Gefahr:** `install.ps1` startet den Dienst (Zeile 173) **bevor** `Set-SecureACLs.ps1` läuft (Zeile 219–230; ein Fehler
dort ist nur eine Warnung). Bis dahin erbt `C:\ProgramData\ProcessMonitorService\logs` die Standard-ACL von
`C:\ProgramData`, unter der Standardbenutzer lesen und eigene Unterordner anlegen dürfen. `Set-SecureACLs.ps1` entfernt
die Vererbung (Zeile 20), setzt aber **keinen Besitzer** (Zeile 13–33). Legt ein Standardbenutzer das Verzeichnis vor der
Installation an, bleibt er Besitzer und kann die DACL jederzeit zurückändern → Logs mit Kommandozeilen anderer Benutzer
lesen, Logs löschen/verfälschen; mit einer Verzeichnisverbindung (Junction) statt `logs` ließe sich zudem der als
LocalSystem schreibende Dienst auf ein fremdes Verzeichnis umlenken (Dateien mit teilweise vom Angreifer gewählten
Inhalten). Das Muster ist bekannt, für dieses Projekt aber nicht praktisch nachgestellt.

**Mitigation:**
- I2: Verzeichnis vor dem ersten Start anlegen, Besitzer explizit Administratoren (`S-1-5-32-544`), Abbruch bei
  fremdem Besitzer oder Reparse-Point; ACL-Fehler bricht die Installation ab.
- Positiv: Ziel-ACL selbst ist restriktiv (nur Administratoren + SYSTEM, Vollzugriff, vererbt).

**Restrisiko:** Mittel – Ausnutzung braucht lokalen Zugriff vor der Installation; nach I2 Gering.

---

## 3. Secrets und personenbezogene Daten in protokollierten Kommandozeilen

**Gefahr:** `LogProcessEvent` (`ProcessMonitorWorker.cs` Zeile 526–539) schreibt `CommandLine` und `ExecutablePath`
jedes überwachten Prozesses ungefiltert ins JSON-Log; `UserSid` ordnet sie einer Person zu. Die Default-Filter in
`appsettings.json` umfassen `powershell.exe` und `cmd.exe` — genau dort stehen typischerweise Kennwörter, Tokens oder
Connection-Strings als Argumente (`-Password …`, `/p:…`, `net use … <Kennwort>`). Aufbewahrung bis 30 Dateien à 10 MB;
`process-JSONFiles.ps1 -Export` erzeugt zusätzlich CSV-Kopien im **aktuellen Verzeichnis** ohne ACL-Schutz.
Zugleich ist das Log ein Nutzungsprotokoll je Benutzer (Datenschutz/Mitbestimmung, `security/DATA_HANDLING.md`).

**Mitigation:**
- Positiv: Datenverzeichnis nur für Administratoren + SYSTEM (nach Bedrohung 2 korrekt angelegt).
- I5: Maskierung bekannter Secret-Muster vor dem Logging, Option zum Abschalten der Kommandozeilen-Protokollierung.
- Betrieb: Include-Filter auf das Nötige beschränken; CSV-Exporte nur in geschützte Verzeichnisse und nach Gebrauch löschen.

**Restrisiko:** Mittel – Maskierung per Muster ist nie vollständig; Administratoren sehen die Daten bestimmungsgemäß.

---

## 4. WQL-Bedingung aus Filtermustern: nicht dokumentkonformes Escaping

**Gefahr:** `BuildProcessFilter` (`ProcessMonitorWorker.cs` Zeile 246–311) setzt die Filtermuster aus der Konfiguration
in die WQL-Ereignisabfrage ein; `EscapeWqlString` (Zeile 238–245) verdoppelt Apostrophe (`''`) und verdoppelt
Backslashes. Laut Microsoft-Doku zur WMI-WHERE-Klausel werden Backslash und Anführungszeichen in WQL-Strings mit
Backslash maskiert (`\'`); eine Verdopplung `''` ist dort nicht vorgesehen. Im `LIKE`-Muster sind `[ ]`, `^`, `%` und `_`
Metazeichen; ein wörtliches `_` muss laut Doku in eckige Klammern (`[_]`). Ob die zusätzliche Backslash-Verdopplung
innerhalb von `LIKE` korrekt ist, ist ungeprüft. Ein Muster mit Apostroph kann die Abfrage
abbrechen lassen (Watcher-Start schlägt fehl → Dienst stoppt, K10) oder ihre Bedeutung verändern; `_` im Namen
(z. B. `my_tool.exe` mit Wildcard) wirkt als Einzelzeichen-Joker. Eingabequelle ist nur `appsettings.json` — relevant,
sobald Bedrohung 1 greift oder Filterlisten aus anderen Quellen übernommen werden.

**Mitigation:**
- Positiv: `ShouldMonitorProcess` (Zeile 461–524) prüft jedes Ereignis zusätzlich im Code (Regex mit `Regex.Escape`),
  eine zu weite WQL-Bedingung führt also nicht zu falsch protokollierten Prozessen.
- I4: Escaping korrigieren (Test zuerst), Muster beim Laden validieren (erlaubte Zeichen, Länge).

**Restrisiko:** Gering – Eingabe nur durch Administratoren, Nachprüfung im Code vorhanden.

---

## 5. Formel-Injection (CSV-Injection) beim Export der Log-Analyse

**Gefahr:** `process-JSONFiles.ps1 -Export` (Zeile 171–175) schreibt `ProcessName`, `ExecutablePath` und `CommandLine`
unverändert per `Export-Csv`. Diese Felder kontrolliert jeder lokale Benutzer (Dateiname, Argumente). Beginnt eine
Zelle mit `=`, `+`, `-`, `@` (oder Tab/CR), interpretiert eine Tabellenkalkulation sie als Formel — ein Administrator,
der den Export öffnet, kann so Formeln eines Angreifers ausführen bzw. Daten aus der Tabelle abfließen lassen
(OWASP „CSV Injection“).

**Mitigation:**
- I7: betroffene Zellen neutralisieren (vorangestelltes `'` bzw. Tab gemäß OWASP), Test zuerst.
- Bis dahin: Export nicht direkt in Excel öffnen, sondern als Text importieren.

**Restrisiko:** Mittel – Angriff ist trivial, setzt aber das Öffnen des Exports in einer Tabellenkalkulation voraus.

---

## 6. Umgehung der Überwachung

**Gefahr:** Die Überwachung lässt sich ohne Adminrechte unterlaufen:
(a) **Namenswahl** — Filter prüfen nur `Win32_Process.Name`; ein Benutzer kann ein Programm so benennen, dass es einen
Exclude-Filter trifft (`appsettings.json`: `system*`, `svchost.exe`, `*temp*`) oder keinen Include-Filter.
(b) **Kurzlebige Prozesse** — `__InstanceCreationEvent … WITHIN 1` (Zeile 216–217) ist Polling; Prozesse, die zwischen
zwei Abfragen starten und enden, erscheinen nicht (K1).
(c) **Ausfall** — ein Fehler beim Watcher-Start stoppt den Dienst ohne automatischen Neustart (K10).

**Mitigation:**
- Doku: Scope-Grenze „keine lückenlose Erfassung“ (`architecture/OVERVIEW.md`); Exclude-Filter nur mit vollständigem
  Namen statt breiter Wildcards; für Sicherheitsüberwachung `ExecutablePath` mit auswerten.
- I6: Dienst-Wiederherstellung; Backlog: ereignisbasierte Quelle (ETW/Trace-Klassen) statt Polling.

**Restrisiko:** Hoch für den Einsatz als Sicherheits-/Forensikwerkzeug; Gering für Nutzungsstatistik (Zweck laut README).

---

## 7. Veraltete Laufzeit im self-contained Deployment

**Gefahr:** Das Projekt zielt auf `net9.0-windows` mit Paketen 9.0.6 (Juni 2025) und wird self-contained veröffentlicht
— die mitgelieferte Runtime wird nicht über Windows/Microsoft Update gepatcht. .NET 9 erhält nach dem **2026-11-10**
keine Sicherheitsupdates mehr; seit 9.0.6 sind mehrere Patch-Releases mit CVE-Fixes erschienen (zuletzt 9.0.20 am
2026-09-08). Ein als LocalSystem laufender Dienst mit veralteter Runtime vergrößert die Angriffsfläche
(`security/DEPENDENCY_AUDIT.md` → Befunde).

**Mitigation:**
- I3: Migration auf .NET 10 LTS (Support bis November 2028), Pakete auf aktuelle Patch-Stände.
- Betrieb: nach jedem .NET-Patch-Day neu bauen und verteilen (`operations/DEPLOYMENT.md`).

**Restrisiko:** Mittel – keine bekannte Schwachstelle in den genutzten Komponenten belegt, aber Patchstand unbekannt
(hängt vom Build-SDK ab) und Support-Ende in vier Wochen.

---

## Geprüft und verworfen

- **Secrets im Repository:** keine gefunden (alle versionierten Dateien gelesen; `git log -p --all` nach Mustern wie password, secret, token, apikey, connectionstring, private key durchsucht — einziger Treffer ein Kommentar in `.gitignore`; `UserSecretsId` ist kein Secret).
- **Credentials im Deploy-Skript:** werden interaktiv abgefragt bzw. als `PSCredential` übergeben, nicht gespeichert
  oder geloggt.
- **Log-Injection in die JSON-Datei:** `CompactJsonFormatter` maskiert Sonderzeichen; Zeilenumbrüche in
  Kommandozeilen erzeugen keine gefälschten Einträge.
- **Prompt-Injection:** Das Projekt verarbeitet keine Inhalte mit LLMs; im analysierten Code und README wurden keine an
  Agenten gerichteten Anweisungen gefunden.

---

## Hinweise

- Dieses Dokument wird bei jeder neuen Funktion geprüft, die externe Eingaben verarbeitet
  oder auf sensible Ressourcen zugreift — anhand der Leitfragen in `security/THREAT_MODEL_GUIDE.md`.
- Neue Bedrohungen als ADR dokumentieren wenn sie eine Architekturentscheidung auslösen.
- Quellen der Web-Recherche (2026-10-11): `security/DEPENDENCY_AUDIT.md` → Befunde.
