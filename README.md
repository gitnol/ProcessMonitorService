- [ProcessMonitorService](#processmonitorservice)
  - [Features](#features)
  - [Beispielhafte Log-Ausgabe nach `service-<yyyyMMdd>.json`](#beispielhafte-log-ausgabe-nach-service-yyyymmddjson)
  - [Beispiel für `appsettings.json`](#beispiel-für-appsettingsjson)
  - [Voraussetzungen für die Installation / Ausführung](#voraussetzungen-für-die-installation--ausführung)
  - [Installation](#installation)
    - [Kompilieren mit .NET 9](#kompilieren-mit-net-9)
    - [Dienst installieren](#dienst-installieren)
    - [Dienst starten](#dienst-starten)
    - [Deinstallation](#deinstallation)
  - [Nutzung als CLI-Tool](#nutzung-als-cli-tool)
  - [Hinweise](#hinweise)
- [Log-Analyse mit PowerShell](#log-analyse-mit-powershell)
  - [Verwendung](#verwendung)
  - [Parameter](#parameter)
  - [Ausgabeformat](#ausgabeformat)
  - [Beispiel-Ausgabe](#beispiel-ausgabe)
- [Lizenz](#lizenz)

# ProcessMonitorService

Ein Windows-Dienst und CLI-Tool zur Überwachung von Prozessen über WMI (CIM) Events. Der Dienst protokolliert gestartete und beendete Prozesse in JSON-Dateien, inklusive Benutzer-SID, Pfad und Kommandozeile.

## Features

- Überwachung von Prozessen über `__InstanceCreationEvent` und `__InstanceDeletionEvent`.
- Filterung nach Prozessname. Konfigurierbar über `appsettings.json`. Änderungen an dieser Datei werden automatisch erkannt und übernommen.
- Tägliche JSON-Log-Dateien, standardmäßig gespeichert unter:  
  `C:\ProgramData\ProcessMonitorService\service-<yyyyMMdd>.json`
- Automatische Erkennung, ob als Windows-Dienst oder interaktiv (CLI) gestartet.
- Protokollierte Informationen:
  - Prozessname
  - Prozess-ID
  - Pfad zur ausführbaren Datei
  - Kommandozeile
  - Benutzer-SID
  - Eventtyp (Start/Stop)
  - Zeitstempel

## Beispielhafte Log-Ausgabe nach `service-<yyyyMMdd>.json`

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
    "ProcessFilters": [ "notepad.exe", "calc.exe", "chrome*", "my-p?ocess.exe" ],
    "CacheExpiryMinutes": 30, // 1440 = Prozesse aus dem Cache entfernen, die älter als ein Tag sind
    "StatusUpdateIntervalMinutes": 5,
    "CacheCleanupIntervalMinutes": 10
  }
}
```
- Die Liste `ProcessFilter` bestimmt, welche Prozesse überwacht werden. (es wird * und ? unterstützt)
- `StatusUpdateIntervalMinutes`: Gibt nach wiederkehrenden `5` Minuten eine Statusmeldung aus: `Service status: Running. Cache entries: {CacheCount}, Active filters: {FilterCount}`
- `CacheCleanupIntervalMinutes`: Nach `10` Minuten wird der Cache Aufräumvorgang gestartet. Es werden dann die Prozesse aus dem Cache entfernt, welche ein Alter von `CacheExpiryMinutes` Minuten besitzen
- `CacheExpiryMinutes`: Prozesse, die länger laufen als `30` Minuten, werden beim Cache Aufräumvorgang nach `CacheCleanupIntervalMinutes` aus dem Cache entfernt.

## Voraussetzungen für die Installation / Ausführung

- .NET 9
- Administratorrechte für:
  - Dienstinstallation
  - Zugriff auf Prozessinformationen
  - Schreiben nach `C:\ProgramData\ProcessMonitorService`

## Installation

### Kompilieren mit .NET 9

**Voraussetzungen**

- .NET 9 SDK mit `winget install Microsoft.DotNet.SDK.9`
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

**Debug Build:**
```powershell
dotnet build
```

**Publish Build:**  
Wird erstellt in `\bin\Release\net9.0-windows\win-x64\publish\ProcessMonitorService.exe`
```powershell
dotnet publish -c Release -r win-x64 --self-contained true
```

### Dienst installieren

- Verwendet: `\bin\Release\net9.0-windows\win-x64\publish\ProcessMonitorService.exe`
- Dienst startet automatisch nach Ausführung der `install.ps1`
```powershell
.\install.ps1
```

### Dienst starten

```powershell
Start-Service ProcessMonitorService
```

### Deinstallation

```powershell
.\uninstall.ps1
```

## Nutzung als CLI-Tool

Das Programm kann auch direkt in einer Eingabeaufforderung (mit Administratorrechten) gestartet werden. Es überwacht dann Prozesse interaktiv und gibt die Logs in die Konsole und in die Log-Datei aus.

```powershell
.\ProcessMonitorService.exe
```
- Zum Beenden `Strg+C` verwenden.

## Hinweise

- Für beendete Prozesse kann die Benutzer-SID nicht direkt mehr ausgelesen werden – sie wird daher zur Startzeit gespeichert und beim Stop-Ereignis wiederverwendet.
- Verwendet `System.Management` (WMI/CIM), daher nur unter Windows lauffähig.
- Zugriff auf die Daten auf `Administratoren` (und `System`) beschränken (Alternativ: `./Set-SecureACLs.ps1` - wird bei Installation ausgeführt):
  ```powershell
  New-Item -Path "C:\ProgramData\ProcessMonitorService" -ItemType Directory -Force
  icacls "C:\ProgramData\ProcessMonitorService" /inheritance:r
  icacls "C:\ProgramData\ProcessMonitorService" /grant:r "*S-1-5-32-544:(OI)(CI)(F)" "*S-1-5-18:(OI)(CI)(F)"
  # Die wichtigsten Well-Known SIDs:

  # S-1-5-32-544 = Administratoren/Administrators
  # S-1-5-18 = SYSTEM
  # S-1-5-32-545 = Benutzer/Users
  # S-1-5-11 = Authentifizierte Benutzer/Authenticated Users
  ```

# Log-Analyse mit PowerShell

Das mitgelieferte PowerShell-Skript `process-JSONFiles.ps1` ermöglicht die komfortable Analyse der JSON-Log-Dateien.

## Verwendung

**Grundlegende Verwendung:**
```powershell
# Alle Events des aktuellen Tages anzeigen
.\process-JSONFiles.ps1

# Nach spezifischem Prozess filtern
.\process-JSONFiles.ps1 -ProcessName "chrome.exe"

# Nur Start-Events anzeigen
.\process-JSONFiles.ps1 -EventType "Start"

# Events der letzten 7 Tage
.\process-JSONFiles.ps1 -Days 7

# Kombinierte Filter
.\process-JSONFiles.ps1 -ProcessName "notepad.exe" -EventType "Start" -Days 3

# Kombinierte Filter mit Ausgabe und Auswahlmöglichkeit im GridView und Ausgabe wieder in der Pipeline
.\process-JSONFiles.ps1 -ProcessName "chrome.exe" -EventType "All" -Days 3 -ForPipeline | Out-GridView -Title "Notepad Prozessereignisse der letzten drei Tage" -PassThru | % {$_}
```

**Export-Funktionen:**
```powershell
# Events in CSV-Datei exportieren
.\process-JSONFiles.ps1 -Export

# Gefilterte Events exportieren
.\process-JSONFiles.ps1 -ProcessName "chrome.exe" -Days 7 -Export
```

## Parameter

| Parameter | Beschreibung | Standard |
|-----------|--------------|----------|
| `-LogPath` | Pfad zu den JSON-Log-Dateien | `C:\ProgramData\ProcessMonitorService\logs\service-*.json` |
| `-ProcessName` | Filter nach Prozessname (z.B. "chrome.exe") | Alle Prozesse |
| `-EventType` | Filter nach Event-Typ: "Start", "Stop", "All" | "All" |
| `-Days` | Zeigt Events der letzten X Tage | 1 |
| `-Export` | Exportiert Ergebnisse in CSV-Datei | Nein |
| `-ForPipeline` | Ermöglicht Weiterverarbeitung der Ergebnisse über eine Pipeline | Nein |

## Ausgabeformat

Das Skript zeigt folgende Informationen an:
- **Timestamp**: Datum und Uhrzeit des Events
- **EventType**: "Start" oder "Stop"
- **ProcessName**: Name der ausführbaren Datei
- **ProcessId**: Prozess-ID
- **UserSid**: Benutzer-SID
- **ExecutablePath**: Vollständiger Pfad zur Datei
- **CommandLine**: Verwendete Kommandozeile

## Beispiel-Ausgabe

```
=== ProcessMonitorService Log-Analyse ===
✅ Gefundene Dateien: 3
📈 Gesamte Process-Events: 142
🔍 Nach Prozessname 'chrome.exe' gefiltert: 28

📋 Gefilterte Events:
Timestamp           EventType ProcessName ProcessId UserSid                                     
---------           --------- ----------- --------- -------                                     
2025-06-15 09:15:32 Start     chrome.exe  1234      S-1-5-21-123456789-123456789-123456789-1001
2025-06-15 09:15:45 Stop      chrome.exe  1234      S-1-5-21-123456789-123456789-123456789-1001

📊 Statistiken:
Name        Count
----        -----
chrome.exe     28
notepad.exe    15
calc.exe        8
```

# Lizenz

MIT – freie Nutzung, keine Garantie.
