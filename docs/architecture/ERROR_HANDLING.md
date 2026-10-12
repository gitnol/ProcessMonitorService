# Fehlerbehandlung – ProcessMonitorService

Wo und wie Fehler erkannt, propagiert, behandelt und gemeldet werden.
Konsolidiert Regeln, die sonst in `CONVENTIONS.md`, `principles/FAIL_FAST.md`,
`principles/CONTRACTS.md` und `principles/LOGGING.md` verstreut sind.

---

## Exception-/Fehler-Hierarchie

- **Ist-Stand:** keine projektspezifischen Exceptions. Fehler aus WMI/CIM (`ManagementException`, `CimException`) werden
  an den Boundaries gefangen und geloggt; die CIM-Abfragen liefern bei Fehlern **Sentinel-Strings** statt Exceptions
  (siehe Konvertierungstabelle).
- **Regel für neuen Code:** Sobald eine Stelle einen Fehler gezielt behandeln muss, eine Basis
  `public class ProcessMonitorException : Exception` und spezifische Unterklassen einführen (erster Kandidat:
  Konfigurationsfehler bei der Options-Validierung in I4).
- **Niemals nackte `Exception` werfen.** PowerShell-Skripte: terminierende Fehler per `throw` mit klarer Meldung;
  externe Programme über `$LASTEXITCODE` prüfen.

---

## Boundary vs. interne Fehler

| Ebene | Verhalten im Projekt |
|---|---|
| **Boundary `Main`** (`Program.cs`) | Fehlende Adminrechte → `Log.Fatal`, Programmende (Exit-Code heute `0`, K13); Exception beim Host-Start → `Log.Fatal`, `Environment.ExitCode = 1` |
| **Boundary `ExecuteAsync`** | `OperationCanceledException` → Info „stopping gracefully“; jede andere Exception → `LogCritical` + `throw` → Host stoppt sauber (kein automatischer Dienst-Neustart, K10, I6) |
| **Boundary WMI-Event-Handler / Timer-Callbacks** | Jede Exception wird gefangen und mit `LogError` protokolliert; das einzelne Ereignis bzw. der Timer-Tick geht verloren, der Dienst läuft weiter |
| **Anwendung** (`HandleProcessEventAsync`, Filter) | Wirft nicht nach außen; Fehler pro Ereignis werden lokal geloggt |
| **Infrastruktur** (`ProcessOwnerService`) | CIM-Fehler → Sentinel-String + `LogError`/`LogWarning`; kein Durchreichen der `CimException` |

---

## Was geloggt und was gerethrowt wird

- **Boundary** fängt → loggt → konvertiert in das Ausgabeformat (hier: Log-Eintrag, im Fall `Main` Exit-Code). Das
  Level richtet sich nach der Art des Fehlers (Level-Semantik: `docs/principles/LOGGING.md`):
  - **unerwartet** (Programmierfehler, WMI-Dienst nicht verfügbar) → `ERROR`/`CRITICAL` mit vollem Stacktrace
    (`_logger.LogError(ex, …)`);
  - **erwartet** (Prozess beim SID-Lookup schon beendet, Elternprozess existiert nicht mehr) → `WARNING` oder `DEBUG`
    ohne Stacktrace. **Ist-Stand weicht ab:** „Process with ID … not found“ wird als `ERROR` geloggt (I6).
- **Mittelschichten** lassen Fehler durch — **kein doppeltes Logging** desselben Fehlers. Ist-Stand:
  `InitializeWmiWatchersAsync` loggt `ERROR` und wirft weiter, `ExecuteAsync` loggt denselben Fehler erneut als
  `CRITICAL` (doppelt).
- **Niemals leeres `catch {}` ohne Logging.** Wird ein Fehler bewusst ignoriert, wird er auf `DEBUG` (reine Information)
  bzw. `WARNING` (Zustand ist auffällig) geloggt und der Grund kommentiert (`LOGGING.md`).

---

## Retry-Strategien

- Im Dienst gibt es **keine** Retries: ein fehlgeschlagenes CIM-Lookup liefert einen Sentinel-Wert, das Ereignis wird
  trotzdem geloggt.
- `Deploy-ProcessMonitorService.ps1`: `Invoke-RemoteScriptWithRetry` mit `MAX_RETRIES = 3`, fester Pause 5 s, ohne
  Jitter. Die Remote-Aktionen (Deinstallation, Installation) sind wiederholbar, aber nicht streng idempotent
  (Installation löscht und erzeugt den Dienst neu). Robocopy hat eigene Retries (`/R:3 /W:5`) — das sind zwei
  getrennte Operationen, keine geschachtelte Retry-Schleife.
- Authentifizierungsfehler bei WinRM **nicht** retryen (heute werden sie mit retryt — Verbesserung in I7 prüfen).
- Nicht-retrybare Fehler (Datei fehlt, Parameter ungültig) → sofort abbrechen.

---

## Feindliche Eingaben — kein Absturz an Systemgrenzen

Alles, was von außen kommt, ist feindlich: hier vor allem **Prozessnamen, Pfade und Kommandozeilen beliebiger
Prozesse** (frei wählbar durch jeden lokalen Benutzer), die Konfigurationsdatei und die JSON-Logs, die
`process-JSONFiles.ps1` einliest. Ergebnis ist **immer ein behandelbarer Fehler**, nie ein Prozessabsturz, ein
hängender Prozess oder verlorene Daten.

> Abgrenzung zu [[FAIL_FAST]]: Fail Fast bricht die **Operation** früh und laut ab.
> Diese Regel sorgt dafür, dass dabei nicht der **Prozess** oder die Arbeit des Nutzers
> mitgerissen wird. Abgrenzung zu [[UNTRUSTED_DOCUMENT_CONTENT]]: dort geht es um fremden
> Inhalt im LLM-Prompt (Injection/Fälschung), hier um die Robustheit des Prozesses.

- **Eingabe-abgeleitete Zahlen prüfen:** Konfigurierte Intervalle/Grenzen vor dem Anlegen von Timern und Caches
  validieren (heute nicht, I4).
- **Größen begrenzen:** Der SID-Cache ist auf `MaxCacheSize` begrenzt (Schutz gegen Prozess-Fluten). Die Verdrängung
  sortiert bei vollem Cache **alle** Einträge je neuem Prozess — bei Prozess-Fluten CPU-intensiv (Backlog).
- **Fremde Strings nie ungeprüft in Abfragen:** Filtermuster werden per `EscapeWqlString` in WQL eingesetzt; das
  Escaping ist nicht dokumentkonform (Bedrohung 4, I4).
- **Schreiben atomar:** übernimmt Serilog (Append-only Logdateien); `process-JSONFiles.ps1 -Export` schreibt die CSV
  direkt — bei Abbruch bleibt eine unvollständige Datei (akzeptiert, Auswertungsartefakt).
- **Letztes Sicherheitsnetz** an der äußersten Boundary: `Main` (`catch (Exception)` + `Log.Fatal`), Event-Handler und
  Timer-Callbacks mit eigenem `try/catch`.
- **Jeder Crash-Fix bringt einen kleinen synthetischen Regressionstest mit**, der vor dem
  Fix nachweislich abgestürzt ist (behaviorales Rot, `KICKOFF.md` Phase 2).
- **Robustheitstest über alle Einstiegspunkte**, sobald es eine zentrale Eingangsschicht gibt: hier die
  Filterlogik (ab I4 `ProcessFilter`) mit feindlichen Mustern (leer, nur Wildcards, Apostroph, `[`, sehr lang) und die
  Skript-Parameter (Pester).

---

## Konvertierungstabelle

| Fehlerfall / Exception | Exit-Code / Ergebnis | Log-Level | Bemerkung |
|---|---|---|---|
| Keine Adminrechte beim Start | Programmende, Exit-Code `0` (sollte ≠ 0 sein, K13) | `FATAL` | Im CLI-Modus zusätzlich Konsolenmeldung |
| Exception beim Aufbau/Start des Hosts | Exit-Code `1` | `FATAL` | `Main`-Catch |
| WMI-Watcher lassen sich nicht starten | Host stoppt; Dienst bleibt gestoppt | `ERROR` + `CRITICAL` | doppelt geloggt; Recovery fehlt (I6) |
| Fehler bei Verarbeitung eines Ereignisses | Ereignis verworfen, Dienst läuft weiter | `ERROR` | `HandleProcessEventAsync` |
| Prozess beim SID-Lookup nicht mehr vorhanden | `UserSid = "UNKNOWN_PROCESS_NOT_FOUND"` | `ERROR` (sollte `WARNING`/`DEBUG` sein) | kurzlebige Prozesse |
| `GetOwnerSid` liefert keine SID | `UserSid = "UNKNOWN_SID_NOT_FOUND"` | — | kein Log |
| CIM-Fehler beim SID-Lookup | `UserSid = "ERROR_GETTING_SID"` | `ERROR` | |
| Stop-Ereignis ohne Cache-Eintrag | `UserSid = "UNKNOWN"` | — | Start vor Dienststart, Cache verdrängt oder abgelaufen |
| Elternprozess nicht ermittelbar | `ParentName = "N/A"` bzw. `"ERROR_GETTING_PROCESS_NAME"` | — / `WARNING` | PID 0 oder Elternprozess beendet |
| Fehler im Timer-Callback | Tick verworfen | `ERROR` | Cache-Bereinigung / Statusmeldung |
| PowerShell-Skripte: Fehler | `exit 1` | Konsolenausgabe (`Write-Host`) | `install.ps1` wartet zusätzlich auf Enter (`Read-Host`) |

Die Sentinel-Strings landen im Feld `UserSid` bzw. `ParentName` des JSON-Logs; Auswertungen müssen sie als
„unbekannt“ behandeln (`docs/features/LOG_ANALYSIS.md`).

---

## Anti-Pattern

- **`throw new Exception("...")`** statt einer spezifischen Klasse.
- **Catch-and-rethrow ohne Mehrwert** — entfernen.
- **Loggen und weiterwerfen** in Mittelschichten (Bestand: `InitializeWmiWatchersAsync`).
- **Stilles Wegfangen** in der Mitte des Aufruf-Stacks — Fail-Fast wird ausgehebelt.
- **Erwartbare Zustände als `ERROR`** loggen — erzeugt Alarmmüdigkeit.

---

## Pflege

Trigger (siehe `KICKOFF.md` Phase 3a): neue Exception-Klasse / neue Boundary
/ neue Retry-Stelle → diese Datei + Konvertierungstabelle aktualisieren.
