# ProcessMonitorService

Ein Windows-Dienst und CLI-Tool zur Überwachung von Prozessen über WMI (CIM) Events. Der Dienst protokolliert gestartete und beendete Prozesse in JSON-Dateien, inklusive Benutzer-SID, Pfad und Kommandozeile des Prozesses.

## Features

- Überwachung von Prozessen über `__InstanceCreationEvent` und `__InstanceDeletionEvent`.
- Filterung nach Prozessname. Siehe `appsettings.json`. Änderungen an dieser Datei werden überwacht und eingelesen und aktualisieren den Filter im laufenden Betrieb.
- Tägliche JSON-Log-Dateien, standardmäßig gespeichert unter: `C:\ProgramData\ProcessMonitor\service-<yyyyMMdd>.json`.
  - Beispiel einer Zeile:
    `{"@t":"2025-06-14T14:12:09.2452257Z","@mt":"Process {EventType}: {ProcessName} (PID: {ProcessId}) User: {UserSid} Path: {ExecutablePath} Command: {CommandLine}","EventType":"Start","ProcessName":"CalculatorApp.exe","ProcessId":30316,"UserSid":"S-1-5-21-1234567890-1234567890-1234567890-1234","ExecutablePath":"C:\\Program Files\\WindowsApps\\Microsoft.WindowsCalculator_11.2502.2.0_x64__8wekyb3d8bbwe\\CalculatorApp.exe","CommandLine":"\"C:\\Program Files\\WindowsApps\\Microsoft.WindowsCalculator_11.2502.2.0_x64__8wekyb3d8bbwe\\CalculatorApp.exe\"","SourceContext":"ProcessMonitorWorker","MachineName":"REDACTED","ThreadId":1}`
- Automatische Erkennung, ob als Windows-Dienst oder interaktiv gestartet.
- Protokollierte Informationen:
  - Prozessname
  - Prozess-ID
  - Pfad zur ausführbaren Datei
  - Kommandozeile
  - Benutzer-SID
  - Eventtyp (Start/Stop)
  - Zeitstempel

## Installation

### Kompilieren mit .NET 9

- **Debug Build:**

  ```powershell
  dotnet build
  ```

- **Publish Build:**
  wird erstellt in `\bin\Release\net9.0-windows\win-x64\publish\ProcessMonitorService.exe`
  ```powershell
  dotnet publish -c Release -r win-x64 --self-contained true
  ```

### Dienst installieren:

- benutzt: `\bin\Release\net9.0-windows\win-x64\publish\ProcessMonitorService.exe`
- Dienst startet automatisch nach Ausführung der `install.ps1`
  ```powershell
  .\install.ps1
  ```

### Dienst starten:

```powershell
Start-Service ProcessMonitorService
```

## Deinstallation

```powershell
.\uninstall.ps1
```

## Voraussetzungen

- .NET 9 SDK
- Pakete:
  ```powershell
  dotnet add package Microsoft.Extensions.Hosting --version 9.0.6
  dotnet add package Microsoft.Extensions.Hosting.WindowsServices
  dotnet add package Microsoft.Management.Infrastructure
  dotnet add package Microsoft.Extensions.Diagnostics.HealthChecks
  dotnet add package System.Management
  dotnet add package System.Management.Automation
  dotnet add package Serilog.Extensions.Hosting
  dotnet add package Serilog.Settings.Configuration
  dotnet add package Serilog.Sinks.Console
  dotnet add package Serilog.Sinks.File
  dotnet add package Serilog.Formatting.Compact
  ```
- Administratorrechte für:
  - Dienstinstallation
  - Zugriff auf Prozessinformationen
  - Schreiben nach C:\ProgramData

## Hinweise

- Für beendete Prozesse kann die Benutzer-SID nicht direkt mehr ausgelesen werden – sie wird daher zur Startzeit gespeichert und beim Stop-Ereignis wiederverwendet.
- Verwendet `System.Management` (WMI/CIM), daher nur unter Windows lauffähig.
- Wenn der Zugriff auf die Daten auf `Administratoren` (und `System`) beschränkt werden soll:
  ```powershell
  New-Item -Path "C:\ProgramData\ProcessMonitor" -ItemType Directory -Force
  icacls "C:\ProgramData\ProcessMonitor" /inheritance:r
  icacls "C:\ProgramData\ProcessMonitor" /grant:r "Administratoren:(OI)(CI)(F)" "SYSTEM:(OI)(CI)(F)"
  ```

## Lizenz

MIT – freie Nutzung, keine Garantie.
