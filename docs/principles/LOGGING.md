# Logging Strategy

## Regel

Logs sind das einzige Fenster in den Systemzustand eines laufenden Prozesses.
Jeder Log-Eintrag muss ohne Kontextwissen verständlich und filterbar sein.

## Konkrete Vorgaben

- **Level DEBUG:** Detaillierter Datenfluss (z.B. "Datei X gelesen, Hash: Y")
- **Level INFO:** Fachliche Meilensteine (z.B. "Dokument X erfolgreich verarbeitet")
- **Level WARNING:** Erwartete, aber behandelte Probleme (z.B. "Service Timeout, Versuch 1/3")
- **Level ERROR:** Unerwartete Fehler – muss zwingend den Stacktrace enthalten (z. B. Python `exc_info=True`,
  .NET `LogError(ex, …)`)
- **Kontext:** Jeder Eintrag muss IDs, Dateinamen oder Bezeichner enthalten.
  "Verarbeitung fehlgeschlagen" ist verboten.
  "Verarbeitung für Datei 'X.pdf' fehlgeschlagen" ist korrekt.
- Konsolenausgaben über `print()` sind für interaktive CLI-Tools akzeptabel.
  Bei automatisiertem Betrieb (Scheduler, Service) muss das Logging-Framework verwendet werden.

## Verwandt

- [[FAIL_FAST]]
- [[UNTRUSTED_DOCUMENT_CONTENT]]

## Projektspezifisch

<!-- Hier beim Befüllen ersetzen. Maßgeblich für die Update-Klassifizierung ist
     der Vergleich gegen die alte Template-Version (ALTES_PROJECT_AKTUALISIEREN.md,
     Schritt 2c/3): LOGGING.md ist konstruktionsbedingt immer Kategorie C. -->


- **Framework:** Dienst über `ILogger<T>` mit Serilog als Provider (strukturierte Message-Templates, Konfiguration in
  `appsettings.json`); vor dem Host-Aufbau ein Serilog-Bootstrap-Logger. Die PowerShell-Skripte schreiben nur auf die
  Konsole (`Write-Host` bzw. `Write-LogMessage` mit Zeitstempel und Level) — sie laufen interaktiv.
- **Fachliche Log-Einträge sind das Produkt:** Jedes Prozessereignis ist ein `Information`-Eintrag mit festen
  Properties (`docs/features/PROCESS_MONITORING.md`); diese Einträge nicht für Diagnosezwecke umformulieren.
- **DEBUG-Modus:** `Serilog:MinimumLevel:Override:ProcessMonitorWorker` auf `Debug` setzen (Filter-Snapshots, aktive
  Filterlisten, Cache-Bereinigung); laut Serilog.Settings.Configuration lädt `MinimumLevel` beim Ändern der Datei ohne Neustart nach (nicht im Lauf geprüft).
- **Nicht loggen:** Credentials aus dem Deploy-Skript; über das dokumentierte Ereignisformat hinaus keine weiteren
  Prozessdaten (Umgebungsvariablen, Fensterinhalte). Kommandozeilen sind bereits sensibel und werden ab I5 maskiert.
  Erwartbare Zustände (Prozess schon beendet) nicht als `ERROR` (Ist-Stand weicht ab, I6).
- **Ziele:** rollierende JSON-Dateien (CLEF) unter `C:\ProgramData\ProcessMonitorService\logs\` + Konsole; kein
  Windows-Ereignisprotokoll (Backlog).
