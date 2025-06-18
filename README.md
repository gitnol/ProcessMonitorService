- [ProcessMonitorService](#processmonitorservice)
  - [Features](#features)
  - [Beispielhafte Log-Ausgabe nach `service-<yyyyMMdd>.json`](#beispielhafte-log-ausgabe-nach-service-yyyymmddjson)
  - [Beispiel für `appsettings.json`](#beispiel-für-appsettingsjson)
  - [Filterlogik](#filterlogik)
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
- Flexible Filterung nach Prozessname mit Include- und Exclude-Filtern. Konfigurierbar über `appsettings.json`. Änderungen an dieser Datei werden automatisch erkannt und übernommen.
- Unterstützung für Wildcards (`*` und `?`) in Filtern.
- Tägliche JSON-Log-Dateien, standardmäßig gespeichert unter:  
  `C:\ProgramData\ProcessMonitorService\logs\service-<yyyyMMdd>.json`
- Automatische Erkennung, ob als Windows-Dienst oder interaktiv (CLI) gestartet.
- Protokollierte Informationen:
  - Prozessname
  - Prozess-ID
  - Pfad zur ausführbaren Datei
  - Kommandozeile
  - Benutzer-SID
  - Eventtyp (Start/Stop)
  - Zeitstempel
  - Prozessname des aufrufenden Prozesses (ParentName)
  - Prozess-ID des aufrufenden Prozesses (ParentProcessId)

## Beispielhafte Log-Ausgabe nach `service-<yyyyMMdd>.json`

```json
{
    "@t": "2025-06-18T08:49:26.0121288Z",
    "@mt": "Process {EventType}: {ProcessName} (PID: {ProcessId}) User: {UserSid} Parent: {ParentName} (PID: {ParentProcessId}) Path: {ExecutablePath} Command: {CommandLine}",
    "EventType": "Start",
    "ProcessName": "firefox.exe",
    "ProcessId": 1234,
    "UserSid": "S-1-5-21-1234567890-123456789-1234567890-1001",
    "ParentName": "firefox.exe",
    "ParentProcessId": 5678,
    "ExecutablePath": "C:\\Program Files\\Mozilla Firefox\\firefox.exe",
    "CommandLine": "\"C:\\Program Files\\Mozilla Firefox\\firefox.exe\"",
    "SourceContext": "ProcessMonitorWorker",
    "MachineName": "MYPCNAME",
    "ThreadId": 1
}
```

## Beispiel für `appsettings.json`

```json
{
  "ProcessMonitor": {
    "ProcessFilters": [ "notepad.exe", "calc.exe", "chrome*", "my-p?ocess.exe" ],
    "ProcessExcludeFilters": ["system*", "svchost.exe", "*temp*"],
    "CacheExpiryMinutes": 30,
    "StatusUpdateIntervalMinutes": 5,
    "CacheCleanupIntervalMinutes": 10
  }
}
```

```json
"Serilog": {
    "Using": [ "Serilog.Sinks.Console", "Serilog.Sinks.File" ],
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning",
        "Microsoft.Hosting.Lifetime": "Information",
        "ProcessMonitorWorker": "Information" // <-- Ändere das zu Debug, wenn du mehr Output sehen möchtest
      }
    }
```

**Konfigurationsoptionen:**

- **`ProcessFilters`**: Liste der zu überwachenden Prozesse (Include-Filter). Unterstützt Wildcards (`*` und `?`). Wenn leer oder nicht definiert, werden alle Prozesse überwacht.
- **`ProcessExcludeFilters`**: Liste der auszuschließenden Prozesse (Exclude-Filter). Unterstützt Wildcards (`*` und `?`). Diese Filter haben Vorrang vor Include-Filtern.
- **`CacheExpiryMinutes`**: Prozesse, die länger laufen als die angegebenen Minuten, werden beim Cache-Aufräumvorgang entfernt. (Standard: 30)
- **`StatusUpdateIntervalMinutes`**: Intervall für Statusmeldungen in Minuten. (Standard: 5)
- **`CacheCleanupIntervalMinutes`**: Intervall für Cache-Aufräumvorgänge in Minuten. (Standard: 10)

## Filterlogik

Die Filterlogik arbeitet nach folgendem Prinzip:

1. **Exclude-Filter (höchste Priorität)**: Prozesse, die einem Exclude-Filter entsprechen, werden **niemals** überwacht, unabhängig von Include-Filtern.

2. **Include-Filter**: 
   - Wenn Include-Filter definiert sind: Nur Prozesse, die einem Include-Filter entsprechen, werden überwacht.
   - Wenn keine Include-Filter definiert sind: Alle Prozesse werden überwacht (außer ausgeschlossene).

3. **Wildcard-Unterstützung**:
   - `*` = beliebig viele Zeichen (z.B. `chrome*` für alle Chrome-Prozesse)
   - `?` = genau ein Zeichen (z.B. `my-p?ocess.exe` für `my-process.exe`)

**Beispiele:**

```json
{
  "ProcessFilters": ["notepad.exe", "chrome*"],
  "ProcessExcludeFilters": ["*temp*", "system*"]
}
```
- ✅ Überwacht: `notepad.exe`, `chrome.exe`, `chrome_proxy.exe`
- ❌ Nicht überwacht: `notepad_temp.exe`, `system32.exe`, `calc.exe` (nicht in Include-Liste)

```json
{
  "ProcessFilters": [],
  "ProcessExcludeFilters": ["svchost.exe", "*temp*"]
}
```
- ✅ Überwacht: Alle Prozesse außer den ausgeschlossenen
- ❌ Nicht überwacht: `svchost.exe`, `notepad_temp.exe`, `temp_file.exe`

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
- Zugriff auf die Daten wird auf `Administratoren` (und `System`) beschränkt (Alternativ: `./Set-SecureACLs.ps1` - wird bei Installation ausgeführt):
  ```powershell
  New-Item -Path "C:\ProgramData\ProcessMonitorService" -ItemType Directory -Force
  icacls "C:\ProgramData\ProcessMonitorService" /inheritance:r
  icacls "C:\ProgramData\ProcessMonitorService" /grant:r "*S-1-5-32-544:(OI)(CI)(F)" "*S-1-5-18:(OI)(CI)(F)"
  ```
  
  **Wichtige Well-Known SIDs:**
  - `S-1-5-32-544` = Administratoren/Administrators
  - `S-1-5-18` = SYSTEM
  - `S-1-5-32-545` = Benutzer/Users
  - `S-1-5-11` = Authentifizierte Benutzer/Authenticated Users

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

# Kombinierte Filter mit GridView-Ausgabe und Pipeline-Weiterverarbeitung
.\process-JSONFiles.ps1 -ProcessName "chrome.exe" -EventType "All" -Days 3 -ForPipeline | Out-GridView -Title "Chrome Prozessereignisse der letzten drei Tage" -PassThru | ForEach-Object {$_}
```

**Export-Funktionen:**
```powershell
# Events in CSV-Datei exportieren
.\process-JSONFiles.ps1 -Export

# Gefilterte Events exportieren
.\process-JSONFiles.ps1 -ProcessName "chrome.exe" -Days 7 -Export

# Zeigt nur Start- und Stop-Zeiten von Prozessen an, berechnet die Laufzeit
.\process-JSONFiles.ps1 -OnlyProcessRuntimes
```

## Parameter

| Parameter | Beschreibung | Standard |
|-----------|--------------|----------|
| `-LogPath` | Pfad zu den JSON-Log-Dateien | `C:\ProgramData\ProcessMonitorService\logs\service-*.json` |
| `-ProcessName` | Filter nach Prozessname (z.B. "chrome.exe") | Alle Prozesse |
| `-EventType` | Filter nach Event-Typ: "Start", "Stop", "All" | "All" |
| `-Days` | Zeigt Events der letzten X Tage | 1 |
| `-Export` | Exportiert Ergebnisse in CSV-Datei | Nein |
| `-ForPipeline` | Ermöglicht Weiterverarbeitung der Ergebnisse über Pipeline | Nein |
| `-OnlyProcessRuntimes` | Zeigt nur Start- und Stop-Zeiten von Prozessen an, berechnet die Laufzeit | Nein |

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