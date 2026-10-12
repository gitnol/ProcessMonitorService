# Glossar – ProcessMonitorService

Ubiquitous Language für dieses Projekt. Jeder Begriff hat genau eine Bedeutung.
Synonyme oder doppelt belegte Begriffe schaffen Drift und werden vermieden.

LLM-Agenten konsultieren dieses Glossar, **bevor** sie Begriffe im Code, in
Commit-Messages oder in Doku verwenden.

---

## Begriffe

| Begriff | Bedeutung | Wo verwendet | Nicht zu verwechseln mit |
|---|---|---|---|
| Prozessereignis | Ein protokollierter Start oder Stop eines überwachten Prozesses (`EventType` = `Start`/`Stop`). | `HandleProcessEventAsync`, `LogProcessEvent`, JSON-Log | Log-Eintrag allgemein (Status-, Fehler- und Lebenszyklusmeldungen sind keine Prozessereignisse) |
| Include-Filter | Muster in `ProcessFilters`; ist mindestens eines gesetzt, werden nur passende Prozesse überwacht. | `ProcessMonitorOptions`, `_processFilterSet` | „Whitelist“ |
| Exclude-Filter | Muster in `ProcessExcludeFilters`; passende Prozesse werden nie überwacht, auch wenn ein Include-Filter passt. | `_processExcludeFilterSet` | „Blacklist“ |
| Filtermuster | Prozessname mit optionalen Wildcards `*` (beliebig viele Zeichen) und `?` (genau ein Zeichen), ohne Beachtung der Groß-/Kleinschreibung. | `BuildProcessFilter`, `ShouldMonitorProcess` | Regulärer Ausdruck (wird intern daraus erzeugt, ist aber nicht konfigurierbar) |
| Prozessname | Wert von `Win32_Process.Name`, also der Dateiname der ausführbaren Datei inkl. Endung (z. B. `notepad.exe`). | Filter, Feld `ProcessName` | Pfad (`ExecutablePath`), Fenstertitel |
| Owner-SID | Security Identifier des Benutzers, unter dem ein Prozess läuft, ermittelt per CIM-Methode `GetOwnerSid` beim Start. | `ProcessOwnerService.GetProcessOwnerSidAsync`, Feld `UserSid` | Benutzername (wird nicht aufgelöst) |
| SID-Cache | Speicher PID → Owner-SID, damit das Stop-Ereignis die SID des bereits beendeten Prozesses tragen kann; begrenzt durch `MaxCacheSize` und `CacheExpiryMinutes`. | `_processSidCache`, `ProcessCacheEntry` | Konfigurations-Cache |
| Elternprozess | Prozess mit der `ParentProcessId` des Ereignisses; sein Name wird zum Zeitpunkt des Ereignisses per PID nachgeschlagen. | Felder `ParentName`, `ParentProcessId` | Aufrufender Benutzer |
| Sentinel-Wert | Fester Ersatzstring statt eines echten Werts, z. B. `UNKNOWN`, `N/A`, `ERROR_GETTING_SID`. | `ProcessOwnerService`, `HandleProcessEventAsync` | Leerer Wert / `null` |
| WMI-Watcher | `ManagementEventWatcher` für `__InstanceCreationEvent` bzw. `__InstanceDeletionEvent` auf `Win32_Process`. | `_startWatcher`, `_stopWatcher` | Datei-Watcher (`reloadOnChange` der Konfiguration) |
| Dienstmodus | Betrieb als Windows-Dienst unter LocalSystem, gesteuert durch die Dienststeuerung. | `UseWindowsService()`, `install.ps1` | CLI-Modus |
| CLI-Modus | Interaktiver Start der `ProcessMonitorService.exe` in einer Admin-Konsole, Beenden mit Strg+C. | README „Nutzung als CLI-Tool“ | Log-Analyse-Skript |
| Datenverzeichnis | `C:\ProgramData\ProcessMonitorService` mit Unterordner `logs`; per ACL auf Administratoren und SYSTEM beschränkt. | `Set-SecureACLs.ps1`, Serilog-Pfad | Programmverzeichnis (`C:\Program Files\ProcessMonitorService` bzw. Publish-Ordner) |
| Bootstrap-Logger | Serilog-Logger, der vor dem Host-Aufbau auf Konsole und `startup_error_<Zeitstempel>.log` schreibt. | `Program.cs` | Haupt-Logger aus `appsettings.json` |
| Statusmeldung | Periodischer Log-Eintrag mit Cache-Füllstand und Filteranzahl. | `LogStatusUpdate` | Health Check |
| Log-Analyse | Auswertung der JSON-Logs mit `process-JSONFiles.ps1`. | `docs/features/LOG_ANALYSIS.md` | Monitoring des Dienstes selbst |
| Remote-Deployment | Verteilung und Installation auf einem anderen Rechner per `Deploy-ProcessMonitorService.ps1`. | `docs/architecture/REMOTE_EXECUTION.md` | Lokale Installation (`install.ps1`) |

---

## Verbotene Synonyme

Begriffe, die im Projekt-Kontext **nicht** mehr verwendet werden dürfen,
auch wenn sie umgangssprachlich gleichbedeutend wirken:

| Verbotener Begriff | Korrekt | Grund |
|---|---|---|
| Whitelist / Blacklist | Include-Filter / Exclude-Filter | Code und README sprechen von Include/Exclude |
| User / Benutzer (für `UserSid`) | Owner-SID | Das Feld enthält eine SID, keinen Benutzernamen; bei Fehlern einen Sentinel-Wert |
| Prozess-Log | JSON-Log bzw. Prozessereignis | Mehrdeutig zwischen Logdatei und Einzelereignis |

---

## Pflege

- Neuer Domänenbegriff im Code → **vor dem Commit** hier ergänzen.
- Begriff wird umbenannt → alter Eintrag bleibt mit Vermerk „ersetzt durch <neuer Begriff>“
  stehen (mindestens eine Inkrement-Generation als Übergangsphase).
- Begriff entfällt → Eintrag mit „deprecated <Datum>“ markieren, nicht löschen.
- Diese Datei steht in der Trigger-Matrix in `KICKOFF.md` Phase 3a.
