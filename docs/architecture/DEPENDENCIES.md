# Abhängigkeiten – ProcessMonitorService

Verbindet sich mit `docs/security/DEPENDENCY_AUDIT.md`. Stand: `ProcessMonitorService.csproj` am 2026-10-11.

---

## NuGet-Pakete

Versionen direkt in `ProcessMonitorService.csproj` (kein Central Package Management, kein Lock-File).

| Paket | Zweck | Version | Lizenz |
|---|---|---|---|
| `Microsoft.Extensions.Hosting` | Generic Host (DI + Konfiguration + Logging) | `9.0.6` | MIT |
| `Microsoft.Extensions.Hosting.WindowsServices` | `UseWindowsService()` — Betrieb als Windows-Dienst | `9.0.6` | MIT |
| `Microsoft.Extensions.Diagnostics.HealthChecks` | `AddHealthChecks()` — registriert, aber ungenutzt (KNOWN_ISSUES K9) | `9.0.6` | MIT |
| `System.Management` | WMI-Ereignisse (`ManagementEventWatcher`, `WqlEventQuery`) | `9.0.6` | MIT |
| `Microsoft.Management.Infrastructure` | CIM-Abfragen (`CimSession`: Owner-SID, Elternprozess-Name) | `3.0.0` | MIT |
| `Serilog.Extensions.Hosting` | Serilog als Logging-Provider (`UseSerilog`, Bootstrap-Logger) | `9.0.0` | Apache 2.0 |
| `Serilog.Settings.Configuration` | Serilog-Konfiguration aus `appsettings.json` | `9.0.0` | Apache 2.0 |
| `Serilog.Sinks.Console` | Konsolenausgabe (CLI-Modus) | `6.0.0` | Apache 2.0 |
| `Serilog.Sinks.File` | Rollierende JSON-Logdateien | `7.0.0` | Apache 2.0 |
| `Serilog.Formatting.Compact` | `CompactJsonFormatter` (CLEF) für die Logdateien | `3.0.0` | Apache 2.0 |

Lizenzangaben aus dem Gedächtnis der Paketfamilien übernommen, beim nächsten Audit gegen nuget.org prüfen
(Confidence-Block I1 im `CHANGELOG.md`).

**Konfigurierte, aber nicht referenzierte Enricher:** `appsettings.json` nennt unter `Serilog:Enrich` auch
`WithMachineName` und `WithThreadId`; die Pakete `Serilog.Enrichers.Environment`/`Serilog.Enrichers.Thread` sind nicht
referenziert. `MachineName` und `ThreadId` setzt stattdessen `Program.cs` per `Enrich.WithProperty` (KNOWN_ISSUES K3).

**Testpakete:** noch keine (ab I4: xUnit + `xunit.runner.visualstudio`, Mocking-Bibliothek nach Wahl; FluentAssertions
ab Version 8 ist für kommerzielle Nutzung lizenzpflichtig — 7.x pinnen oder `AwesomeAssertions` verwenden).

---

## .NET-SDK / Runtime

- Zielplattform `net9.0-windows` (STS, Support-Ende **2026-11-10**). Migration auf `net10.0-windows` (LTS) in I3.
- Kein `global.json` — die SDK-Version ist heute nicht gepinnt. Ab I3:

```json
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestFeature"
  }
}
```

(Die konkrete Feature-Band-Version beim Anlegen in I3 an das installierte SDK anpassen.)

- Veröffentlichung **self-contained** (`dotnet publish -c Release -r win-x64 --self-contained true`): Die Runtime wird
  mit ausgeliefert; Runtime-Sicherheitsupdates erfordern Neu-Build und Neu-Verteilung (`docs/operations/DEPLOYMENT.md`).

---

## Externe Tools / CLIs (falls genutzt)

| Tool | Zweck | Installation |
|---|---|---|
| .NET SDK 9 (ab I3: 10) | Build/Publish | `winget install Microsoft.DotNet.SDK.9` |
| `sc.exe`, `robocopy`, `icacls` | Dienstverwaltung, Kopie, ACL (Bordmittel Windows) | im Betriebssystem enthalten |
| PSScriptAnalyzer | Gate 1 für die Skripte (`docs/CONVENTIONS.md` §12) | `Install-Module PSScriptAnalyzer -Scope CurrentUser` |
| Pester 5 | Gate 4 für die Skripte (ab I2) | `Install-Module Pester -Scope CurrentUser -SkipPublisherCheck` |

---

## Externe Services

Der Dienst ruft keine Netzwerkdienste auf. Lokale Betriebssystem-Schnittstellen:

| Service | Zweck | Auth | Wo dokumentiert |
|---|---|---|---|
| WMI/CIM (`root\cimv2`, `Win32_Process`) | Prozess-Ereignisse, Owner-SID, Elternprozess | Windows-Identität des Dienstes (LocalSystem) | `security/AUTHENTICATION.md` |
| WinRM / SMB-Admin-Share (nur `Deploy-ProcessMonitorService.ps1`) | Remote-Deinstallation/-Installation, Dateikopie | Kerberos/Negotiate mit `PSCredential` bzw. Identität des Aufrufers | `security/AUTHENTICATION.md`, `architecture/REMOTE_EXECUTION.md` |

---

## Lock-File / Reproducibility

- Heute **kein** `packages.lock.json`. Empfehlung (Backlog): `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>`
  aktivieren, Lock-File committen, Build mit `dotnet restore --locked-mode`.

---

## Audit

- `dotnet list package --vulnerable --include-transitive` (setzt ein erfolgreiches `dotnet restore` voraus)
- Bei Befund: Pakete updaten oder Mitigation dokumentieren.

Siehe `docs/security/DEPENDENCY_AUDIT.md`.

---

## Pflege

Trigger: neues NuGet-Paket / Tool / Service → hier eintragen, Audit laufen lassen.
