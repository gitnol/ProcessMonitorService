# Changelog – ProcessMonitorService

Format orientiert sich an [Keep a Changelog](https://keepachangelog.com/).
Einträge chronologisch absteigend (neueste oben). Jeder Eintrag entspricht
genau einem Inkrement (= einem Commit oder einer eng zusammengehörigen
Commit-Serie). Roadmap-Änderungen (KICKOFF Phase 1 Punkt 2a), Reflexions-Refreshs und
Post-Mortems erhalten einen kurzen Eintrag ohne I#.

Nicht alle Sektionen sind in jedem Eintrag nötig — leere Sektionen weglassen.

Die Code-Historie vor I1 (Juni 2025) steht nur in `git log`; dieser Changelog beginnt mit der Doku-Baseline.

---

## 2026-10-23 I8 – Auslieferung mit gepatchter Runtime neu gebaut, Patchstand prüfbar

Commit-Serie: `test(I8): add failing Pester tests for bundled runtime patch check` (5458925) →
`feat(I8): fail self-contained build below minimum bundled runtime` (ff03ba8) → `docs(I8): …`.
Anlass: Kadenz-Sweep 2026-10-23, Runtime-Pack `win-x64` ≤ 9.0.20 mit High-Advisories (Bedrohung 7).

### Added
- `ProcessMonitorService.csproj`: Property `MinimumBundledRuntimeVersion` (`9.0.21`) und Target
  `CheckBundledRuntimePatchLevel` (nach `ResolveFrameworkReferences`, nur `SelfContained=true`, nicht in
  Design-Time-Builds). Abbruch mit `PMS0001` unter der Mindestversion, mit `PMS0002`, wenn der Runtime-Pack
  `Microsoft.NETCore.App` nicht eindeutig feststellbar ist; sonst Ausgabezeile „Patchstand: …".
- `tests/RuntimePatchCheck.Tests.ps1` (Pester, Tag `Build`): erstes Testverzeichnis des Projekts.
- README: SDK ≥ 9.0.319, Abschnitt Patchstand mit Nachweisbefehl; PowerShell ≥ 7.6.7 für `process-JSONFiles.ps1`
  (CVE-2026-96251/-96303).
- `docs/operations/DEPLOYMENT.md`: Patchstand-Prüfung, Pflege der Mindestversion, Pflicht-Nachweis nach jedem Publish,
  Release-Schritt „Patchstand-Nachweis".

### Changed
- `docs/architecture/OVERVIEW.md` (Verteilungs-Flowchart mit Prüfschritt), `docs/architecture/DEPENDENCIES.md`,
  `docs/CONVENTIONS.md` (§11 Gate-Stand, §12 Build-Test in `tests/`), `docs/TODO.md` (I8 entfernt, I9 als Rest,
  I3-Aufgabe Mindestversion 10.0.x), `docs/STATE.md`.

### Security
- Neues lokales Artefakt (`bin\Release\net9.0-windows\win-x64\publish\`, nicht versioniert): Runtime-Pack
  `Microsoft.NETCore.App.Runtime.win-x64/9.0.21` laut `ProcessMonitorService.deps.json`, `System.Private.CoreLib.dll`
  `9.0.21+d280e4fb…`. Das alte Artefakt (Runtime 9.0.6) liegt umbenannt daneben (`publish_alt-runtime-9.0.6`).
- Noch **nicht verteilt** → I9 (Nutzeraufgabe). Der High-Befund bleibt im Betrieb offen und blockiert I2.
- `docs/security/DEPENDENCY_AUDIT.md`: Korrektur zum Sweep — CVE-2026-85790 ist mit dem Neubau **nicht** erledigt,
  der Runtime-Pack 9.0.21 bringt `msquic.dll` 2.5.11 (< 2.5.12) mit; Bewertung bleibt „praktisch nein" (kein QUIC).
  `docs/security/THREAT_MODEL.md` Bedrohung 7: Stand I8.

### Iterations-Log
```
ITERATION 1 — I8 Patchstand-Prüfung beim Publish
Ziel:         self-contained Publish scheitert unter der Mindestversion, gelingt mit 9.0.21
RED-Beweis:   Pester 6.1.0: "[-] bricht den Publish ab …: Expected 0 to be different from the actual value"
              (Publish mit -p:MinimumBundledRuntimeVersion=99.0.0 lief durch);
              "[-] laesst den Publish … durch …: Expected regular expression '^\d+\.\d+\.\d+$' to match <empty>"
GREEN-Beweis: "Tests Passed: 2, Failed: 0"; dotnet publish -p:MinimumBundledRuntimeVersion=99.0.0 →
              "error PMS0001: Gebuendelter Runtime-Pack 9.0.21 liegt unter der Mindestversion 99.0.0";
              regulär → "Patchstand: gebuendelter Runtime-Pack Microsoft.NETCore.App 9.0.21 (Mindestversion 9.0.21)"
Gate:         dotnet format --verify-no-changes ✓  dotnet build -warnaserror (--no-incremental) 0 Warnungen ✓
              Pester tests/ 2 passed ✓  PSScriptAnalyzer tests/ 0 ✓ (Bestandsskripte 112 Warnungen, unverändert)
Commit:       5458925 (test), ff03ba8 (feat)
```

### Confidence / Ungeprüft
- (a) Nicht getestet: Pfad `PMS0002` (Runtime-Pack nicht feststellbar) — kein Weg gefunden, ihn ohne Eingriff ins SDK
  auszulösen; Ausschluss von Design-Time-Builds (Visual Studio) nicht geprüft. Geprüft per Handlauf: framework-abhängiger
  Publish mit Mindestversion 99.0.0 läuft durch (Target greift nicht), `dotnet build -r win-x64 --self-contained true`
  mit 99.0.0 scheitert mit `PMS0001`. Keine Installation, kein Dienststart mit dem neuen Artefakt (gehört zu I9).
- (b) Ungeprüft übernommen: dass `ResolvedRuntimePack`/`FrameworkName`/`NuGetPackageVersion` in künftigen SDKs
  (.NET 10, I3) gleich heißen — fällt das weg, greift `PMS0002` (fail closed). Mit MSBuild-`VersionLessThan` werden
  Vorabversions-Suffixe nicht bewertet.
- Das neue Artefakt wurde aus dem Arbeitsbaum gebaut, dessen csproj inhaltlich ff03ba8 entspricht, aber vor dem
  docs-Commit — kein Release-Build im Sinne von `operations/DEPLOYMENT.md` („sauberer Arbeitsbaum"). Für die
  Verteilung (I9) aus dem gepushten `master` neu veröffentlichen ist die saubere Variante.
- (c) Ohne vollständiges Lesen geändert: `docs/CONVENTIONS.md`, `docs/architecture/OVERVIEW.md`,
  `docs/architecture/DEPENDENCIES.md` (jeweils nur der betroffene Abschnitt gelesen).
- Gate-3-Befund: Die Init-Annahme „CS0105 macht Gate 3 rot" ist widerlegt (0 Warnungen trotz doppelter `using`-Zeile).

### Lessons Learned
- Ein Scanner-„keine anfälligen Pakete" sagt bei self-contained Builds nichts über die gebündelte Runtime; die
  Prüfung gehört an den Build (Runtime-Pack-Version) und an das Artefakt (`deps.json`). Pauschale Aussagen wie „mit dem
  Neubau erledigt" am neuen Artefakt nachmessen (hier: MsQuic blieb unter dem Fix-Stand).

## 2026-10-11 I1 – docs_template-Baseline eingeführt

Commit-Serie: `chore: enforce LF line endings (.gitattributes + normalize)` → `chore: extend .gitignore for docs
baseline` → `docs: initialize project documentation baseline (I1)` → `docs: record I1 commit hash`.

### Added
- `docs/` nach docs_template v27@5ce0f84, Stack `csharp-console`, keine Overlays (`init_docs.sh`); alle Ausfüllstellen
  aus dem Code befüllt.
- `docs/architecture/REMOTE_EXECUTION.md` aus dem Stack `powershell-automation` (Herkunft im Dateikopf).
- `docs/features/PROCESS_MONITORING.md` (Hauptfeature) und `docs/features/LOG_ANALYSIS.md`.
- Inkrementplan I2–I7 in `docs/TODO.md`, 13 bekannte Probleme in `docs/KNOWN_ISSUES.md`, 7 Bedrohungen in
  `docs/security/THREAT_MODEL.md`.
- `AGENTS.md` + `CLAUDE.md` (reine Zeiger auf `docs/KICKOFF.md`).
- Interna-Hook `tools/git-hooks/pre-commit` + `commit-msg` (öffentliches Repository, `CONVENTIONS_COMMON.md` 3.1),
  aktiviert per `git config core.hooksPath tools/git-hooks`.
- `.claude/settings.json` mit dem Advocatus-Diaboli-Hinweis vor `git commit` (`docs/principles/ADVOCATUS_DIABOLI.md`).

### Changed
- `.gitattributes` neu (`* text=auto eol=lf`); das Repo war im Index bereits LF, nur der Windows-Arbeitsbaum war CRLF.
- `.gitignore` ergänzt: `*.pem`, `*.key`, Umgebungs-`appsettings` außer Development, CSV-Exporte der Log-Analyse,
  `/docs/local/`, `/.internal-terms`, `/.claude/settings.local.json`.
- `docs/KICKOFF.md` Phase 3b Punkt 6: das Grep-Muster für AGENT-ANWEISUNG-Kommentare als gleichwertige Regex
  `'<!--[ ]AGENT-ANWEISUNG'` geschrieben — der Template-Wortlaut wird von `validate_project_docs.sh` (Prüfung 5) selbst
  als verbliebener Kommentar gemeldet.
- `docs/GLOSSARY.md` → „Pflege“: Beispiel-Platzhalter des Template-Texts in spitze Klammern umgeschrieben, weil die
  Platzhalter-Prüfung sie als offene Ausfüllstellen meldet.

### Removed
- `architecture/DOMAIN_MODEL.md` aus der base-Kopie gelöscht (conditional, Bedingung „fachliche Tiefe“ nicht erfüllt;
  die Regeln stehen in `docs/features/PROCESS_MONITORING.md`).
- Overlay-Hinweisblock in `docs/KICKOFF.md` Phase 3a (kein Overlay) durch drei projektspezifische Trigger ersetzt;
  Abschnitt „Kennzahlen (gemessen)“ in `docs/STATE.md` gelöscht (kein Referenzmaßstab/Orakel im Projekt).

### Security
- Security-Currency-Abgleich beim Init (2026-10-11): .NET 9 Support-Ende 2026-11-10, keine GHSA-Treffer für die
  verbauten Paketnamen, Details und Quellen in `docs/security/DEPENDENCY_AUDIT.md` → Befunde.
- Bedrohungen aus dem Code: Dienst-Binary aus nicht geschütztem Pfad (LocalSystem), Besitz/ACL des Datenverzeichnisses,
  Secrets/PII in protokollierten Kommandozeilen, nicht dokumentkonformes WQL-Escaping, CSV-Injection im Export,
  Umgehung durch Namenswahl/kurzlebige Prozesse, veraltete self-contained Runtime (`docs/security/THREAT_MODEL.md`).
  Keine Secrets im Repository gefunden.

### Confidence / Ungeprüft
- (a) **Nichts gebaut oder ausgeführt:** kein `dotnet build`/`publish`, kein Dienststart, keine Skriptausführung
  (Rahmen des Laufs). Alle Verhaltensaussagen in `KNOWN_ISSUES.md`, `THREAT_MODEL.md` und `CONFIGURATION.md` sind aus
  dem Quelltext abgeleitet — insbesondere die Konfigurations-Präzedenz (K12), der Exit-Code ohne Adminrechte (K13),
  die Compiler-Warnung CS0105 und das Parse-Verhalten von `process-JSONFiles.ps1` unter PowerShell 5.1 (K2).
- (a) Kein Scanner-Lauf (`dotnet list package --vulnerable` braucht ein Restore mit Paket-Download) — Paketbefunde
  stammen aus Web-Recherche (GHSA-Suche, nuget.org, .NET-Release-Notes); eine leere Suche ist kein Beweis für Freiheit
  von Schwachstellen.
- (b) Annahmen: Lizenzangaben der Microsoft-/Serilog-Pakete nur für `Microsoft.Management.Infrastructure` und
  `Serilog.Extensions.Hosting` auf nuget.org geprüft; die übrigen aus der Paketfamilie übernommen. Die Squatting-/Junction-
  Bedrohung des Datenverzeichnisses ist ein bekanntes Muster, für dieses Projekt aber nicht praktisch nachgestellt.
- (b) Bewertung „WQL sieht keine Verdopplung `''` vor“ stützt sich auf die Microsoft-Doku zur WHERE-Klausel
  (Backslash-Escape), nicht auf einen Lauf gegen WMI; das Backslash-Verhalten innerhalb von `LIKE` ist ungeklärt.
- (c) Template-Dateien unter `docs/principles/` und `docs/REFLECTION.md` wurden nur in den Teilen gelesen, die beim Init
  befüllt werden; sie sind unverändert übernommen (außer `principles/INDEX.md` Titel und `principles/LOGGING.md`
  Projektteil, `REFLECTION.md` Projektabschnitt).
- Struktur-Check: `<Template-Root>/scripts/validate_project_docs.sh docs/ --stack csharp-console` direkt vor dem
  Doku-Commit → Exit 0. Zwei Template-Textstellen dafür angepasst (siehe „Changed“: KICKOFF-Grep-Muster, GLOSSARY-Pflege).
