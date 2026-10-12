# Setup – ProcessMonitorService

---

## Voraussetzungen

- Windows (WMI/CIM, Windows-Dienst; `net9.0-windows`)
- .NET SDK 9 (`winget install Microsoft.DotNet.SDK.9`); ab I3 .NET SDK 10 und `global.json`
- Administratorrechte für Ausführung, Dienstinstallation und Zugriff auf `C:\ProgramData\ProcessMonitorService`
- PowerShell 5.1 für Install-/Deploy-Skripte, PowerShell 7 (`pwsh`) für `process-JSONFiles.ps1` (KNOWN_ISSUES K2)
- Optional: Visual Studio 2022, Rider oder VS Code mit C#-Erweiterung
- Für die Skript-Gates: PSScriptAnalyzer, ab I2 Pester 5 (`docs/architecture/DEPENDENCIES.md`)

Entwicklung und Tests von Dienst-Installation und Deployment auf einer **Test-VM**, nicht auf dem eigenen Arbeitsrechner.

---

## Erst-Setup

```powershell
# Repo clonen, in Projekt-Root wechseln
git clone https://github.com/gitnol/ProcessMonitorService.git
cd ProcessMonitorService

# Interna-Hook aktivieren (öffentliches Repo, docs/CONVENTIONS_COMMON.md 3.1) und Denylist anlegen
git config core.hooksPath tools/git-hooks
# .internal-terms im Repo-Root anlegen (gitignored), ein interner Begriff pro Zeile

# SDK-Version prüfen
dotnet --version

# Dependencies wiederherstellen und bauen
dotnet restore
dotnet build ProcessMonitorService.sln
```

---

## Ausführung

```powershell
# CLI-Modus (Admin-Konsole), Development-Environment über launchSettings.json
dotnet run --project ProcessMonitorService.csproj

# Release-Build (self-contained), Ausgabe:
#   bin\Release\net9.0-windows\win-x64\publish\ProcessMonitorService.exe
dotnet publish -c Release -r win-x64 --self-contained true

# Veröffentlichtes Programm interaktiv starten (Strg+C beendet)
.\bin\Release\net9.0-windows\win-x64\publish\ProcessMonitorService.exe
```

Installation als Dienst und Remote-Verteilung: `docs/operations/DEPLOYMENT.md`.

---

## Tests

Noch keine Testprojekte. Ab I4 bzw. I2:

```powershell
dotnet test tests/ProcessMonitorService.UnitTests                 # Gate 4 C# (ab I4)
Invoke-Pester tests/ -ExcludeTag Integration -Output Detailed      # Gate 4 PowerShell (ab I2)
dotnet test --collect:"XPlat Code Coverage"                        # mit Coverage
```

---

## Konfiguration

| Quelle | Beispiel | Verwendung |
|---|---|---|
| `appsettings.json` | `"ProcessMonitor": { "ProcessFilters": [ "notepad.exe" ] }` | Filter, Intervalle, Serilog — maßgeblich (Reload zur Laufzeit) |
| `appsettings.{Environment}.json` | `appsettings.Development.json` | Umgebungs-Override (hat Vorrang, K12) |
| Environment-Variables | `ProcessMonitor__MaxCacheSize=20000` | nur für Schlüssel, die nicht in `appsettings.json` stehen (K12) |
| Command-Line | `--ProcessMonitor:MaxCacheSize 20000` | wie Environment-Variables |

Vollständige Hierarchie: `docs/architecture/CONFIGURATION.md`.

---

## Pflege

Trigger: neues SDK / neues Werkzeug / neue Build-Anforderung → hier eintragen.
