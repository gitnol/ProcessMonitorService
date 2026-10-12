# Monitoring – ProcessMonitorService

Überwachung des **Dienstes selbst** (nicht der überwachten Prozesse — dazu `docs/features/PROCESS_MONITORING.md`).

---

## Signale im Dienstbetrieb

| Signal | Bedeutung | Wo prüfen |
|---|---|---|
| Dienststatus `Running` | Dienst läuft | `Get-Service ProcessMonitorService` |
| Dienststatus `Stopped` ohne manuellen Stopp | kritischer Fehler, kein Auto-Neustart (K10) | Log: „CRITICAL ERROR in ProcessMonitorWorker“; Ereignisanzeige → System (Dienststeuerung) |
| Statusmeldung „Service status: Running. Cache entries: …“ | Lebenszeichen (Heartbeat), alle `StatusUpdateIntervalMinutes` (Default 5) | JSON-Log |
| Warnung „Cache-Kapazität … erreicht“ | `MaxCacheSize` ausgeschöpft | JSON-Log |
| `ERROR`-Einträge „Error handling process event“ | einzelne Ereignisse verworfen | JSON-Log |
| Neue `startup_error_*.log` | Dienst wurde (neu) gestartet — entsteht bei jedem Start (K4) | `logs`-Ordner |
| Exit-Code `1` im CLI-Modus | Host konnte nicht gestartet werden | Konsole / `startup_error_*.log` |

---

## Logging-Konfiguration

Serilog aus `appsettings.json` (nicht die `Logging`-Sektion von `Microsoft.Extensions.Logging`):

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning",
        "Microsoft.Hosting.Lifetime": "Information",
        "ProcessMonitorWorker": "Information"
      }
    }
  }
}
```

- Datei-Senke: **CLEF/JSON** (`CompactJsonFormatter`), eine Zeile pro Ereignis — maschinenlesbar für
  `process-JSONFiles.ps1` und Log-Sammler.
- Konsole: Textformat mit `SourceContext` (CLI-Modus).
- Keine Korrelations-IDs nötig (keine Requests); Zuordnung über `ProcessId`, `MachineName` und Zeitstempel.

---

## Heartbeat-Regel für Betreiber

| Metrik | Schwellwert |
|---|---|
| Abstand zwischen zwei Statusmeldungen | > 2 × `StatusUpdateIntervalMinutes` (Default: 10 Minuten) → Alarm „Dienst hängt oder ist gestoppt“ |
| Dienststatus | ≠ `Running` außerhalb von Wartungsfenstern → Alarm |
| `ERROR`-Rate | dauerhaft > 0 pro Statusintervall → prüfen (WMI/CIM-Probleme) |
| Größe des `logs`-Ordners | nahe 30 × 10 MB → Aufbewahrung reicht ggf. nicht für den gewünschten Zeitraum (K11) |

Eine automatische Auswertung (z. B. geplante Aufgabe, die das Alter der neuesten Statusmeldung prüft) gehört nicht
zum Projekt; Vorschlag für die Ereignisprotokoll-Senke steht im Backlog.

---

## Windows-Ereignisprotokoll

Heute schreibt der Dienst **nicht** ins Ereignisprotokoll (keine EventLog-Senke). Sichtbar ist dort nur, was die
Dienststeuerung selbst protokolliert (Start/Stopp, unerwartetes Ende). Backlog: Lebenszyklus- und Fehlermeldungen
zusätzlich ins Anwendungsprotokoll schreiben.

---

## Pflege

Trigger: neue Metrik / Logging-Senke / Alert → hier aktualisieren.
