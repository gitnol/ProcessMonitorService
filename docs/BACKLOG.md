# Backlog – ProcessMonitorService

Parkplatz für Ideen, Verbesserungen und Aufgaben, die noch **nicht** für ein
konkretes Inkrement priorisiert sind.

**Abgrenzung:**

- `TODO.md` = aktuelle, priorisierte Arbeit mit Definition of Done
- `BACKLOG.md` = noch nicht eingeplant, kein DoD nötig
- `KNOWN_ISSUES.md` = bekannte Probleme mit Workaround, nicht aktiv geplant

Wenn ein Backlog-Punkt für ein Inkrement vorgesehen wird → Verschiebung von
`BACKLOG.md` nach `TODO.md` (mit DoD ergänzen).

---

## Ideen / Features

- Ereignisquelle ohne Polling-Lücke — `Win32_ProcessStartTrace`/`Win32_ProcessStopTrace` oder ETW-Provider
  `Microsoft-Windows-Kernel-Process` statt `__InstanceCreationEvent … WITHIN 1`; schließt KNOWN_ISSUES K1 und senkt die
  WMI-Last. Vorher messen, ob die Trace-Klassen auf den Zielsystemen kurzlebige Prozesse zuverlässiger liefern
  (Berichte dazu sind uneinheitlich).
- Zusätzliche Senke Windows-Ereignisprotokoll für Lebenszyklus- und Fehlermeldungen (Dienststart/-stopp, kritische
  Fehler), damit Betriebsmonitoring ohne Zugriff auf das Datenverzeichnis möglich ist.
- Elternprozess-Ermittlung robuster: Erstellungszeit des Elternprozesses mitprüfen (PID-Wiederverwendung, K6).
- Health Check tatsächlich nutzen (z. B. Watcher-Zustand) oder `AddHealthChecks()` samt Paket entfernen (K9).

---

## Refactoring-Kandidaten

- `ProcessMonitorWorker.cs` in einzelne Dateien je Typ mit file-scoped Namespace `ProcessMonitorService` aufteilen —
  Auslöser: erstes Inkrement, das die Datei ohnehin stark ändert (I4 extrahiert bereits `ProcessFilter`).
- `RestartWatchers` ohne `.Wait()` (asynchron, mit Sperre gegen parallele Neustarts) — Auslöser: I6 oder Änderung der Reload-Logik.
- Doppelte Konfigurationsregistrierung in `Program.cs` auflösen, damit Umgebungsvariablen/CLI-Argumente wieder
  Vorrang vor `appsettings.json` haben (K12) — Auslöser: erster Bedarf an Overrides per Umgebungsvariable.
- Cache-Verdrängung ohne vollständiges Sortieren je Insert (z. B. Verdrängung in Blöcken oder über die Bereinigung) —
  Auslöser: messbare CPU-Last bei Prozess-Fluten.
- `.editorconfig` mit den Regeln aus `docs/CONVENTIONS.md` §2 anlegen, damit Gate 1+2 sie durchsetzen.

---

## Technische Schuld

- Kein CI (z. B. GitHub Actions mit den Gates aus `docs/CONVENTIONS.md` §11/§12) — jede Änderung hängt an lokaler Disziplin.
- Kein `packages.lock.json` und kein Central Package Management — Builds nicht reproduzierbar (`docs/architecture/DEPENDENCIES.md`).
- Kein SBOM (`dotnet CycloneDX`) — Komponentenliste für Audits nur aus der `.csproj`.
- `UserSecretsId` in der `.csproj` ungenutzt (Vorlagenrest) — verwirrt bei Security-Reviews.

---

## Template-Evolution / Stack-Lücken

Hier landen Projekt-Charakteristiken, für die kein passender Stack/Overlay
existierte. Jeder Eintrag ist eine potenzielle Template-Erweiterung — so sind
z.B. `react-web` und die document-intelligence-Overlays entstanden.

- Stack-Lücke: Mischprojekt „.NET-Windows-Dienst + PowerShell-Betriebsskripte“ — `csharp-console` deckt den Dienst ab,
  für die Skripte fehlen Gates (PSScriptAnalyzer/Pester), Encoding-Regel (BOM für Windows PowerShell 5.1) und
  Remote-Deploy-Doku; übernommen wurden `architecture/REMOTE_EXECUTION.md` und Teile von `CONVENTIONS.md` aus
  `powershell-automation`. Der Struktur-Check kennt nur einen Stack. — 2026-10-11 ProcessMonitorService
- Stack-Lücke: Windows-Dienst-Betrieb (Installation per `sc.exe`, Dienstkonto, Wiederherstellungsoptionen,
  ProgramData-ACLs) hat in `csharp-console` keine eigene Doku; `operations/DEPLOYMENT.md` und `operations/MONITORING.md`
  wurden von Hand ergänzt. — 2026-10-11 ProcessMonitorService

---

## Verworfen (mit Begründung)

Punkte, die geprüft und bewusst nicht weiterverfolgt wurden. Bleiben als Spur
sichtbar, damit derselbe Punkt nicht in jeder Session erneut diskutiert wird.

- ~~Umzug des Projekts nach `src/` gemäß Template-Skelett~~ — 2026-10-11 kein Nutzen bei einem einzigen Projekt;
  README, `install.ps1` und Deploy-Skript arbeiten mit Pfaden relativ zum Repo-Root (`docs/architecture/CSHARP_PROJECT.md`)
