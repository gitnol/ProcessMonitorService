# Runbook – ProcessMonitorService

Symptom → Diagnose → Fix. Einträge aus der Code-Analyse beim Init abgeleitet; im Betrieb bestätigte Fälle ergänzen.

---

## Build / Restore

| Symptom | Diagnose | Fix |
|---|---|---|
| `error NETSDK1045` (SDK-Version) | .NET-9-SDK fehlt (ab I3: SDK aus `global.json`) | `winget install Microsoft.DotNet.SDK.9` |
| `dotnet format` bricht mit Exception ab | `.sln` und `.csproj` liegen im selben Ordner | Solution angeben: `dotnet format ProcessMonitorService.sln --verify-no-changes` |
| `dotnet build -warnaserror` rot mit CS0105 | doppelte `using System.IO;` in `Program.cs` | in I3 entfernen |

---

## Runtime

| Symptom | Diagnose | Fix |
|---|---|---|
| Programm endet sofort mit „requires administrative privileges“ | Start ohne Adminrechte | Konsole „Als Administrator ausführen“; Dienst läuft als LocalSystem |
| Dienst startet, stoppt aber gleich wieder | WMI-Watcher-Start fehlgeschlagen (Log: „Failed to initialize WMI watchers“ / „CRITICAL ERROR“), z. B. WMI-Dienst gestört oder ungültiges Filtermuster | `Get-Service Winmgmt`; letzte Filteränderung in `appsettings.json` prüfen (Apostroph/Sonderzeichen, Bedrohung 4); Dienst neu starten |
| Dienst nach einem Fehler dauerhaft gestoppt | kein automatischer Neustart (K10) | `Start-Service ProcessMonitorService`; Ursache im Log; I6 |
| Erwarteter Prozess fehlt im Log | Exclude-Filter trifft, Include-Filter trifft nicht oder Prozess war kürzer als ca. 1 s aktiv (K1) | Filter in `appsettings.json` prüfen; `Serilog:MinimumLevel:Override:ProcessMonitorWorker` auf `Debug` → „Active include filters“ |
| `UserSid` = `UNKNOWN_PROCESS_NOT_FOUND` / `ERROR_GETTING_SID` | Prozess schon beendet bzw. CIM-Fehler | erwartbar bei kurzlebigen Prozessen; bei Häufung CIM/WMI prüfen |
| Stop-Ereignis mit `UserSid` = `UNKNOWN` | Start lag vor Dienststart, Cache-Eintrag abgelaufen (`CacheExpiryMinutes`) oder verdrängt (`MaxCacheSize`) | Werte erhöhen, falls langlaufende Prozesse eine SID am Stop brauchen |
| Warnung „Cache-Kapazität … erreicht“ | mehr gleichzeitig laufende überwachte Prozesse als `MaxCacheSize` | Include-Filter enger fassen oder `ProcessMonitor:MaxCacheSize` erhöhen |
| Deadlock / hängender Watcher-Neustart | `RestartWatchers` blockiert mit `.Wait()` im Reload-Callback | Dienst neu starten; Refactoring im Backlog |

---

## Logging und Diagnose

```powershell
# Dienststatus
Get-Service ProcessMonitorService
sc.exe qc ProcessMonitorService          # Binary-Pfad und Dienstkonto
sc.exe qfailure ProcessMonitorService    # Wiederherstellungsoptionen (heute leer, K10)

# Neueste Logdatei ansehen (Admin-Konsole)
Get-ChildItem C:\ProgramData\ProcessMonitorService\logs | Sort-Object LastWriteTime | Select-Object -Last 3
Get-Content (Get-ChildItem C:\ProgramData\ProcessMonitorService\logs\service-*.json | Sort-Object LastWriteTime | Select-Object -Last 1) -Tail 20

# Mehr Details: in appsettings.json "ProcessMonitorWorker": "Debug" setzen
# (Filter und laut Serilog-Doku auch MinimumLevel wirken ohne Neustart; Senken erst nach Neustart)

# Live-Diagnose (dotnet-counters/dotnet-stack als globale Tools)
dotnet-counters monitor -n ProcessMonitorService
dotnet-stack report -n ProcessMonitorService
```

Auswertung der Prozessereignisse: `docs/features/LOG_ANALYSIS.md`.

---

## Performance-Probleme

| Symptom | Diagnose | Fix |
|---|---|---|
| Hohe CPU-Last von `WmiPrvSE.exe` | Polling `WITHIN 1`, viele Prozesse, besonders ohne Include-Filter | Include-Filter setzen; Backlog: ereignisbasierte Quelle |
| Hohe CPU des Dienstes bei Prozess-Fluten | Cache voll → Verdrängung sortiert alle Einträge je neuem Prozess | `MaxCacheSize`/Filter anpassen; Backlog-Refactoring |
| Logverzeichnis wächst schnell | viele Ereignisse, 10 MB je Datei, 30 Dateien | Filter enger; Aufbewahrung in `appsettings.json` anpassen (K11) |

---

## Konfigurations-Probleme

| Symptom | Diagnose | Fix |
|---|---|---|
| Umgebungsvariable/CLI-Argument wirkt nicht | Schlüssel steht in `appsettings.json`, das zuletzt registriert wird (K12) | Wert in `appsettings.json` ändern |
| Geändertes Intervall wirkt nicht | Timer werden nur beim Start angelegt (K5) | Dienst neu starten |
| Geänderte Filter wirken nicht | Snapshot identisch oder Datei nicht im Programmverzeichnis geändert | `appsettings.json` neben der `ProcessMonitorService.exe` ändern; Debug-Log „Configuration snapshot identical“ prüfen |

---

## Pflege

Trigger: häufig wiederkehrendes Support-Problem → hier dokumentieren.
