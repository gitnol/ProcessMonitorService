# Bekannte Probleme – ProcessMonitorService

Bugs, Quirks und Einschränkungen, die **nicht** akut behoben werden, aber
dokumentiert sein müssen, damit niemand zweimal darüber stolpert.

**Abgrenzung:**

- `TODO.md` = Arbeit, die in einem priorisierten Inkrement aufgeräumt wird
- `KNOWN_ISSUES.md` = bekannt, akzeptiert oder verschoben; mit Workaround
- `BACKLOG.md` = noch unpriorisierte Ideen, keine konkreten Probleme

Alle Einträge stammen aus der Code-Analyse beim Init (2026-10-11) — sie sind aus dem Quelltext abgeleitet, **nicht**
durch einen Lauf reproduziert (Testlauf ohne Build und ohne Dienstinstallation). „Reproduktion“ beschreibt deshalb den
erwarteten Weg.

---

## Aktive Probleme

| # | Beschreibung | Reproduktion | Workaround | Akzeptanzgrund | Geplant für |
|---|---|---|---|---|---|
| K1 | Sehr kurzlebige Prozesse fehlen im Log: intrinsische WMI-Ereignisse werden per Polling (`WITHIN 1`) erzeugt. | Prozess starten, der nach < 1 s endet (z. B. `cmd /c exit`), Log prüfen | keiner; für lückenlose Erfassung andere Quelle (Sysmon, ETW) nutzen | Architekturwechsel nötig | offen (`BACKLOG.md`) |
| K2 | `process-JSONFiles.ps1` deklariert `#Requires -Version 5.1`, nutzt aber den Operator `??` (ab PowerShell 7) und Emojis in einer BOM-losen Datei — unter Windows PowerShell 5.1 Parse-Fehler. | Skript in `powershell.exe` (5.1) starten | mit `pwsh` (PowerShell 7) ausführen | Analyse ist Admin-Werkzeug, PS 7 meist verfügbar | `I7` |
| K3 | Log-Property `ThreadId` ist in allen Einträgen gleich: `Program.cs` reichert den Wert einmalig beim Start an (`Enrich.WithProperty`). Die in `appsettings.json` genannten Enricher `WithMachineName`/`WithThreadId` haben kein Paket. | beliebige Logdatei, Feld `ThreadId` | Feld ignorieren | nur Diagnosewert | `I6` |
| K4 | Bei **jedem** Start entsteht eine neue Datei `startup_error_<Zeitstempel>.log` (Bootstrap-Logger), auch ohne Fehler; keine Aufbewahrungsgrenze. | Dienst mehrfach neu starten, `logs`-Ordner ansehen | Dateien gelegentlich manuell löschen | geringe Dateigröße | `I6` |
| K5 | Änderungen an `StatusUpdateIntervalMinutes`/`CacheCleanupIntervalMinutes` wirken erst nach Neustart (Timer werden nur in `InitializeTimers` angelegt). | Wert in `appsettings.json` ändern, Statusmeldungs-Abstand beobachten | Dienst neu starten | selten geändert | `I6` |
| K6 | `ParentName` wird per PID zum Ereigniszeitpunkt nachgeschlagen: bei beendetem Elternprozess `N/A`, bei wiederverwendeter PID ein falscher Name. | Elternprozess beenden, dann Kindprozess beenden → Stop-Ereignis | Elternprozess über `ParentProcessId` und Startereignis korrelieren | forensisch relevant, aber selten | offen (`BACKLOG.md`) |
| K7 | `Deploy-ProcessMonitorService.ps1`: `-RemoteServicePath` wird ignoriert (Pfad fest verdrahtet), die Robocopy-Kopie über `\\<Ziel>\C$` läuft mit der Identität des Aufrufers statt mit `-Credential`, Default von `-LocalSourcePath` ist ein entwicklerspezifischer Pfad. | Deploy mit Benutzer ohne Adminrechte auf dem Ziel, aber gültigen `-Credential` | als Benutzer mit Adminrechten auf dem Ziel ausführen; `-LocalSourcePath` immer angeben | Skript laut README „nicht optimiert“ | `I7` |
| K8 | Ereignisse während eines Watcher-Neustarts (Filteränderung) gehen verloren. | Filter ändern und gleichzeitig Prozesse starten | Filteränderungen in ruhigen Zeiten | kurzes Fenster | offen |
| K9 | `AddHealthChecks()` ist registriert, wird aber nirgends abgefragt (kein Endpunkt, kein Publisher). | Code-Suche nach `HealthCheck` | — | wirkungslos, aber harmlos | offen (`BACKLOG.md`) |
| K10 | Kritischer Fehler in `ExecuteAsync` stoppt den Host sauber; der Kommentar „Re-throw to trigger service restart“ trifft nicht zu, `install.ps1` setzt keine Wiederherstellungsoptionen → Überwachung bleibt aus. | WMI-Dienst während des Starts nicht verfügbar | Dienststatus überwachen, manuell starten | — | `I6` |
| K11 | Aufbewahrung nach Dateianzahl (`retainedFileCountLimit: 30`, 10 MB je Datei, Roll bei Größe), README spricht von täglichen Dateien. Bei hoher Last decken 30 Dateien weniger als 30 Tage ab; `process-JSONFiles.ps1 -Days` filtert nach Änderungsdatum der Datei. | viele Prozesse ohne Include-Filter überwachen | Limits in `appsettings.json` anpassen | Speicherplatz begrenzt | offen |
| K12 | `Program.cs` registriert `appsettings*.json` ein zweites Mal **nach** Umgebungsvariablen und CLI-Argumenten → diese können Werte aus `appsettings.json` nicht überschreiben (`docs/architecture/CONFIGURATION.md`). | `ProcessMonitor__CacheExpiryMinutes=1` setzen, Wirkung prüfen | Werte direkt in `appsettings.json` ändern | bisher kein Bedarf an Overrides | offen (`BACKLOG.md`) |
| K13 | Start ohne Adminrechte endet mit `Log.Fatal`, aber Exit-Code `0` (`return` ohne `Environment.ExitCode`). | als Standardbenutzer starten, `$LASTEXITCODE` prüfen | Konsolenausgabe lesen | nur CLI-Modus betroffen | `I6` |

---

## Behobene Probleme

Letzte Einträge behalten bis zur nächsten Aufräum-Iteration (Spur für
Lessons Learned). Danach archivieren oder löschen.

| # | Beschreibung | Behoben in | Commit |
|---|---|---|---|
| (noch keine Einträge) | | | |

---

## Pflege

- Neuer Bug ohne sofortigen Fix → Eintrag hier; TODO-Punkt **nur**, wenn
  priorisiert wird.
- Eintrag wird behoben → in „Behobene Probleme" verschieben, in der nächsten
  Aufräum-Iteration löschen.
- Diese Datei steht in der Trigger-Matrix in `KICKOFF.md` Phase 3a.
