# Dependency-Audit – ProcessMonitorService

Wann und wie Abhängigkeiten auf bekannte Schwachstellen geprüft werden.
Verbindet sich mit `architecture/DEPENDENCIES.md` (Was ist drin?) und
`security/THREAT_MODEL.md` (Bedrohung: Vulnerable Dependencies).

---

## Wann auditieren

| Auslöser | Pflicht? |
|---|---|
| Vor jedem Release | Ja |
| Bei neuer oder aktualisierter Dependency | Ja, die betroffene + Transitive |
| Nach Sicherheitsmeldung (CVE) für eine genutzte Dep | Ja, sofort |
| Monatlicher .NET-Patch-Day (zweiter Dienstag) | Ja — self-contained Deployment bündelt die Runtime, Patch nur per Neu-Build |
| Kadenz-Sweep (Scanner **und** Web-Advisory-/Best-Practice-Recherche für Base+Stack) | Default 14 Tage, Marker `**Nächster Security-Sweep:**` im `STATE.md`-Kopf — `principles/SECURITY_CURRENCY.md` |

---

## Audit-Befehle pro Stack

| Stack | Befehl | Output | Bemerkung |
|---|---|---|---|
| .NET | `dotnet list ProcessMonitorService.csproj package --vulnerable --include-transitive` | Pakete + Severity | NuGet-Audit-API; braucht vorheriges `dotnet restore`. Ab .NET 8 SDK warnt zusätzlich NuGet Audit beim Restore (`NU1901`–`NU1904`) |
| .NET-Runtime | Runtime-Version des Publish-Ordners mit den .NET-Release-Notes abgleichen | gebündelte Runtime-Version | self-contained: `ProcessMonitorService.exe` enthält die Runtime des Build-SDKs |
| PowerShell | PSScriptAnalyzer/Pester-Versionen gegen PSGallery + manueller Abgleich gegen MSRC | begrenzt | PSGallery hat keine integrierte CVE-DB — manuell prüfen |

> Ergebnis als Artefakt sichern (`docs/audits/<JJJJ-MM-TT>-deps.txt` oder
> CI-Artefakt), damit Historie nachvollziehbar bleibt.

---

## Schweregrad-Politik

| Severity | Reaktion |
|---|---|
| Critical | Sofort patchen (innerhalb 24h) oder Mitigation (Feature deaktivieren); blockiert das Inkrement |
| High | Blockiert das aktuelle Inkrement, bis gepatcht oder mitigiert (`principles/SECURITY_CURRENCY.md`) |
| Medium | Im nächsten regulären Inkrement |
| Low | Backlog; bei nächstem Major-Upgrade mitziehen |

Mitigation kann auch sein: Nutzung der verwundbaren Funktion entfernen,
falls Update nicht möglich ist.

---

## Befunde (datiert)

Ablage für Scanner- und Recherche-Ergebnisse (`principles/SECURITY_CURRENCY.md` → Artefakt). Neueste oben.
Öffentliches oder potenziell öffentliches Repo: die Betroffenheit **konkreter interner Hosts** nur in `docs/local/`,
hier nur generisch (`CONVENTIONS_COMMON.md` 3.1).

Bestand für den Abgleich: `ProcessMonitorService.csproj` (Stand 2026-10-11), siehe `architecture/DEPENDENCIES.md`.
Kein Scanner-Lauf (kein Restore im Init-Lauf) — nur Web-Recherche.

| Datum | Quelle (Scanner / URL) | Paket / Version | Befund | betrifft uns? (Begründung) | Maßnahme / Restrisiko |
|---|---|---|---|---|---|
| 2026-10-11 | https://github.com/dotnet/core/blob/main/release-notes/9.0/README.md ; https://devblogs.microsoft.com/dotnet/dotnet-sts-releases-supported-for-24-months/ | .NET 9 Runtime/SDK, `Microsoft.Extensions.*` 9.0.6, `System.Management` 9.0.6 | .NET 9 (STS) wird bis **2026-11-10** unterstützt (24-Monats-STS); danach keine Sicherheitsupdates. Nachfolger .NET 10 LTS (Support bis November 2028). | Ja — `net9.0-windows`, Dienst als LocalSystem | I3 vor dem 2026-11-10; Restrisiko Mittel bis zur Migration |
| 2026-10-11 | https://raw.githubusercontent.com/dotnet/core/main/release-notes/9.0/9.0.20/9.0.20.md | .NET 9.0.20 (2026-09-08) | Release mit sechs CVE-Fixes (Details beim MSRC „TBD“); aktualisierte Pakete u. a. `System.Management`, `Microsoft.Extensions.Hosting`, `…Hosting.WindowsServices`, `…Diagnostics.HealthChecks` auf 9.0.20 | Unklar — betroffene Komponenten nicht veröffentlicht; die Pakete im Projekt stehen auf 9.0.6, die gebündelte Runtime hängt vom Build-SDK ab | Mit I3 auf aktuelle Patch-Stände; bis dahin Builds nur mit aktuellem SDK |
| 2026-10-11 | https://github.com/advisories?query=ecosystem%3Anuget+serilog | `Serilog.*` (Extensions.Hosting 9.0.0, Settings.Configuration 9.0.0, Sinks.Console 6.0.0, Sinks.File 7.0.0, Formatting.Compact 3.0.0) | Einziger Treffer: GHSA-5x5q-cqf6-gj8r (CVE-2024-44930) in `Serilog.Enrichers.ClientInfo` | Nein — Paket nicht verwendet | keine; Serilog 10.x verfügbar (Update in I3 prüfen) |
| 2026-10-11 | https://github.com/advisories?query=ecosystem%3Anuget+System.Management ; https://www.nuget.org/packages/Microsoft.Management.Infrastructure/3.0.0 | `System.Management` 9.0.6, `Microsoft.Management.Infrastructure` 3.0.0 | Keine GHSA-Treffer; MMI 3.0.0 ist die neueste stabile Version, ohne Deprecation-/Vulnerability-Hinweis | Nein (kein bekannter Befund) — Textsuche ist kein Vollständigkeitsbeweis | Scanner-Lauf in I3 nachholen |
| 2026-10-11 | https://learn.microsoft.com/en-us/dotnet/core/extensions/windows-service | `Microsoft.Extensions.Hosting` (BackgroundService) | Best Practice: Seit .NET 6 stoppt eine unbehandelte Exception den Host **sauber**; für Dienst-Wiederherstellung `Environment.Exit(≠0)` + `sc.exe failure` | Ja — `ExecuteAsync` wirft weiter, keine Recovery-Optionen (K10) | I6 |
| 2026-10-11 | https://learn.microsoft.com/en-us/windows/win32/wmisdk/where-clause ; https://learn.microsoft.com/en-us/windows/win32/wmisdk/like-operator | WQL (WMI) | Backslash, `"` und `'` in WQL-Strings per Backslash maskieren; in `LIKE` sind `[ ]`, `^`, `%`, `_` Metazeichen, ein wörtliches `_` gehört in `[_]` | Ja — `EscapeWqlString` verdoppelt Apostrophe | I4 (Bedrohung 4) |
| 2026-10-11 | https://learn.microsoft.com/en-us/archive/blogs/winsdk/accidental-denial-of-service-through-inefficient-program-design-part-1-watching-wmi-events-for-object-creation-e-g-win32_process | WMI `__InstanceCreationEvent … WITHIN` | Intrinsische Ereignisse auf `Win32_Process` werden per Polling erzeugt; kurze Intervalle kosten Last, Polling verpasst kurzlebige Prozesse | Ja — `WITHIN 1` | K1, Backlog (ereignisbasierte Quelle) |
| 2026-10-11 | https://community.owasp.org/attacks/CSV_Injection | `process-JSONFiles.ps1` (`Export-Csv`) | Zellen mit führendem `=`, `+`, `-`, `@`, Tab, CR werden als Formel interpretiert | Ja — Felder sind benutzerkontrolliert | I7 (Bedrohung 5) |

---

## SBOM (optional)

Software Bill of Materials für regulierte Projekte:

- .NET: `dotnet CycloneDX` (Backlog: noch nicht eingerichtet)
- Format: CycloneDX oder SPDX

SBOM gehört in `docs/audits/` oder als Build-Artefakt — nicht in den
Quellbaum als rotierende Datei.

---

## Was nicht ausreicht

- **„Wir nutzen nur populäre Bibliotheken"** — populär ≠ sicher (siehe
  `event-stream`, `colors.js`, `xz-utils`).
- **GitHub Dependabot allein** — gut für externe Repos, nicht ausreichend
  für private/interne Deps oder Binaries (hier: die gebündelte Runtime).
- **Einmaliger Audit beim Projektstart** — Audits müssen wiederholt werden,
  weil neue CVEs ständig erscheinen.
- **Scanner-Lauf allein** — `dotnet list package --vulnerable` findet bekannte CVEs der Pakete, aber keine
  Härtungs-Best-Practices, keine Advisories ohne CVE-Eintrag und nicht den Patchstand der self-contained Runtime.
  Web-Recherche nach `principles/SECURITY_CURRENCY.md` ergänzt den Scanner, ersetzt ihn nicht (und umgekehrt).

---

## Pflege

Trigger (siehe `KICKOFF.md` Phase 3a): neue Dependency hinzugefügt /
CVE-Meldung erhalten / Audit-Tool geändert → diese Datei + Audit-Artefakt
aktualisieren.
