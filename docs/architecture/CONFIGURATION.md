# Konfiguration – ProcessMonitorService

Wo Werte herkommen, in welcher Reihenfolge sie überschreiben, und wie sie
validiert werden. Verbindet sich mit den Prinzipien [[EXPLICIT_OVER_IMPLICIT]]
und [[FAIL_FAST]].

---

## Quellen-Hierarchie (Präzedenz, höchste zuerst) — Ist-Stand

`Program.cs` nutzt `Host.CreateDefaultBuilder(args)` (registriert `appsettings.json`, `appsettings.{Environment}.json`,
im Development User-Secrets, Umgebungsvariablen, CLI-Argumente) und fügt danach in `ConfigureAppConfiguration`
**noch einmal** `appsettings.json` und `appsettings.{Environment}.json` aus `AppContext.BaseDirectory` hinzu. Später
registrierte Quellen gewinnen. Daraus folgt (aus dem Code abgeleitet, nicht per Lauf verifiziert — KNOWN_ISSUES K12):

1. **`appsettings.{Environment}.json`** aus dem Programmverzeichnis (zweite Registrierung)
2. **`appsettings.json`** aus dem Programmverzeichnis (zweite Registrierung, `optional: false`, `reloadOnChange: true`)
3. **CLI-Argumente** (`--ProcessMonitor:CacheExpiryMinutes 60`)
4. **Umgebungsvariablen** (`ProcessMonitor__CacheExpiryMinutes=60`)
5. Erste Registrierung der JSON-Dateien durch `CreateDefaultBuilder` (Content Root)
6. **Code-Defaults** in `ProcessMonitorOptions`

Folge: Schlüssel, die in `appsettings.json` stehen, lassen sich **nicht** per Umgebungsvariable oder CLI-Argument
überschreiben. Environment: als Dienst ohne `DOTNET_ENVIRONMENT` → `Production` (`appsettings.Production.json`
optional, nicht versioniert); mit `dotnet run` setzt `Properties/launchSettings.json` `Development`.

Niedrigere Quelle wird durch höhere **überschrieben**, nicht ergänzt — Ausnahme: Listen (`ProcessFilters`,
`ProcessExcludeFilters`) werden von `Microsoft.Extensions.Configuration` indexweise zusammengeführt
(Eintrag `:0`, `:1`, …); eine kürzere Liste in einer höheren Quelle lässt die überzähligen Einträge der niedrigeren stehen.

**Reload zur Laufzeit:** `appsettings.json` wird bei Änderung neu geladen (`IOptionsMonitor.OnChange`). Wirksam sofort:
Include-/Exclude-Filter (Watcher-Neustart), `CacheExpiryMinutes`, `MaxCacheSize`. **Nur beim Start** wirksam:
`StatusUpdateIntervalMinutes`, `CacheCleanupIntervalMinutes` (Timer werden nicht neu gesetzt, KNOWN_ISSUES K5) und die
Serilog-Senken (Pfad, Format, Aufbewahrung — werden beim Start gebaut). `Serilog:MinimumLevel` (inkl. `Override`) lädt
laut Doku von `Serilog.Settings.Configuration` bei Dateiänderung nach (im Lauf nicht geprüft).

---

## Validierung beim Laden

- Werte werden **einmal beim Programmstart** geladen und in einem
  **unveränderlichen** Konfigurationsobjekt abgelegt:
  - C#: `record` mit `init`-only Properties, via `IOptions<T>` bzw. `IOptionsMonitor<T>` (Reload liefert ein neues Objekt)
  - PowerShell: `Set-Variable -Option ReadOnly` schützt nur die Variable — Inhalte zusätzlich
    unveränderlich halten (z. B. `[System.Collections.ObjectModel.ReadOnlyDictionary]`)
- **Ist-Stand:** `ProcessMonitorOptions` ist eine veränderliche Klasse mit `set`-Properties; es gibt **keine**
  Validierung. Negative Intervalle führen erst beim Anlegen der Timer zu einer Exception, leere oder unsinnige
  Filtermuster werden ungeprüft in WQL übersetzt. Validierung per `IValidateOptions<ProcessMonitorOptions>` → I4.
- **Nach dem Laden darf kein ungültiger Zustand mehr möglich sein** (Fail Fast).
- Typkonvertierung (str → int/bool/Path/Uri) findet **beim Laden** statt,
  nicht bei jedem Lesezugriff.
- Bereichsvalidierungen (`MIN ≤ x ≤ MAX`), Existenzprüfungen (Pfade), Format-Checks (erlaubte Zeichen in
  Filtermustern) gehören in den Lade-Pfad.

---

## Secrets gehören NICHT in normale Konfigurationsdateien

- Der Dienst benötigt heute **keine** Secrets. `UserSecretsId` in der `.csproj` stammt aus der Projektvorlage und wird
  nicht genutzt.
- Falls künftig Secrets nötig werden: sichere Quelle (Windows Credential Store/DPAPI, Umgebungsvariable des Dienstes),
  Details in `docs/operations/SECRETS_MANAGEMENT.md`.
- Versionierte Konfigurationsdateien enthalten **nie** Secrets.

---

## Konfigurationsobjekt — Verwendung

- Funktionen erhalten Konfigwerte als **Parameter** (Default-Argument),
  nicht über globalen Zugriff. Siehe [[EXPLICIT_OVER_IMPLICIT]].
- Das vollständige Konfigurationsobjekt wird **einmal** im Einstiegspunkt
  gebaut (`IConfiguration` in `Program.cs`) und per `IOptionsMonitor<ProcessMonitorOptions>` an den Worker gereicht.
- Reine Logik (Filter, ab I4 `ProcessFilter`) **kennt das Konfigurationsobjekt nicht** —
  sie bekommt die Filterlisten als Parameter.

---

## Konfigurations-Tabelle

Sektion `ProcessMonitor` (Klasse `ProcessMonitorOptions`) und die für den Betrieb relevanten Serilog-Schlüssel.
Quelle „FILE“ = `appsettings.json`; ENV/CLI wirken nur für Schlüssel, die dort fehlen (siehe oben).

| Schlüssel | Typ | Default | Quelle(n) | Beschreibung |
|---|---|---|---|---|
| `ProcessMonitor:ProcessFilters` | `List<string>` | leer = alle Prozesse | FILE (ENV/CLI) | Include-Filter auf den Prozessnamen; Wildcards `*` und `?`; Groß-/Kleinschreibung egal |
| `ProcessMonitor:ProcessExcludeFilters` | `List<string>` | leer | FILE (ENV/CLI) | Exclude-Filter; Vorrang vor Include |
| `ProcessMonitor:CacheExpiryMinutes` | `int` | `30` | FILE (ENV/CLI) | SID-Cache-Einträge älter als dieser Wert werden bei der Bereinigung entfernt |
| `ProcessMonitor:StatusUpdateIntervalMinutes` | `int` | `5` | FILE (ENV/CLI) | Intervall der Statusmeldung (nur beim Start wirksam) |
| `ProcessMonitor:CacheCleanupIntervalMinutes` | `int` | `10` | FILE (ENV/CLI) | Intervall der Cache-Bereinigung (nur beim Start wirksam) |
| `ProcessMonitor:MaxCacheSize` | `int` | `10000` | ENV / CLI / FILE | Obergrenze des SID-Caches; bei Überlauf wird der älteste Eintrag verdrängt. Nicht in `appsettings.json` und nicht im README dokumentiert |
| `Serilog:MinimumLevel:Override:ProcessMonitorWorker` | `string` | `Information` | FILE | Auf `Debug` setzen für Filter-/Snapshot-Details |
| `Serilog:WriteTo:1:Args:path` | `string` | `C:/ProgramData/ProcessMonitorService/logs/service-.json` | FILE | Ziel der JSON-Logs (Datum wird vor `.json` eingefügt) |
| `Serilog:WriteTo:1:Args:fileSizeLimitBytes` | `long` | `10485760` | FILE | 10 MB je Datei, danach neue Datei (`rollOnFileSizeLimit: true`) |
| `Serilog:WriteTo:1:Args:retainedFileCountLimit` | `int` | `30` | FILE | Aufbewahrung nach **Dateianzahl**, nicht nach Tagen (KNOWN_ISSUES K11) |

Skript-Parameter (`install.ps1`, `Deploy-ProcessMonitorService.ps1`, `process-JSONFiles.ps1`) stehen in
`docs/features/PROCESS_MONITORING.md` bzw. `docs/features/LOG_ANALYSIS.md` und `docs/architecture/REMOTE_EXECUTION.md`.

---

## Anti-Pattern

- **Werte direkt aus `Environment.GetEnvironmentVariable` / `$env:VAR` mitten in der Logik lesen.** Versteckte Abhängigkeit.
- **Konfiguration zur Laufzeit mutieren** (z. B. `_currentOptions.MaxCacheSize = …` im Worker). Immutabilität bricht.
  Test-Setup ist die Ausnahme — dort über DI/Override-Mechanismus, nicht durch Mutation.
- **Magic Defaults im Code** (Werte an mehreren Stellen neu) statt zentralem Wert in `ProcessMonitorOptions`.
- **Option ohne Doku:** jede neue Option in `appsettings.json`, README und dieser Tabelle (Bestand: `MaxCacheSize`).

---

## Pflege

Trigger (siehe `KICKOFF.md` Phase 3a): neue Konfig-Quelle / neuer Schlüssel /
geänderte Präzedenz → diese Datei eintragen, Validierung implementieren,
Tabelle aktualisieren.
