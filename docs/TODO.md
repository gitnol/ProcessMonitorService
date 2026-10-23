# TODO – ProcessMonitorService

Einzige Quelle des Inkrementplans (KICKOFF verweist nur hierher). Pro Inkrement: Aufgaben und — falls über die
Standard-DoD hinaus nötig — zusätzliche Kriterien. Ein Inkrement gilt erst als erledigt, wenn die Standard-DoD
**und** seine eigenen Kriterien erfüllt sind.

Parkplatz für noch unspezifizierte Ideen: `BACKLOG.md`.
Bekannte Bugs ohne aktiven Fix: `KNOWN_ISSUES.md`.

Priorisierung beim Init (2026-10-11): ausnutzbare lokale Rechteausweitung zuerst (I2), dann das harte Datum
.NET-9-Support-Ende 2026-11-10 (I3), dann Testbarkeit als Voraussetzung für die Code-Härtungen (I4–I6).
Herleitung der Bedrohungen: `docs/security/THREAT_MODEL.md`.

---

## Standard-Definition of Done (gilt für jedes Inkrement — nicht abhaken, sie bleibt stehen)

- Code-Änderungen abgeschlossen, kein TODO-/FIXME-Kommentar offen
- Quality Gates grün (`CONVENTIONS_COMMON.md` §5; Befehle in `CONVENTIONS.md`)
- Integration-Tests grün, falls relevant
- Security-Checkliste aus `CONVENTIONS.md` durchgegangen (falls der Stack eine liefert)
- Trigger-Matrix in `KICKOFF.md` Phase 3a geprüft → alle relevanten Docs aktualisiert
  (`architecture/OVERVIEW.md`, `features/`, `architecture/`, `security/`, `GLOSSARY.md`, `KNOWN_ISSUES.md`, `LESSONS_LEARNED.md`)
- `STATE.md`: Tabellenzeile + „Nächster Schritt"-Abschnitt aktualisiert
- `CHANGELOG.md`: neuer Abschnitt mit Datum + Inkrement-Titel + Sections
  (Added / Changed / Fixed / Removed / Security / Confidence / Ungeprüft / Lessons Learned)
- KICKOFF Phase 3b vollständig (Struktur-Check, TODO-Verschiebung, Advocatus-Diaboli-Gate)
- Commit erstellt (`<type>(<scope>): <desc>`); Commit-Hash in `STATE.md` eingetragen

---

## Aktuell (Hohe Priorität)

### I2: Installations- und Verzeichnis-Härtung

Bedrohungen 1 und 2 (`docs/security/THREAT_MODEL.md`). Dateien: `install.ps1`, `Set-SecureACLs.ps1`,
`tests/Install.Tests.ps1` (neu), `README.md`.

**Aufgaben:**

- [ ] Pester-Tests (Tag `Unit`, alles gemockt) für zwei neue Prüffunktionen schreiben und behaviorales Rot zeigen:
      (a) „Binary-Verzeichnis nur für Administratoren/SYSTEM/TrustedInstaller schreibbar“,
      (b) „Datenverzeichnis existiert bereits mit fremdem Besitzer“
- [ ] `install.ps1`: Registrierung abbrechen, wenn `-ExePath` in einem für Nicht-Admins schreibbaren Verzeichnis liegt
      (Hinweis auf `C:\Program Files\ProcessMonitorService` bzw. `Deploy-ProcessMonitorService.ps1`)
- [ ] `Set-SecureACLs.ps1`: Besitzer explizit auf `S-1-5-32-544` setzen; vorhandenes Verzeichnis mit anderem Besitzer
      → Abbruch mit klarer Meldung statt stillem Weiterlaufen
- [ ] `install.ps1`: `Set-SecureACLs.ps1` **vor** `Start-Service` ausführen; Fehlschlag bricht die Installation ab
      (heute nur Warnung nach dem Start)
- [ ] README-Abschnitt „Installation“ an das neue Verhalten anpassen

**Zusätzliche DoD** (nur, was über die Standard-DoD hinausgeht):

- [ ] PSScriptAnalyzer ohne neue Warnungen für die geänderten Skripte (`docs/CONVENTIONS.md` §12)
- [ ] Manueller Installationstest auf einer Test-VM dokumentiert (Ergebnis generisch im CHANGELOG, Details nach `docs/local/`)

### I3: Migration auf .NET 10 LTS

Support-Ende .NET 9 am 2026-11-10 (`docs/security/DEPENDENCY_AUDIT.md`, Befund vom 2026-10-11). Dateien:
`ProcessMonitorService.csproj`, `Program.cs`, `README.md`, `global.json` (neu), `docs/architecture/DEPENDENCIES.md`.

**Aufgaben:**

- [ ] `global.json` mit .NET-10-SDK anlegen; `TargetFramework` → `net10.0-windows`
- [ ] Alle `Microsoft.Extensions.*`/`System.Management`-Pakete auf die aktuelle 10.0.x-Patch-Version, Serilog-Pakete auf
      die aktuell unterstützte Hauptversion heben; `dotnet list package --vulnerable --include-transitive` ohne Befund
- [ ] Gate 3 lauffähig machen: doppelte `using System.IO;` in `Program.cs` (Compiler-Warnung CS0105) und weitere
      Warnungen beheben, bis `dotnet build -warnaserror` grün ist
- [ ] README (Voraussetzungen, Build-Pfade `net10.0-windows`) und `docs/architecture/DEPENDENCIES.md` aktualisieren
- [ ] `MinimumBundledRuntimeVersion` im csproj auf den aktuellen 10.0.x-Patchstand heben (seit I8; Test
      `tests/RuntimePatchCheck.Tests.ps1` Untergrenze anpassen)

**Zusätzliche DoD:**

- [ ] Abschluss vor dem 2026-11-10
- [ ] Smoke-Test im CLI-Modus auf einer Test-VM (Start, ein Start-/Stop-Ereignis von `notepad.exe` im JSON-Log, sauberes Beenden)

### I4: Test-Harness und Filterlogik

Bedrohung 4 (WQL-Escaping). Dateien: neues Testprojekt `tests/ProcessMonitorService.UnitTests/`,
neue Klasse `ProcessFilter` (aus `ProcessMonitorWorker.cs` extrahiert), `ProcessMonitorWorker.cs`, `ProcessMonitorService.sln`.

**Aufgaben:**

- [ ] xUnit-Testprojekt anlegen und in die Solution aufnehmen (Gate 4 aus `docs/CONVENTIONS.md` §11 wird lauffähig)
- [ ] `ShouldMonitorProcess` und `BuildProcessFilter` in eine reine Klasse `ProcessFilter` extrahieren;
      Charakterisierungstests für das bestehende Verhalten (Exclude vor Include, Wildcards `*`/`?`, Groß-/Kleinschreibung)
      mit Mutationsprüfung statt Rot (`KICKOFF.md` Phase 2)
- [ ] Test, der für ein Filtermuster mit Apostroph bzw. `_`/`%`/`[` eine dokumentkonforme WQL-Query erwartet
      (behaviorales Rot), dann `EscapeWqlString` korrigieren: Backslash-Escape für das Apostroph statt Verdopplung;
      im `LIKE`-Muster `_`, `%`, `[` in eckige Klammern setzen
- [ ] Ungültige Filtermuster beim Laden ablehnen (Fail Fast, `IValidateOptions<ProcessMonitorOptions>`)

**Zusätzliche DoD:**

- [ ] Gate 4 (`dotnet test`) läuft in der Gate-Zeile mit

### I5: Kommandozeilen im Log absichern

Bedrohung 3. Dateien: `ProcessMonitorWorker.cs`, neue Klasse für die Maskierung, Unit-Tests, `appsettings.json`, `README.md`.

**Aufgaben:**

- [ ] Tests für eine Maskierungsfunktion (z. B. `-Password <x>`, `/p:<x>`, `--password=<x>`, `-Credential`,
      Connection-String-Muster `Password=…;`) schreiben, behaviorales Rot zeigen
- [ ] Maskierung vor dem Logging von `CommandLine` anwenden
- [ ] Neue Option `ProcessMonitor:LogCommandLine` (Default `true` = bisheriges Verhalten, nur maskiert) in
      `ProcessMonitorOptions`, `appsettings.json` und README dokumentieren
- [ ] `docs/security/DATA_HANDLING.md` und `docs/features/PROCESS_MONITORING.md` nachziehen

**Zusätzliche DoD:**

- [ ] Kein unmaskiertes Beispiel-Secret aus den Testfällen im erzeugten JSON-Log (Integrationstest oder dokumentierter Handtest)

### I9: Gepatchtes Artefakt auf alle Instanzen verteilen (Rest aus I8)

Rest aus I8 (2026-10-23): Das lokale Artefakt ist mit Runtime-Pack 9.0.21 neu gebaut und die Patchstand-Prüfung greift
beim Publish; der High-Befund aus dem Kadenz-Sweep 2026-10-23 bleibt im Betrieb offen, bis die installierten Instanzen
ersetzt sind. Blockiert I2 nach der Schweregrad-Politik. **Nutzeraufgabe** (Verteilung auf Zielrechner), kein Code.

**Aufgaben:**

- [ ] Neues Artefakt (`bin\Release\net9.0-windows\win-x64\publish\`, Runtime-Pack 9.0.21) auf alle Instanzen verteilen
      (`docs/operations/DEPLOYMENT.md` → Installation/Release); Bestand der Instanzen und ihr vorheriger Runtime-Stand
      nur in `docs/local/`
- [ ] Je Instanz Patchstand nachweisen (`System.Private.CoreLib.dll` bzw. `ProcessMonitorService.deps.json` im
      Installationsordner ≥ 9.0.21) und Post-Deploy-Verifikation aus `docs/operations/DEPLOYMENT.md`

**Zusätzliche DoD:**

- [ ] Runtime-Stand des verteilten Artefakts im CHANGELOG generisch belegt (Versionsnummer, keine Hostnamen)
- [ ] Wird I3 vorher abgeschlossen und verteilt, entfällt I9 (dann gilt der 10.0.x-Stand)

---

## Mittlere Priorität

### I6: Dienst-Robustheit und Logging-Korrekturen

Dateien: `ProcessMonitorWorker.cs`, `Program.cs`, `install.ps1`, `appsettings.json`, Tests.

**Aufgaben:**

- [ ] Kritischer Fehler in `ExecuteAsync`: nach dem Logging `Environment.Exit(1)` statt `throw` (der Kommentar „Re-throw
      to trigger service restart“ trifft seit .NET 6 nicht zu); `install.ps1` setzt Wiederherstellungsoptionen per `sc.exe failure`
- [ ] `ThreadId` nicht mehr einmalig beim Start als Konstante anreichern (KNOWN_ISSUES K3)
- [ ] Prozess bereits beendet beim SID-Lookup: `Warning`/`Debug` statt `Error` (erwartbarer Fall, `docs/principles/LOGGING.md`)
- [ ] Bootstrap-Logdatei `startup_error_*.log` nur noch bei Fehlern bzw. mit Aufbewahrungsgrenze (KNOWN_ISSUES K4)
- [ ] Timer-Intervalle bei Konfigurationsänderung neu setzen (KNOWN_ISSUES K5)
- [ ] Ende ohne Adminrechte mit Exit-Code ≠ 0 (KNOWN_ISSUES K13)

**Zusätzliche DoD:**

- [ ] Wiederherstellung nach simuliertem kritischem Fehler auf einer Test-VM nachgewiesen (Dienst startet nach Recovery-Delay neu)

### I7: Log-Analyse- und Deploy-Skript korrigieren

Bedrohung 5 (CSV-Injection). Dateien: `process-JSONFiles.ps1`, `Deploy-ProcessMonitorService.ps1`, Pester-Tests, `README.md`.

**Aufgaben:**

- [ ] `process-JSONFiles.ps1`: `#Requires -Version 7.0` (nutzt `??` und Emojis ohne BOM) **oder** 5.1-kompatibel umschreiben —
      Entscheidung im CHANGELOG begründen (KNOWN_ISSUES K2)
- [ ] CSV-Export: Zellen, die mit `=`, `+`, `-`, `@`, Tab oder CR beginnen, neutralisieren (Test zuerst)
- [ ] `Deploy-ProcessMonitorService.ps1`: `-RemoteServicePath` tatsächlich verwenden, Kopie mit den übergebenen
      Anmeldedaten (z. B. `Copy-Item -ToSession`) statt Admin-Share im Kontext des aufrufenden Benutzers,
      entwicklerspezifischen Default für `-LocalSourcePath` entfernen (KNOWN_ISSUES K7)

**Zusätzliche DoD:**

- [ ] Checkliste in `docs/architecture/REMOTE_EXECUTION.md` für das geänderte Skript abgehakt

---

## Niedrige Priorität

- (noch keine Einträge) — unpriorisierte Ideen stehen in `BACKLOG.md`

---

## Abgeschlossen

- I1 docs_template-Baseline eingeführt — [ABGESCHLOSSEN 2026-10-11]
