# Feature: Prozessüberwachung (Dienst / CLI)

---

## Zweck

Erfasst Start- und Stop-Ereignisse von Prozessen auf dem lokalen Windows-Rechner, filtert sie nach Prozessnamen und
schreibt je Ereignis einen strukturierten Log-Eintrag mit Owner-SID, Elternprozess, Pfad und Kommandozeile.
Auslöser: WMI-Ereignisse `__InstanceCreationEvent`/`__InstanceDeletionEvent` auf `Win32_Process` (Polling `WITHIN 1`).
Ausgabe: JSON-Logdatei (CLEF) unter `C:\ProgramData\ProcessMonitorService\logs\` und Konsole.

---

## Einstiegspunkte

| Auslöser | Typ | Beschreibung |
|---|---|---|
| `ProcessMonitorService` (Dienstname) | Windows-Dienst, `IHostedService` `ProcessMonitorWorker` | Start automatisch beim Systemstart (`install.ps1`, Starttyp `auto`), Konto LocalSystem |
| `ProcessMonitorService.exe` | CLI-Modus (Admin-Konsole) | gleiche Verarbeitung, Ende mit Strg+C; ohne Adminrechte sofortiges Ende |
| `appsettings.json` | Konfiguration mit Reload | Filteränderung startet die WMI-Watcher neu |
| `install.ps1` | PowerShell | `-ExePath <Pfad zur exe>` (Default: neben dem Skript), `-ServiceName` (Default `ProcessMonitorService`) |
| `uninstall.ps1` | PowerShell | `-ServiceName` |

Kommandozeilenargumente der exe werden nur als Konfigurationsquelle ausgewertet (z. B. `--ProcessMonitor:MaxCacheSize 20000`,
eingeschränkt durch K12); eigene Optionen gibt es nicht.

---

## Eingabe-/Ausgabe-Kontrakt

**Eingabe:** Konfiguration (`docs/architecture/CONFIGURATION.md`) und WMI-Ereignisse.

**Ausgabe je Prozessereignis** (Message-Template `Process {EventType}: {ProcessName} (PID: {ProcessId}) User: {UserSid} …`,
Felder im JSON):

| Feld | Typ | Quelle | Bedeutung |
|---|---|---|---|
| `@t` | ISO-8601 UTC | Serilog | Zeitpunkt der Verarbeitung (nicht exakt der Prozessstart, Polling) |
| `EventType` | `Start` / `Stop` | Watcher | Art des Ereignisses |
| `ProcessName` | string | `Win32_Process.Name` | Dateiname inkl. Endung |
| `ProcessId` | int | `Win32_Process.ProcessId` | PID |
| `UserSid` | string | `GetOwnerSid` (Start) bzw. SID-Cache (Stop) | SID oder Sentinel-Wert (`docs/architecture/ERROR_HANDLING.md`) |
| `ParentName` | string | CIM-Abfrage per `ParentProcessId` | Name des Elternprozesses zum Ereigniszeitpunkt oder `N/A` |
| `ParentProcessId` | int | `Win32_Process.ParentProcessId` | PID des Elternprozesses |
| `ExecutablePath` | string | `Win32_Process.ExecutablePath` | voller Pfad oder `N/A` |
| `CommandLine` | string | `Win32_Process.CommandLine` | vollständige Kommandozeile oder `N/A` — **sensibel** (`docs/security/DATA_HANDLING.md`) |
| `SourceContext` | string | Serilog | `ProcessMonitorWorker` — Filterkriterium von `process-JSONFiles.ps1` |
| `MachineName`, `ThreadId` | string, int | `Program.cs` | Rechnername; `ThreadId` konstant (K3) |

Zusätzlich: Lebenszyklus-, Status- und Fehlermeldungen ohne `EventType`.

**Exit-Codes (CLI-Modus):** `0` normales Ende (und heute auch: fehlende Adminrechte, K13), `1` Host-Start fehlgeschlagen.

---

## Fachliche Regeln (statt `architecture/DOMAIN_MODEL.md`)

1. **Exclude vor Include:** Trifft ein Exclude-Filter, wird der Prozess nie protokolliert.
2. **Include optional:** Ohne Include-Filter werden alle nicht ausgeschlossenen Prozesse protokolliert; mit
   Include-Filtern nur passende.
3. **Muster:** `*` = beliebig viele Zeichen, `?` = genau ein Zeichen, Vergleich ohne Groß-/Kleinschreibung, ganzer
   Name muss passen.
4. **Zweifache Prüfung:** Filter werden als WQL-Bedingung an WMI gegeben **und** im Code je Ereignis erneut geprüft
   (`ShouldMonitorProcess`) — die Code-Prüfung ist maßgeblich.
5. **SID beim Stop:** Die SID eines beendeten Prozesses ist nicht mehr abfragbar; sie wird beim Start gecacht und beim
   Stop aus dem Cache genommen (und entfernt). Fehlt der Eintrag → `UNKNOWN`.
6. **Cache-Grenzen:** höchstens `MaxCacheSize` Einträge (Verdrängung des ältesten), Einträge älter als
   `CacheExpiryMinutes` werden alle `CacheCleanupIntervalMinutes` entfernt — für Prozesse, die länger laufen als
   `CacheExpiryMinutes`, trägt das Stop-Ereignis deshalb `UNKNOWN` (README: „Prozesse, die länger laufen … werden
   entfernt“).

---

## Service-Schicht

```csharp
public interface IProcessOwnerService
{
    Task<string> GetProcessOwnerSidAsync(int processId);
    Task<string> GetProcessNameByIdAsync(uint processId);
}
```

`ProcessMonitorWorker : BackgroundService` verdrahtet Watcher, Filter, Cache und Timer; ab I4 liegt die Filterlogik in
einer eigenen, ohne WMI testbaren Klasse.

---

## Exitcodes / Statuscodes

| Code | Bedeutung |
|---|---|
| `0` | Normales Ende (Strg+C/Dienststopp); heute auch bei fehlenden Adminrechten (K13) |
| `1` | Host konnte nicht gestartet werden (`Program.cs`, `Environment.ExitCode = 1`) |

---

## Kritische Patterns

- Event-Handler (`OnProcessStarted`/`OnProcessStopped`) geben die Arbeit per `Task.Run` ab und dürfen nie werfen.
- Filteränderungen nur über `UpdateProcessFilters` (Snapshot-Vergleich unter Sperre, dann Watcher-Neustart).
- Log-Felder sind Schnittstelle zu `process-JSONFiles.ps1` — Namen nicht einseitig ändern.
- Neue Felder nur nach Prüfung gegen `docs/security/DATA_HANDLING.md`.

---

## Bekannte Einschränkungen

| Einschränkung | Ursache | Status |
|---|---|---|
| Kurzlebige Prozesse fehlen | Polling `WITHIN 1` (K1) | Akzeptiert, Alternative im Backlog |
| Kommandozeilen unmaskiert im Log | kein Filter auf Secrets (Bedrohung 3) | Geplant I5 |
| Kein Neustart nach kritischem Fehler | sauberer Host-Stopp, keine Recovery-Optionen (K10) | Geplant I6 |
| Ereignisse während Watcher-Neustart verloren | Watcher werden gestoppt, dann neu angelegt (K8) | Akzeptiert |
| `ParentName` ungenau | Nachschlagen per PID zum Ereigniszeitpunkt (K6) | Akzeptiert, Backlog |

---

## Bekannte Fallstricke

- Seit .NET 6 stoppt eine unbehandelte Exception in `ExecuteAsync` den Host **sauber** — die Dienststeuerung sieht
  keinen Fehler und startet nicht neu (Microsoft-Doku „Create Windows Service using BackgroundService“).
- `appsettings.json` wird zur Laufzeit neu geladen, aber nur Filter und Cache-Werte wirken sofort; Intervalle und
  Serilog-Senken (Pfad, Aufbewahrung) erst nach Neustart (K5).
- Werte aus Umgebungsvariablen/CLI überschreiben `appsettings.json` nicht (K12).
- Exclude-Muster mit breiten Wildcards (`*temp*`) schließen auch harmlose oder absichtlich so benannte Programme aus
  (Umgehung, Bedrohung 6).

---

## Teststrategie

- Unit: `tests/ProcessMonitorService.UnitTests/Filtering/ProcessFilterTests.cs` — Filterregeln 1–4 und WQL-Erzeugung (I4);
  Cache-Regeln 5–6 mit injizierter Zeitquelle
- Integration: `tests/ProcessMonitorService.IntegrationTests/Wmi/ProcessEventCaptureTests.cs` — echter Watcher auf einer Test-VM
- End-to-End: veröffentlichtes Programm im CLI-Modus starten, `notepad.exe` öffnen/schließen, JSON-Log prüfen
  (Smoke-Test, `docs/operations/DEPLOYMENT.md`)
