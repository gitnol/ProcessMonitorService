# Deployment – ProcessMonitorService

Beschreibt Build, Installation (lokal und remote), Update und Rollback des Windows-Dienstes. Stack-spezifische
Setup- und Betriebsdetails stehen in `operations/SETUP.md` und `operations/RUNBOOK.md`.

Verbindet sich mit `operations/SETUP.md` (Erst-Setup), `operations/RUNBOOK.md`
(Betrieb), `operations/SECRETS_MANAGEMENT.md` und `architecture/REMOTE_EXECUTION.md` (Remote-Deployment).

---

## Umgebungen

| Umgebung | Zweck | Datenquelle | Wer darf deployen |
|---|---|---|---|
| dev | Lokale Entwicklung, CLI-Modus mit `dotnet run` | Prozesse des Entwicklerrechners | Entwickler:in (Admin) |
| test | Test-VM für Dienstinstallation, Upgrade, Deploy-Skript | Prozesse der Test-VM | Entwickler:in / Maintainer |
| prod | Überwachte Rechner beim Betreiber | echte Prozess- und Benutzerdaten (personenbezogen) | Administrator des Betreibers |

Konkrete Rechner der Test- und Produktivumgebung stehen nicht in diesem öffentlichen Repository (`docs/local/`).
Unterschiede zwischen Umgebungen werden ausschließlich über Konfiguration gesteuert (`appsettings.json`,
`architecture/CONFIGURATION.md`), nicht über Code-Branches.

---

## Build

```powershell
dotnet publish -c Release -r win-x64 --self-contained true
```

Build-Artefakt: Ordner `bin\Release\net9.0-windows\win-x64\publish\` mit `ProcessMonitorService.exe`, Runtime-Dateien,
`appsettings.json` und den `*.ps1`-Skripten (über `<None Update="*.ps1">` kopiert). Ab I3: `net10.0-windows`.

**Reproduzierbar:**
- [ ] Dependency-Lock-File committet (`packages.lock.json`) — heute nicht vorhanden (Backlog)
- [ ] Build wird in sauberer Umgebung getestet (CI) — heute nicht vorhanden (Backlog)
- [ ] SDK-Version gepinnt (`global.json`) — ab I3

**Self-contained:** Die .NET-Runtime steckt im Artefakt. Nach jedem .NET-Patch-Day mit Sicherheitsfixes neu bauen und
neu verteilen — Windows Update patcht diese Runtime nicht (`security/DEPENDENCY_AUDIT.md`).

**Patchstand-Prüfung im Build (seit I8):** Das Target `CheckBundledRuntimePatchLevel` in `ProcessMonitorService.csproj`
läuft bei jedem self-contained Build/Publish direkt nach der Auflösung der Runtime-Packs, also vor dem Kompilieren und
bevor ein Artefakt entsteht. Es bricht ab mit

| Code | Bedeutung | Abhilfe |
|---|---|---|
| `PMS0001` | Runtime-Pack `Microsoft.NETCore.App` liegt unter `MinimumBundledRuntimeVersion` | aktuelles SDK installieren (`dotnet --list-sdks`), neu veröffentlichen |
| `PMS0002` | Runtime-Pack nicht feststellbar (kein oder mehrdeutiger Treffer) | Build-Ausgabe prüfen; nicht durch Abschalten des Targets umgehen |

Die Mindestversion steht als Property `MinimumBundledRuntimeVersion` im csproj (Stand I8: `9.0.21`, Sweep
2026-10-23). **Pflege:** nach jedem .NET-Patch-Day mit Sicherheitsfixes auf die neue Patch-Version anheben (eigener
Commit). Einmalig übersteuern (`-p:MinimumBundledRuntimeVersion=<x.y.z>`) nur zum Testen, nie für ein verteiltes
Artefakt. Framework-abhängige Builds (`--self-contained false`) prüft das Target nicht — dort patcht Microsoft Update
die installierte Runtime. Regressionstest: `Invoke-Pester tests/RuntimePatchCheck.Tests.ps1` (ruft `dotnet publish` in
ein temporäres Verzeichnis auf).

**Patchstand-Nachweis nach jedem Publish (Pflicht):** Die Build-Ausgabe enthält die Zeile
`Patchstand: gebuendelter Runtime-Pack Microsoft.NETCore.App <Version> (Mindestversion <Version>)`. Zusätzlich am
fertigen Artefakt prüfen und die Version beim Release notieren (CHANGELOG, generisch ohne Rechnernamen):

```powershell
$publish = '.\bin\Release\net9.0-windows\win-x64\publish'
Select-String -Path "$publish\ProcessMonitorService.deps.json" `
    -Pattern 'runtimepack\.Microsoft\.NETCore\.App\.Runtime\.win-x64/[\d.]+' | Select-Object -First 1
(Get-Item "$publish\System.Private.CoreLib.dll").VersionInfo.ProductVersion   # z. B. 9.0.21+<Commit>
```

Vor einem Neu-Publish den alten Publish-Ordner umbenennen oder leeren: `dotnet publish` überschreibt, entfernt aber
keine Dateien, die die neue Runtime nicht mehr mitbringt.

---

## Installation

**Lokal (Admin-Konsole, im Publish-Ordner):**

```powershell
.\install.ps1                       # registriert den Dienst (Starttyp automatisch, LocalSystem) und startet ihn
.\install.ps1 -ExePath "C:\Program Files\ProcessMonitorService\ProcessMonitorService.exe"
```

Vorher den Publish-Ordner in ein **nur für Administratoren schreibbares** Verzeichnis kopieren (empfohlen
`C:\Program Files\ProcessMonitorService`). Aus einem Benutzer- oder Entwicklerverzeichnis installiert, kann jeder
Benutzer mit Schreibrecht dort Code als LocalSystem ausführen (`security/THREAT_MODEL.md` Bedrohung 1; Prüfung in I2).

`install.ps1` entfernt einen vorhandenen Dienst gleichen Namens, legt ihn per `sc.exe create` neu an, startet ihn und
führt danach `Set-SecureACLs.ps1` aus (Reihenfolge wird in I2 umgedreht, Bedrohung 2).

**Remote:** `Deploy-ProcessMonitorService.ps1 -TargetHost <Zielrechner> -LocalSourcePath <Publish-Ordner>\*`
(Ablauf, Voraussetzungen und Einschränkungen: `architecture/REMOTE_EXECUTION.md`).

**Deinstallation:** `.\uninstall.ps1` (stoppt und entfernt den Dienst; Logs und Datenverzeichnis bleiben erhalten).

---

## Release

Das Projekt hat keine Versionsnummer, keine Tags und keine Release-Artefakte; verteilt wird der jeweilige Build.

| Schritt | Verantwortung | Verifikation |
|---|---|---|
| Dependency-Audit + Security-Currency-Abgleich (Pflicht vor jedem Release) | Maintainer | datierter Eintrag in `security/DEPENDENCY_AUDIT.md` → Befunde (`principles/SECURITY_CURRENCY.md`) |
| Build aus einem Commit auf `master` (sauberer Arbeitsbaum) | Maintainer | Commit-Hash notieren |
| Patchstand-Nachweis am Artefakt (siehe Build) | Maintainer | Runtime-Pack-Version aus `ProcessMonitorService.deps.json` ≥ `MinimumBundledRuntimeVersion`, im CHANGELOG notiert |
| Installation + Smoke-Test auf der Test-VM | Maintainer | Dienst `Running`, Start-/Stop-Ereignis von `notepad.exe` im JSON-Log, Statusmeldung erscheint |
| Freigabe | Administrator des Betreibers | manuell |
| Verteilung (lokal oder `Deploy-ProcessMonitorService.ps1`) | Administrator des Betreibers | Dienststatus je Zielrechner |
| Beobachtung | Administrator des Betreibers | 24 h Statusmeldungen und `ERROR`-Rate (`operations/MONITORING.md`) |

---

## Rollback

**Bedingung:** Dienst startet nach dem Update nicht, stoppt wiederholt oder protokolliert falsch, und der Fehler ist
nicht innerhalb eines Arbeitstags behebbar.

| Schritt | Befehl / Aktion |
|---|---|
| 1. Letzte stabile Version identifizieren | `git log --oneline` → Commit des vorherigen Builds; bzw. aufbewahrten Publish-Ordner verwenden |
| 2. Rollback ausführen | vorherigen Publish-Ordner neu bauen (`git checkout <commit>` + `dotnet publish …`) und wie oben installieren (`install.ps1` ersetzt den Dienst) |
| 3. Verifikation | Dienst `Running`, Smoke-Test wie bei Release |
| 4. Daten | keine Migrationen; Logformat ist bisher unverändert — ältere Versionen lesen/schreiben dasselbe Format |

Vorherige Publish-Ordner aufbewahren, bis die neue Version 24 h stabil lief. Robocopy im Deploy-Skript entfernt keine
alten Dateien; bei Runtime-Wechsel (I3) den Zielordner nach der Deinstallation leeren.

---

## Post-Deploy-Verifikation

- [ ] Dienst `Running`, Starttyp automatisch (`sc.exe qc ProcessMonitorService`)
- [ ] Datenverzeichnis-ACL nur Administratoren + SYSTEM (`icacls C:\ProgramData\ProcessMonitorService`)
- [ ] Binary-Verzeichnis nicht für Benutzer schreibbar (`icacls "C:\Program Files\ProcessMonitorService"`)
- [ ] Statusmeldung nach `StatusUpdateIntervalMinutes` im Log, keine `ERROR`-Häufung
- [ ] Ein Testprozess aus dem Include-Filter erzeugt Start- und Stop-Ereignis

---

## Pflege

Trigger: neue Umgebung / geänderter Build-Befehl / geänderter Rollback-Pfad / Änderung an `install.ps1`,
`Set-SecureACLs.ps1` oder Deploy-Skript → hier eintragen.
