# .NET-Projektstruktur – ProcessMonitorService

Konkretisiert `docs/architecture/PROJECT_LAYOUT.md` für dieses .NET-Worker-Service-Projekt.

---

## Ist-Layout (Single-Project im Repo-Root)

```
ProcessMonitorService/                 # Repo-Root = Projektordner
├── ProcessMonitorService.sln
├── ProcessMonitorService.csproj       # Sdk="Microsoft.NET.Sdk.Worker", net9.0-windows, UseWindowsService
├── Program.cs                         # Composition Root: Admin-Check, Bootstrap-Logger, Host, DI, Serilog
├── ProcessMonitorWorker.cs            # ProcessMonitorOptions, IProcessOwnerService, ProcessOwnerService,
│                                      # ProcessCacheEntry, ProcessMonitorWorker (fünf Typen, globaler Namespace)
├── appsettings.json                   # ProcessMonitor-Filter + Serilog-Senken (Reload zur Laufzeit)
├── appsettings.Development.json       # nur Logging-Defaults, ohne Wirkung auf Serilog
├── Properties/launchSettings.json     # DOTNET_ENVIRONMENT=Development für `dotnet run`
├── install.ps1 / uninstall.ps1        # Dienst registrieren/entfernen
├── Set-SecureACLs.ps1                 # ACL auf C:\ProgramData\ProcessMonitorService
├── Deploy-ProcessMonitorService.ps1   # Remote-Deployment (WinRM + Robocopy)
├── process-JSONFiles.ps1              # Log-Analyse
├── docs/
└── README.md
```

Es gibt **kein** `src/`/`tests/`, kein `global.json`, keine `Directory.Build.props`/`Directory.Packages.props` und
keine `.editorconfig`. Die `*.ps1` werden per `<None Update="*.ps1">` in Build- und Publish-Ausgabe kopiert, damit
`install.ps1` neben der `ProcessMonitorService.exe` liegt.

**Schichtzuordnung (logisch, nicht physisch getrennt):**

| Schicht | Typ / Datei |
|---|---|
| Präsentation / Composition Root | `Program.cs` |
| Anwendung | `ProcessMonitorWorker` (Ereignisverarbeitung, Filter, Cache, Timer) |
| Domäne (klein) | `ProcessMonitorOptions`, `ProcessCacheEntry`, Filterregeln in `ShouldMonitorProcess` |
| Infrastruktur | `ProcessOwnerService` (CIM-Session), `ManagementEventWatcher` (WMI), Serilog-Senken |

**Projekt-Abhängigkeiten:** keine weiteren Projekte; nur NuGet-Pakete (`docs/architecture/DEPENDENCIES.md`).

---

## Zielbild (schrittweise, kein Big Bang)

```
ProcessMonitorService/
├── ProcessMonitorService.sln
├── ProcessMonitorService.csproj        # bleibt im Root (Deploy-/Install-Pfade unverändert)
├── Filtering/ProcessFilter.cs          # ab I4: reine Filter-/WQL-Logik, testbar ohne WMI
├── ...                                 # je ein Typ pro Datei, file-scoped Namespace ProcessMonitorService
├── tests/
│   ├── ProcessMonitorService.UnitTests/  # ab I4 (xUnit)
│   └── Install.Tests.ps1                 # ab I2 (Pester)
└── global.json                         # ab I3 (.NET-10-SDK)
```

Ein Umzug nach `src/` ist **nicht** geplant, solange README, `install.ps1` und `Deploy-ProcessMonitorService.ps1` mit
Pfaden relativ zum Repo-Root arbeiten; Entscheidung bei Bedarf per ADR (`docs/architecture/ADR/ADR-000-template.md`).

Aufteilen auf mehrere `.csproj`, sobald:

- ein Library-Anteil (z. B. Filterlogik) von einem anderen Projekt wiederverwendet werden soll
- die Domäne unabhängig testbar werden muss und das Testprojekt sonst den Windows-Dienst-Host mitziehen müsste

---

## Central Package Management (CPM)

Derzeit nicht genutzt (ein Projekt; Versionen direkt in `ProcessMonitorService.csproj`). Mit dem Testprojekt (I4)
lohnt sich `Directory.Packages.props`, damit beide Projekte dieselben Versionen nutzen:

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="Microsoft.Extensions.Hosting" Version="…" />
    <PackageVersion Include="Serilog.Extensions.Hosting" Version="…" />
  </ItemGroup>
</Project>
```

In den einzelnen `.csproj`: nur `<PackageReference Include="..." />` ohne Version.

---

## Program.cs (Generic Host) — Ist-Stand

`Host.CreateDefaultBuilder(args)` + `UseWindowsService()` + `UseSerilog(...)`; Registrierungen:

```csharp
services.Configure<ProcessMonitorOptions>(hostContext.Configuration.GetSection("ProcessMonitor"));
services.AddSingleton<IProcessOwnerService, ProcessOwnerService>();
services.AddHostedService<ProcessMonitorWorker>();
services.AddHealthChecks();   // registriert, aber ohne Endpunkt/Abfrage (KNOWN_ISSUES K9)
```

Vor dem Host-Aufbau: Bootstrap-Logger (Konsole + `startup_error_<Zeitstempel>.log`) und Prüfung auf Administratorrechte
(`WindowsBuiltInRole.Administrator`); ohne Adminrechte endet das Programm mit `Log.Fatal`.

---

## Verbotene Strukturen

- **`Utils.cs` / `Helpers.cs`** als Sammelbecken — meist Schichten-Verstoß.
- **Static Service Locator** statt DI (`ServiceLocator.Get<T>()`) — zerstört Testbarkeit.
- **WMI-/CIM-Zugriffe direkt in neuer Fachlogik** — gehören hinter `IProcessOwnerService` bzw. eine Watcher-Abstraktion,
  sonst ist die Logik ohne Windows/WMI nicht testbar.
- **Weitere Typen in `ProcessMonitorWorker.cs`** — neue Typen in eigene Dateien.

---

## Pflege

Trigger: neues Projekt hinzugefügt / Abhängigkeit zwischen Projekten geändert / neue Datei mit eigenem Typ →
diese Datei + `architecture/OVERVIEW.md`.
