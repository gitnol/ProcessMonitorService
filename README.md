# ProcessMonitorService

Ein Windows-Dienst und CLI-Tool zur Überwachung gestarteter und beendeter Prozesse über WMI (CIM) Events. Die Ereignisse werden als JSON-Dateien protokolliert, inklusive Benutzer-SID, Pfad und Kommandozeile des Prozesses.

## 1. Features

- Überwacht Prozesse via `__InstanceCreationEvent` und `__InstanceDeletionEvent`
- Unterstützt Filterung nach Prozessname (`--name`) und Executable-Pfad (`--path`)
- Log-Dateien im JSON-Format (täglich), standardmäßig unter:
  `C:\ProgramData\ProcessMonitor<yyyyMMdd>_processes.json`
- Erkennt automatisch, ob als Windows-Dienst oder interaktiv gestartet
- (CLI-Modus inkl. Live-Ausgabe mit `q` zum Beenden)
- Logging enthält:
  - Prozessname
  - Prozess-ID
  - Pfad zur ausführbaren Datei
  - Kommandozeile
  - Benutzer-SID
  - Eventtyp (Start/Stop)
  - Zeitstempel

## 2. Installation

### 2.1 Kompilieren mit `.NET 9` (z. B. über Visual Studio oder CLI)

- Debug Build:

  ```powershell
  dotnet build
  ```

- Publish Build:
  wird erstellt in `\bin\Release\net9.0-windows\win-x64\publish\ProcessMonitorService.exe`
  ```powershell
  dotnet publish -c Release -r win-x64 --self-contained true
  ```

### 2.2 Dienst installieren:

- benutzt: `\bin\Release\net9.0-windows\win-x64\publish\ProcessMonitorService.exe`
- Dienst startet automatisch nach Ausführung der `install.ps1`
  ```powershell
  .\install.ps1
  ```

### 2.3 Dienst starten:

```powershell
Start-Service ProcessMonitorService
```

## 3. Deinstallation

```powershell
.\uninstall.ps1
```

## 4. Voraussetzungen

- .NET 9 SDK
- Pakete:
  ```powershell
  dotnet add package Microsoft.Extensions.Hosting --version 9.0.6
  dotnet add package Microsoft.Extensions.Hosting.WindowsServices
  dotnet add package Microsoft.Management.Infrastructure
  
  dotnet add package System.Management
  dotnet add package System.Management.Automation
  
  dotnet add package Serilog.Extensions.Hosting
  dotnet add package Serilog.Settings.Configuration
  dotnet add package Serilog.Sinks.Console
  dotnet add package Serilog.Sinks.File
  ```
- Administratorrechte für:
  - Dienstinstallation
  - Zugriff auf Prozessinformationen
  - Schreiben nach C:\ProgramData

## 5. Hinweise

- Für beendete Prozesse kann die Benutzer-SID nicht direkt mehr ausgelesen werden – sie wird daher zur Startzeit gespeichert und beim Stop-Ereignis wiederverwendet.
- Verwendet `System.Management` (WMI/CIM), daher nur unter Windows lauffähig.
- Wenn der Zugriff auf die Daten auf `Administratoren` (und `System`) beschränkt werden soll:
  ```powershell
  New-Item -Path "C:\ProgramData\ProcessMonitor" -ItemType Directory -Force
  icacls "C:\ProgramData\ProcessMonitor" /inheritance:r
  icacls "C:\ProgramData\ProcessMonitor" /grant:r "Administratoren:(OI)(CI)(F)" "SYSTEM:(OI)(CI)(F)"
  ```

## 6. Lizenz

MIT – freie Nutzung, keine Garantie.
