# Changelog – ProcessMonitorService

Format orientiert sich an [Keep a Changelog](https://keepachangelog.com/).
Einträge chronologisch absteigend (neueste oben). Jeder Eintrag entspricht
genau einem Inkrement (= einem Commit oder einer eng zusammengehörigen
Commit-Serie). Roadmap-Änderungen (KICKOFF Phase 1 Punkt 2a), Reflexions-Refreshs und
Post-Mortems erhalten einen kurzen Eintrag ohne I#.

Nicht alle Sektionen sind in jedem Eintrag nötig — leere Sektionen weglassen.

Die Code-Historie vor I1 (Juni 2025) steht nur in `git log`; dieser Changelog beginnt mit der Doku-Baseline.

---

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
