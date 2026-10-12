# Feature: Log-Analyse (`process-JSONFiles.ps1`)

---

## Zweck

Liest die JSON-Logdateien des Dienstes, filtert die Prozessereignisse (`SourceContext` enthält `ProcessMonitorWorker`,
`EventType` `Start`/`Stop`) nach Prozessname, Ereignistyp und Zeitraum und gibt sie als Tabelle mit Statistik, als
Pipeline-Objekte, als CSV-Datei oder als Laufzeitliste (Start→Stop je Prozess) aus. Zeitstempel werden von UTC in
„W. Europe Standard Time“ umgerechnet.

---

## Einstiegspunkte

| Auslöser | Typ | Beschreibung |
|---|---|---|
| `.\process-JSONFiles.ps1` | PowerShell-Skript (Admin-Konsole, PowerShell 7 — K2) | Lesezugriff auf `C:\ProgramData\ProcessMonitorService\logs` braucht Adminrechte |

---

## Eingabe-/Ausgabe-Kontrakt

| Parameter | Typ | Pflicht | Beschreibung |
|---|---|---|---|
| `-LogPath` | `string` | Nein | Dateimuster, Default `C:\ProgramData\ProcessMonitorService\logs\service-*.json` |
| `-ProcessName` | `string` | Nein | Teilstring-Filter (`-like "*<Wert>*"`) auf `ProcessName` |
| `-EventType` | `Start` / `Stop` / `All` | Nein | Default `All` |
| `-Days` | `int` | Nein | Default `1`; filtert **Dateien** nach `LastWriteTime`, nicht Ereignisse nach Zeitstempel |
| `-Export` | `switch` | Nein | schreibt `ProcessEvents_<JJJJMMTT_HHmmss>.csv` ins aktuelle Verzeichnis (UTF-8) |
| `-ForPipeline` | `switch` | Nein | gibt die formatierten Objekte in die Pipeline aus (z. B. `Out-GridView`) |
| `-OnlyProcessRuntimes` | `switch` | Nein | paart Start/Stop je `ProcessId`+`ProcessName` und berechnet `RuntimeInSeconds` |

Vorrang bei mehreren Schaltern: `-Export` vor `-ForPipeline` vor `-OnlyProcessRuntimes` vor Tabellenausgabe.

**Ausgabe:** Objekte mit `Timestamp`, `EventType`, `ProcessName`, `ProcessId`, `UserSid`, `ExecutablePath`,
`CommandLine` (die Felder `ParentName`/`ParentProcessId` des Logs werden **nicht** übernommen). Exit-Code `1`, wenn keine
Logdateien gefunden werden oder ein Fehler auftritt.

---

## Service-Schicht

Keine — einzelnes Skript mit den Funktionen `Get-ProcessEvents`, `Convert-ToGermanTime`, `Format-ProcessEvent`.

---

## Exitcodes / Statuscodes

| Code | Bedeutung |
|---|---|
| `0` | Analyse abgeschlossen (auch bei 0 Treffern) |
| `1` | Keine Logdateien gefunden oder unerwarteter Fehler |

---

## Kritische Patterns

- Feldnamen kommen aus dem Message-Template des Dienstes (`docs/features/PROCESS_MONITORING.md`) — bei Änderungen beide anpassen.
- Sentinel-Werte (`UNKNOWN`, `N/A`, `ERROR_GETTING_SID`) als „unbekannt“ behandeln, nicht als echte SID.
- Exporte enthalten personenbezogene Daten und ggf. Secrets → nur in geschützte Verzeichnisse, nach Gebrauch löschen
  (`docs/security/DATA_HANDLING.md`).

---

## Bekannte Einschränkungen

| Einschränkung | Ursache | Status |
|---|---|---|
| Läuft nicht unter Windows PowerShell 5.1 | `??`-Operator, Emojis ohne BOM (K2) | Geplant I7 |
| CSV-Export anfällig für Formel-Injection | Felder ungefiltert (Bedrohung 5) | Geplant I7 |
| `-Days` wirkt auf Dateien, nicht auf Ereignisse | Filter auf `LastWriteTime` | Akzeptiert |
| Laufzeitpaarung kann bei PID-Wiederverwendung falsch paaren | Gruppierung nach `ProcessId`+`ProcessName` über alle Tage | Akzeptiert |
| Meldung „Verarbeite:“ ohne Dateinamen | `-FilePaths` ist `[string[]]`, Zugriff auf `.Name` liefert nichts | Akzeptiert (kosmetisch) |

---

## Bekannte Fallstricke

- Ohne Adminrechte findet das Skript wegen der ACL keine Dateien und meldet „Keine Log-Dateien gefunden“.
- Die Zeitumrechnung ist fest auf „W. Europe Standard Time“ eingestellt, unabhängig von der Zeitzone des Rechners.

---

## Teststrategie

- Unit (Pester, ab I7): `Format-ProcessEvent` und die CSV-Neutralisierung mit fiktiven CLEF-Zeilen als Fixture
  (keine echten Logs, `docs/security/DATA_HANDLING.md`)
- Integration: Analyse gegen eine auf der Test-VM erzeugte Logdatei
- End-to-End: README-Beispielaufrufe auf der Test-VM
