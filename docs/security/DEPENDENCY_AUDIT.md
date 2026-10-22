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

### 2026-10-23 – Kadenz-Sweep (vorgezogen, fällig 2026-10-25)

Zeitraum: seit dem Sweep vom 2026-10-11 (Quellen als Liste abgerufen, Websuche nur ergänzend). Bestand:
`ProcessMonitorService.csproj` unverändert seit 2026-10-11 (Pakete wie in `architecture/DEPENDENCIES.md`);
`obj/project.assets.json` ist jünger als das csproj, der Scanner lief daher ohne Restore. Build-Umgebung des
Entwicklungsrechners: .NET SDK 9.0.319 (bündelt Runtime 9.0.21), PowerShell 7.6.7. Vorhandenes lokales
Publish-Artefakt (`bin\Release\net9.0-windows\win-x64\publish\`, nicht versioniert, gebaut Juni 2025): Runtime-Pack
`Microsoft.NETCore.App.Runtime.win-x64` **9.0.6** (laut `ProcessMonitorService.deps.json`, `System.Private.CoreLib.dll`
9.0.6), `msquic.dll` 2.4.8; als einziges Framework `Microsoft.NETCore.App` (kein ASP.NET Core, kein WindowsDesktop).
Skripte: `install.ps1`, `uninstall.ps1`, `Set-SecureACLs.ps1`, `Deploy-ProcessMonitorService.ps1` zielen auf Windows
PowerShell 5.1 (`#Requires -Version 5.x`, Selbst-Elevation über `powershell.exe`); `process-JSONFiles.ps1` läuft
faktisch nur unter PowerShell 7 (KNOWN_ISSUES K2).

| Prüfgegenstand | Ergebnis | Bewertung (betrifft uns / nicht, Begründung) | Quelle |
|---|---|---|---|
| NuGet-Scanner, direkte + transitive Pakete | `dotnet list ProcessMonitorService.csproj package --vulnerable --include-transitive`: „keine anfälligen Pakete" | Nein — kein bekannter Befund für die Pakete auf 9.0.6/3.0.0/Serilog-Ständen. Der Scanner prüft den gebündelten Runtime-Pack **nicht** (siehe nächste Zeilen) | NuGet-Audit-API (`https://api.nuget.org/v3/index.json`) |
| .NET 9 Oktober-Servicing | 9.0.21 vom 2026-10-13 (SDK 9.0.319), `cve-list` mit 42 CVEs (RCE, EoP, DoS, Information Disclosure, Security Feature Bypass; viele in ASP.NET Core und SDK); `support-phase` maintenance, `eol-date` 2026-11-10 | **Ja** für die self-contained Auslieferung: jede mit SDK < 9.0.319 gebaute Auslieferung bündelt eine Runtime ≤ 9.0.20; das vorhandene lokale Artefakt bündelt 9.0.6. ASP.NET-Core-CVEs: nein (Framework nicht im Artefakt). SDK-CVEs (EoP/RCE/Bypass im SDK): Build-Rechner mit 9.0.319 gepatcht | https://builds.dotnet.microsoft.com/dotnet/release-metadata/9.0/releases.json ; https://github.com/dotnet/core/blob/main/release-notes/9.0/9.0.21/9.0.21.md |
| GitHub-Advisories `dotnet/runtime` ab 2026-10-11 (14 Einträge) | Runtime-Pack `win-x64` ≤ 9.0.20 (Fix 9.0.21): CVE-2026-70197 (RCE, High, nur Windows-Packs), CVE-2026-69651, CVE-2026-58280, CVE-2026-96233 (DoS, High), CVE-2026-66822 (Information Disclosure, Medium). CVE-2026-96225/-96231 (EoP) nur Linux-/macOS-Packs. `System.Security.Cryptography.Xml`/`.Pkcs` (CVE-2026-96958/-96959/-96960/-96222/-84008/-70162). MsQuic: CVE-2026-85790 (< 2.5.12, Medium), CVE-2026-105016 (nur CI-Vorabversion) | **Ja** (High) für den Runtime-Pack: Komponente ist im Artefakt verbaut; die Ausnutzungsbedingungen von CVE-2026-70197 sind nicht veröffentlicht, der Dienst läuft als LocalSystem → Schweregrad-Politik High, Maßnahme I8. Cryptography.Xml/Pkcs: nein — nicht im Paketgraph (`project.assets.json`). CVE-2026-85790: `msquic.dll` 2.4.8 liegt im Artefakt, der Dienst nutzt kein QUIC und keine Netzwerkverbindung → praktisch nein, mit dem Neubau erledigt. CVE-2026-105016: nein | `gh api repos/dotnet/runtime/security-advisories` |
| GitHub Advisory Database, Ökosystem NuGet, veröffentlicht ab 2026-10-11 | Keine Advisory zu `Serilog.*`, `Microsoft.Extensions.Hosting*`, `Microsoft.Extensions.Diagnostics.HealthChecks`, `System.Management`, `Microsoft.Management.Infrastructure`. Repo-Advisories `serilog/serilog`, `serilog-sinks-file`, `-sinks-console`, `-settings-configuration`, `-extensions-hosting`, `-formatting-compact`, `dotnet/extensions`, `PowerShell/MMI`: leer | Nein | `gh api "/advisories?ecosystem=nuget&published=>=2026-10-11"` ; `gh api repos/<org>/<repo>/security-advisories` |
| PowerShell 7 Advisories, Kanal Dateien (`pwsh/2026/10`, `pwsh/2026/09`) | `CVE-2026-96251.md` (RCE, alle Betriebssysteme) und `CVE-2026-96303.md` (Security Feature Bypass, nur Windows), beide 2026-10-13, behoben in 7.4.21 / 7.5.12 / 7.6.7; Ordner `2026/09` existiert nicht | Bedingt — beide betreffen nur PowerShell 7, nicht Windows PowerShell 5.1 (Ziel von Installation, Deinstallation, ACL- und Deploy-Skript). `process-JSONFiles.ps1` läuft faktisch nur unter PowerShell 7: ausführende Rechner brauchen ≥ 7.6.7 (bzw. 7.4.21/7.5.12). Entwicklungsrechner 7.6.7: gepatcht | `gh api repos/PowerShell/Announcements/contents/pwsh/2026/10` |
| PowerShell, Kanal-Abgleich Issues + Releases | Issue-Liste `PowerShell/Announcements`: kein Eintrag seit 2026-09-23 (die Oktober-Advisories fehlen dort). Releases im Zeitraum: 7.4.21/7.5.12/7.6.7 (2026-10-13) passen zu den Datei-Advisories; 7.6.8 (2026-10-22, aktuell) ohne Sicherheitsinhalt (Update-Benachrichtigung entfernt, .NET SDK 10.0.402) | Keine Kanal-Lücke: jedem Sicherheits-Release ist ein Advisory zugeordnet | `gh issue list -R PowerShell/Announcements --state all` ; `gh release list -R PowerShell/PowerShell` |
| PowerShell-/.NET-Lebenszyklus | PowerShell 7.4 (LTS, .NET 8) und 7.5 (.NET 9) enden mit ihrer .NET-Basis am 2026-11-10 (`eol-date` .NET 8 und 9); aktuelle LTS ist 7.6. .NET 10: 10.0.13 vom 2026-10-13 (SDK 10.0.402), Support bis 2028-11-14 | Ja für I7: bei `#Requires -Version 7.x` für `process-JSONFiles.ps1` mindestens 7.6 verlangen. Für I3: Ziel-Runtime ≥ 10.0.13 | https://learn.microsoft.com/en-us/powershell/scripting/install/powershell-support-lifecycle ; https://builds.dotnet.microsoft.com/dotnet/release-metadata/8.0/releases.json ; `…/10.0/releases.json` |
| Websuche, versionsspezifisch | „Serilog.Sinks.File 7.0.0 / Serilog.Settings.Configuration 9.0.0 vulnerability CVE October 2026", „System.Management / Microsoft.Management.Infrastructure NuGet vulnerability CVE 2026": keine Treffer zu den verbauten Versionen | Nein — Textsuche ist kein Vollständigkeitsbeweis, die Listen-Abrufe oben sind maßgeblich | https://www.nuget.org/packages/serilog.sinks.file |

Offen aus diesem Sweep:

- **Neubau der Auslieferung mit gepatchter Runtime** (SDK ≥ 9.0.319 → Runtime 9.0.21) und Neuverteilung; welche
  Runtime die installierten Instanzen bündeln, ist aus dem Repo nicht feststellbar (Bestand gehört nach `docs/local/`)
  → I8 (High, blockiert I2 nach der Schweregrad-Politik bis zur Mitigation).
- **Patchstand der gebündelten Runtime im Build prüfbar machen** — der NuGet-Scanner deckt den Runtime-Pack nicht ab
  → I8.
- Bedingungen von CVE-2026-70197 (MSRC-Details nicht veröffentlicht) beim nächsten Sweep nachsehen.
- Mindestversion PowerShell 7.6.7 für `process-JSONFiles.ps1` dokumentieren → I8 (README), `#Requires` → I7.

### 2026-10-11 – Init-Recherche

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
