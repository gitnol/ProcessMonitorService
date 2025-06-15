# ProcessMonitorService

Ein Windows-Dienst und CLI-Tool zur Überwachung von Prozessen über WMI (CIM) Events. Der Dienst protokolliert gestartete und beendete Prozesse in JSON-Dateien, inklusive Benutzer-SID, Pfad und Kommandozeile.

## Features

- Überwachung von Prozessen über `__InstanceCreationEvent` und `__InstanceDeletionEvent`.
- Filterung nach Prozessname. Konfigurierbar über `appsettings.json`. Änderungen an dieser Datei werden automatisch erkannt und übernommen.
- Tägliche JSON-Log-Dateien, standardmäßig gespeichert unter:  
  `C:\ProgramData\ProcessMonitor\service-<yyyyMMdd>.json`
- Automatische Erkennung, ob als Windows-Dienst oder interaktiv (CLI) gestartet.
- Protokollierte Informationen:
  - Prozessname
  - Prozess-ID
  - Pfad zur ausführbaren Datei
  - Kommandozeile
  - Benutzer-SID
  - Eventtyp (Start/Stop)
  - Zeitstempel

## Beispielhafte Log-Ausgabe

```json
{
  "@t": "2025-06-14T14:12:09.2452257Z",
  "@mt": "Process {EventType}: {ProcessName} (PID: {ProcessId}) User: {UserSid} Path: {ExecutablePath} Command: {CommandLine}",
  "EventType": "Start",
  "ProcessName": "notepad.exe",
  "ProcessId": 1234,
  "UserSid": "S-1-5-21-1234567890-123456789-1234567890-1001",
  "ExecutablePath": "C:\\Windows\\System32\\notepad.exe",
  "CommandLine": "\"C:\\Windows\\System32\\notepad.exe\"",
  "Timestamp": "2025-06-14T14:12:09.2452257Z"
}
```

## Beispiel für `appsettings.json`

```json
{
  "ProcessMonitor": {
    "ProcessFilters": [ "notepad.exe", "calc.exe" ],
    "CacheExpiryMinutes": 30,
    "StatusUpdateIntervalMinutes": 5,
    "CacheCleanupIntervalMinutes": 10
  }
}
```
- Die Liste `ProcessFilter` bestimmt, welche Prozesse überwacht werden.
- `LogDirectory` legt das Verzeichnis für die Log-Dateien fest.

## Installation

### Kompilieren mit .NET 9

**Debug Build:**
```powershell
dotnet build
```

**Publish Build:**  
Wird erstellt in `\bin\Release\net9.0-windows\win-x64\publish\ProcessMonitorService.exe`
```powershell
dotnet publish -c Release -r win-x64 --self-contained true
```

### Dienst installieren:

- Verwendet: `\bin\Release\net9.0-windows\win-x64\publish\ProcessMonitorService.exe`
- Dienst startet automatisch nach Ausführung der `install.ps1`
```powershell
.\install.ps1
```

### Dienst starten:

```powershell
Start-Service ProcessMonitorService
```

## Nutzung als CLI-Tool

Das Programm kann auch direkt in einer Eingabeaufforderung (mit Administratorrechten) gestartet werden. Es überwacht dann Prozesse interaktiv und gibt die Logs in die Konsole und in die Log-Datei aus.

```powershell
.\ProcessMonitorService.exe
```
- Zum Beenden `Strg+C` verwenden.

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
  dotnet add package Microsoft.Extensions.Diagnostics.HealthChecks
  dotnet add package Microsoft.Management.Infrastructure
  dotnet add package System.Management
  dotnet add package Serilog.Extensions.Hosting
  dotnet add package Serilog.Settings.Configuration
  dotnet add package Serilog.Sinks.Console
  dotnet add package Serilog.Sinks.File
  dotnet add package Serilog.Formatting.Compact
  ```
- Administratorrechte für:
  - Dienstinstallation
  - Zugriff auf Prozessinformationen
  - Schreiben nach `C:\ProgramData`

## Hinweise

- Für beendete Prozesse kann die Benutzer-SID nicht direkt mehr ausgelesen werden – sie wird daher zur Startzeit gespeichert und beim Stop-Ereignis wiederverwendet.
- Verwendet `System.Management` (WMI/CIM), daher nur unter Windows lauffähig.
- Optional: Zugriff auf die Daten auf `Administratoren` (und `System`) beschränken:
  ```powershell
  New-Item -Path "C:\ProgramData\ProcessMonitor" -ItemType Directory -Force
  icacls "C:\ProgramData\ProcessMonitor" /inheritance:r
  icacls "C:\ProgramData\ProcessMonitor" /grant:r "Administratoren:(OI)(CI)(F)" "SYSTEM:(OI)(CI)(F)"
  ```

## Lizenz

MIT – freie Nutzung, keine Garantie.
