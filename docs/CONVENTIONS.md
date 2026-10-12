# Coding Conventions – ProcessMonitorService

Stack: .NET 9 (`net9.0-windows`, Migration auf .NET 10 LTS in I3) | C# 13 (SDK-Default für .NET 9) | Worker Service
(Windows-Dienst + interaktiver CLI-Modus) — dazu PowerShell-Betriebsskripte (Abschnitt 12)

> Gilt **zusammen mit** `docs/CONVENTIONS_COMMON.md` (stack-übergreifend: Commit-Format, Encoding,
> öffentliche Repos/Interna, Quality Gates, parallele Agenten). Diese Datei regelt die
> Stack-Spezifika und hat dort Vorrang.

> **Abweichung von der Template-Vorlage (`csharp-console`):** Private Felder heißen `_camelCase` (Microsoft-C#-
> Namenskonvention und durchgängig im Bestand), nicht `camelCase` ohne Präfix. Der Abschnitt 12 (PowerShell) stammt
> sinngemäß aus dem Template-Stack `powershell-automation` (Gates, Encoding), weil das Projekt beide Sprachen enthält.

**Bestand vs. Regel:** Der Code von 2025 hält einige Regeln unten noch nicht ein (eine Datei mit fünf Typen, globaler
bzw. block-scoped Namespace, `.Wait()` in `RestartWatchers`, keine Tests). Die Regeln gelten für **neuen und
geänderten** Code; der Abbau des Bestands läuft über die Inkremente in `docs/TODO.md` und `docs/BACKLOG.md`
(Refactoring-Kandidaten), nicht als Nebenbei-Änderung.

---

## 1. Git & Versionierung

Commit-Format, Scope-Wahl, `git add`-Regeln und Zeilenenden: `docs/CONVENTIONS_COMMON.md` §1–§2;
Commit-Pflicht pro Inkrement: `docs/KICKOFF.md` Phase 3b. Stack-spezifisch nur:

- Scope-Beispiele: `feat(I4): extract ProcessFilter`, `fix(install): run ACL script before service start`

## 2. C# Code-Richtlinien

Verbindlich (eine `.editorconfig` mit diesen Regeln fehlt noch → `docs/BACKLOG.md`):

- **Naming:** `PascalCase` für Typen, Methoden, Properties, Events. Interfaces mit `I`-Präfix. Private Felder `_camelCase` (Microsoft-Konvention).
- **`var`:** Verwenden, wenn der Typ aus dem Ausdruck offensichtlich ist; explizite Typen für Builtins (`int`, `string`) und wo der Typ nicht offensichtlich ist.
- **Expression-bodied members:** Ja für Properties, Accessors, Indexer, Lambdas. Nein für Konstruktoren, Methoden, Operatoren — dort Block-Bodies.
- **Namespaces:** File-scoped (`namespace ProcessMonitorService;`), nicht block-scoped und nicht global (Bestand: `ProcessMonitorWorker.cs` ohne Namespace).
- **Braces:** Immer, auch bei einzeiligen Bodies. Öffnende Klammer auf eigener Zeile.
- **`using`-Direktiven:** Außen vor dem Namespace, `System.*` zuerst, Gruppen durch Leerzeile getrennt. `ImplicitUsings` ist aktiv — keine Duplikate impliziter Usings.
- **Nullable:** Aktiv (`<Nullable>enable</Nullable>`). Nullable-Warnings werden ernst genommen, nicht stumm unterdrückt.
- **Klammern:** In binären Ausdrücken zur Klarheit, auch wenn nicht zwingend.

## 3. Fehlerbehandlung

- Der Dienst kennt heute keine eigenen Exceptions; Fehler werden an den Boundaries (WMI-Event-Handler, Timer-Callbacks,
  `ExecuteAsync`, `Main`) gefangen und geloggt. Eigene Exceptions erst einführen, wenn eine Stelle sie gezielt
  behandeln muss (z. B. `InvalidFilterPatternException` bei der Options-Validierung in I4) — dann unter einer
  gemeinsamen Basis `ProcessMonitorException : Exception`.
- **Niemals** nackte `throw new Exception(...)`.
- **Niemals** leeres `catch {}` ohne Logging.
- Event-Handler (`EventArrived`) und Timer-Callbacks dürfen **keine** Exception entkommen lassen — sie laufen auf
  Fremd-Threads, ein Fehler dort beendet sonst den Prozess bzw. verschwindet still.
- `catch (Exception ex) when (...)` für gezieltes Filtern. Weiterwerfen mit `throw;` (ohne Stack-Verlust); `LogError`
  nur an der Boundary, die den Fehler endgültig behandelt — nicht loggen und weiterwerfen (kein doppeltes Logging,
  `docs/architecture/ERROR_HANDLING.md`).
- Kritischer Fehler in `ExecuteAsync`: loggen und den Prozess mit Exit-Code ≠ 0 beenden, damit die
  Dienst-Wiederherstellung greift (umgesetzt in I6).

Siehe `docs/architecture/ERROR_HANDLING.md`.

## 4. Logging

- `Microsoft.Extensions.Logging` (`ILogger<T>`) über DI; Serilog als Provider (Konfiguration in `appsettings.json`).
- Structured Logging mit Message Templates: `_logger.LogInformation("Prozess {ProcessName} gestartet", name);` — niemals String-Interpolation in Log-Calls.
- Property-Namen der Prozessereignisse (`EventType`, `ProcessName`, `ProcessId`, `UserSid`, `ParentName`,
  `ParentProcessId`, `ExecutablePath`, `CommandLine`) sind Schnittstelle zu `process-JSONFiles.ps1` — Änderungen nur zusammen.
- Logging-Level wie in `principles/LOGGING.md` (projektspezifischer Teil dort).

## 5. Dateiorganisation

- **Eine Klasse pro Datei.** Dateiname == Klassenname (Bestand: `ProcessMonitorWorker.cs` enthält fünf Typen → Refactoring-Kandidat).
- Dateigröße als Richtwert ≤ 400 Zeilen.
- Namespace-Struktur entspricht Ordner-Struktur.
- Tests unter `tests/` (ab I4: `tests/ProcessMonitorService.UnitTests/`; Pester-Tests der Skripte unter `tests/*.Tests.ps1`).

## 6. Konfiguration

- `appsettings.json` + `appsettings.{Environment}.json` + Environment-Variables + CLI-Args (`Microsoft.Extensions.Configuration`, via `Host.CreateDefaultBuilder`).
- `IOptionsMonitor<ProcessMonitorOptions>` für die Sektion `ProcessMonitor` (Reload zur Laufzeit). Neue Optionen:
  Default in `ProcessMonitorOptions`, Eintrag in `appsettings.json`, README und `docs/architecture/CONFIGURATION.md`.
  Validierung via `IValidateOptions<T>` (ab I4).
- Secrets niemals in `appsettings.*.json` — der Dienst braucht heute keine; siehe `docs/operations/SECRETS_MANAGEMENT.md`.

## 7. Asynchronität

- `async`/`await` durchgängig wenn I/O. Keine `.Result` / `.Wait()` (Deadlock-Risiko; Bestand: `RestartWatchers`).
- `CancellationToken` als letzter Parameter in async-Methoden — auch in interner Logik durchreichen.
- `ConfigureAwait(false)` in Library-Code (nicht in Top-Level-App-Code mit SynchronizationContext).
- Fire-and-forget (`_ = Task.Run(...)` in den Event-Handlern) nur mit vollständigem `try/catch` im Task-Körper.

## 8. Security-Checkliste vor jedem Commit

- [ ] Keine Hardcoded-Secrets — `appsettings.*` ohne Credentials
- [ ] `appsettings.Production.json` und weitere Umgebungsdateien außer `appsettings.Development.json` in `.gitignore`
- [ ] WQL nur über `EscapeWqlString`/`ProcessFilter` zusammensetzen — keine ungeprüften Strings in Queries
- [ ] Keine neuen Log-Felder mit personenbezogenen oder geheimen Daten ohne Prüfung gegen `docs/security/DATA_HANDLING.md`
- [ ] `Process.Start` (falls je nötig) mit `ProcessStartInfo` + `ArgumentList` (kein Shell-String)
- [ ] Keine Stack-Traces in Nutzer-Ausgabe (Console im CLI-Modus)
- [ ] `IDisposable`-Ressourcen (CIM-Session, `ManagementEventWatcher`, Timer) via `using` bzw. `Dispose`
- [ ] Dateien, die der Dienst (LocalSystem) liest oder schreibt, bleiben auf Administratoren/SYSTEM beschränkt

---

## 9. Autonomes Arbeiten & Fehlerbehebung

- Bei Fehlern: Stack-Trace lesen, Log-Level temporär auf Debug erhöhen (`Serilog:MinimumLevel:Override:ProcessMonitorWorker`).
- Nur Dateien verändern, die für das aktuelle Inkrement relevant sind.
- Dienst nicht auf Entwickler- oder Produktivrechnern installieren, um etwas „schnell zu prüfen“ — dafür eine Test-VM.
- Am Ende jedes Inkrements: `docs/TODO.md`, `docs/CHANGELOG.md`, `docs/STATE.md` aktualisieren.

---

## 10. Dokumentationspflichten (Trigger-Regeln)

Vollständige Trigger-Matrix in `docs/KICKOFF.md` Phase 3. Kurzfassung:

### Neue Klasse / Service / Projekt-Referenz
→ `docs/architecture/CSHARP_PROJECT.md` + `docs/architecture/OVERVIEW.md`

### Neues NuGet-Paket
→ `docs/architecture/DEPENDENCIES.md` + `docs/security/DEPENDENCY_AUDIT.md`

### Neue Konfigurationsoption / neuer Skript-Parameter
→ `README.md` + `docs/architecture/CONFIGURATION.md` + `docs/features/<feature>.md`

---

## 11. Quality Gates — pre-commit (Definition of Done)

Die Gates der Definition of Done müssen lokal grün sein:

```bash
dotnet format ProcessMonitorService.sln --verify-no-changes    # Gate 1+2 — Style/Format (Whitespace, Usings, Naming)
dotnet build ProcessMonitorService.sln -warnaserror             # Gate 3 — Compiler + Roslyn-Analyzer als Fehler
dotnet test tests/ProcessMonitorService.UnitTests               # Gate 4 — Unit-Tests (ab I4; vorher kein Testprojekt)
```

**Stand beim Init (2026-10-11):** Kein Gate wurde ausgeführt (Testlauf ohne Build). Gate 3 ist voraussichtlich rot
(doppelte `using System.IO;` in `Program.cs`, Compiler-Warnung CS0105) → wird in I3 behoben; Gate 4 existiert erst ab I4.

Gate 4 wählt das Unit-Testprojekt; Tests, die echtes WMI/CIM brauchen, tragen `[Trait("Category", "Integration")]`
und laufen nicht im Gate (Kernmenge der Testmarker: `docs/principles/VERTICAL_SLICE_FIRST.md`). Gibt es nur ein
Testprojekt, läuft Gate 4 mit `dotnet test --filter "Category!=Integration&Category!=Live"`.

`dotnet format` braucht die Solution bzw. das Projekt als Argument, sobald `.sln` und `.csproj` im selben Ordner
liegen (das ist hier der Fall). `-warnaserror` macht Compiler- und Analyzer-Warnungen zu Build-Fehlern; alternativ
`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` in `Directory.Build.props` setzen.

---

## 12. PowerShell-Skripte (Install, Deploy, Log-Analyse)

Herkunft: sinngemäß aus Template-Stack `powershell-automation` (`CONVENTIONS.md` §9 Quality Gates, Encoding-Ausnahmen),
auf dieses Projekt reduziert.

- **Zielversion je Skript im Kopf festhalten** (`#Requires -Version …`) und einhalten: `install.ps1`, `uninstall.ps1`,
  `Set-SecureACLs.ps1`, `Deploy-ProcessMonitorService.ps1` müssen unter Windows PowerShell 5.1 laufen (Zielrechner);
  `process-JSONFiles.ps1` nutzt PowerShell-7-Syntax (KNOWN_ISSUES K2, I7).
- **Encoding-Ausnahme** (Vorrang vor `CONVENTIONS_COMMON.md` §2): Skripte, die unter Windows PowerShell 5.1 laufen
  sollen, enthalten nur ASCII **oder** sind UTF-8 **mit BOM** gespeichert. Grund: 5.1 liest BOM-lose Dateien als
  ANSI-Codepage; Umlaute werden verstümmelt, bestimmte Mehrbyte-Zeichen (z. B. Emojis) ergeben typografische
  Anführungszeichen und brechen das Parsing.
- **Zeilenenden:** LF im Repo (`.gitattributes`); PowerShell 5.1 und 7 verarbeiten LF problemlos.
- Adminpflicht über `#Requires -RunAsAdministrator`; keine interaktiven `Read-Host`-Abfragen in Pfaden, die
  `Deploy-ProcessMonitorService.ps1` remote ausführt.
- Externe Programme (`sc.exe`, `robocopy`, `icacls`): `$LASTEXITCODE` prüfen (Robocopy: 0–7 = Erfolg).

**Gates für PowerShell** (laufen im selben Gate-Lauf mit, `CONVENTIONS_COMMON.md` §5):

```powershell
Invoke-ScriptAnalyzer -Path . -Severity Warning                 # Gate 1 — Linter (PSScriptAnalyzer), Repo-Root ohne Rekursion in bin/obj
Invoke-Pester tests/ -ExcludeTag Integration -Output Detailed   # Gate 4 — Unit-Tests (Pester; ab I2)
```

Gate 2 und Gate 3 haben in PowerShell kein eigenständiges Werkzeug; PSScriptAnalyzer deckt Stilfragen mit ab.
