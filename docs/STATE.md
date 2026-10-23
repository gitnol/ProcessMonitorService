# Aktueller Projektstatus – ProcessMonitorService

Zuletzt aktualisiert: 2026-10-23
**Initialisiert mit:** docs_template v27@5ce0f84 → v27@2268889 → v28@63b85ec (2026-10-22, Delta-Update)
**Stack / Overlays:** csharp-console / keine
**Letztes abgeschlossenes Inkrement:** I8 – Auslieferung mit gepatchter Runtime neu gebaut, Patchstand beim Publish geprüft (2026-10-23)
**Nächster Schritt:** I9 – gepatchtes Artefakt (Runtime 9.0.21) auf alle Instanzen verteilen (Nutzeraufgabe); danach I2 – Installations- und Verzeichnis-Härtung
**Nächste Reflexion:** bei Abschluss von I3 (siehe `KICKOFF.md` Phase 1c → `docs/REFLECTION.md` — `docs/`-Drift prüfen + Template-Backport prüfen; danach Marker um 3 erhöhen)
**Nächster Security-Sweep:** 2026-11-06 (Intervall 14 Tage; siehe `KICKOFF.md` Phase 1 Punkt 4a → `docs/principles/SECURITY_CURRENCY.md` — fällig, sobald heute ≥ diesem Datum; nach dem Sweep Marker = Sweep-Datum + Intervall)

---

## Nächster Schritt

I9 aus `docs/TODO.md` (Rest aus I8, Nutzeraufgabe): das neu gebaute lokale Artefakt
(`bin\Release\net9.0-windows\win-x64\publish\`, Runtime-Pack 9.0.21) auf alle installierten Instanzen verteilen;
Bestand der Instanzen und ihr vorheriger Runtime-Stand nur in `docs/local/`. Bis dahin bleibt der High-Befund aus dem
Kadenz-Sweep 2026-10-23 im Betrieb offen und blockiert I2 nach der Schweregrad-Politik (`docs/security/DEPENDENCY_AUDIT.md`).

Danach I2: `install.ps1` und `Set-SecureACLs.ps1` so härten, dass der als LocalSystem laufende Dienst nur
aus einem für Nicht-Admins schreibgeschützten Verzeichnis registriert wird und das Datenverzeichnis
`C:\ProgramData\ProcessMonitorService` vor dem ersten Dienststart mit festem Besitzer (Administratoren) und
restriktiver ACL angelegt wird (Bedrohungen 1 und 2 in `docs/security/THREAT_MODEL.md`). `tests/` existiert seit I8
(Pester); zuerst Pester-Tests für die Prüffunktionen anlegen (TDD-Startregel, `KICKOFF.md` Phase 2).

Zeitkritisch danach: I3 (.NET-10-Migration) — .NET 9 erhält nach dem 2026-11-10 keine Sicherheitsupdates mehr. Mit I3
`MinimumBundledRuntimeVersion` auf den aktuellen 10.0.x-Patchstand heben.

---

## Abgeschlossene Inkremente

| # | Titel | Datum | Commit |
|---|-------|-------|--------|
| I1 | docs_template-Baseline eingeführt (Bestand: Code ohne `docs/`) | 2026-10-11 | 666ff08 |
| I8 | Auslieferung mit gepatchter Runtime (9.0.21) neu gebaut, Patchstand-Prüfung beim Publish (`PMS0001`/`PMS0002`) | 2026-10-23 | ff03ba8 |

---

## Offene Punkte

| # | Beschreibung | Priorität |
|---|-------------|-----------|
| I2 | Installations- und Verzeichnis-Härtung (Binary-Pfad, Besitzer des Datenverzeichnisses, ACL vor Dienststart) | Hoch |
| I3 | Migration auf .NET 10 LTS + Paket-Updates, Quality Gates lauffähig machen | Hoch |
| I4 | Test-Harness + Filterlogik extrahieren, WQL-Escaping korrigieren | Hoch |
| I5 | Kommandozeilen im Log: Maskierung bekannter Secret-Muster, konfigurierbare Protokollierung | Hoch |
| I6 | Dienst-Robustheit: Neustart nach kritischem Fehler, Logging-Korrekturen | Mittel |
| I7 | Log-Analyse- und Deploy-Skript korrigieren (PowerShell-Version, CSV-Injection, Parameter) | Mittel |
| I9 | Gepatchtes Artefakt (Runtime 9.0.21) auf alle Instanzen verteilen (Rest aus I8, Nutzeraufgabe) | Hoch |

---

## Risiken & Blocker

Eintragen, **sobald** ein Punkt erkannt ist — nicht erst, wenn er eskaliert.

| Risiko / Blocker | Auswirkung | Mitigation / Nächster Schritt | Owner | Status |
|---|---|---|---|---|
| .NET 9 (STS) erreicht am 2026-11-10 das Support-Ende | Danach keine Sicherheitsupdates für Runtime und die 9.x-Pakete `Microsoft.Extensions.*`/`System.Management`; der Dienst läuft als LocalSystem | I3 vor dem 2026-11-10 abschließen; self-contained Builds danach neu ausrollen | Maintainer | offen |
| Keine automatisierten Tests, kein CI | Jede Änderung (auch die Sicherheits-Inkremente) ohne Regressionsschutz | Pester-Tests ab I2, xUnit-Harness in I4; Quality Gates in `docs/CONVENTIONS.md` §11 | Maintainer | offen |
| Self-contained Deployment bündelt die .NET-Runtime | Runtime-Patches über Windows/Microsoft Update greifen nicht; Patchen nur durch Neu-Build + Neu-Verteilung | Pflichtschritt je Runtime-Patch in `docs/operations/DEPLOYMENT.md`; seit I8 bricht der Publish unter `MinimumBundledRuntimeVersion` ab — die Mindestversion muss nach jedem Patch-Day angehoben werden | Maintainer | teilweise mitigiert |
| Installierte Instanzen bündeln eine Runtime ≤ 9.0.20, sofern vor dem 2026-10-13 gebaut (Stand aus dem Repo nicht feststellbar; High, Sweep 2026-10-23) | Bekannte RCE-/DoS-Advisories im als LocalSystem laufenden Dienst; blockiert I2 | I9: neues Artefakt (9.0.21) verteilen, Bestand in `docs/local/` | Nutzer | offen |
| WMI-Polling (`WITHIN 1`) erfasst sehr kurzlebige Prozesse nicht zuverlässig | Lücke in der Überwachung; Umgehung durch kurzlebige Prozesse möglich | `docs/KNOWN_ISSUES.md` K1, Alternative (ETW bzw. `Win32_ProcessStartTrace`) in `docs/BACKLOG.md` | Maintainer | offen |
| Protokollierte Kommandozeilen und Benutzer-SIDs sind personenbezogen und können Secrets enthalten | Datenschutz- und Mitbestimmungsfragen beim Betreiber; Secrets im Log | I5; Betreiber-Hinweise in `docs/security/DATA_HANDLING.md` | Maintainer | offen |
