# Datenschutz & Compliance – ProcessMonitorService

Kein formaler Compliance-Rahmen im Projekt selbst (öffentliches Open-Source-Werkzeug, MIT-Lizenz). Der **Betreiber**
verarbeitet mit dem Dienst personenbezogene Daten (Benutzer-SIDs, Programmnutzung, Kommandozeilen) und muss deshalb in
seiner Umgebung DSGVO-Grundlagen und — bei Einsatz auf Arbeitsplatzrechnern — Mitbestimmungsrechte der
Arbeitnehmervertretung klären (technische Einrichtung, die Verhalten oder Leistung überwachen kann). Diese Datei
beschreibt die Daten, die der Dienst erzeugt, damit der Betreiber das beurteilen kann; sie ist keine Rechtsberatung.

---

## Datenklassen (kanonisch)

Alle Overlays (`llm-integration`, `document-intelligence`, `compliance-tisax`) verwenden diese vier Klassen und
ergänzen nur ihre Erlaubnis-Matrix bzw. das TISAX-Mapping.

| Klasse | EN | Beispiele | An externe Dienste / Cloud-Modelle |
|---|---|---|---|
| Öffentlich | public | veröffentlichte Texte, Demo-Daten | erlaubt |
| Intern | internal | allgemeine Geschäftskommunikation, interne Prozessdokumente | nach Prüfung des Dienstes (Checkliste unten) |
| Vertraulich | confidential | Kunden- und Lieferantendaten, Verträge, Preise, Strategie | **nur mit dokumentierter Freigabe und Auftragsverarbeitungsvertrag (AVV)** |
| Streng vertraulich | restricted | Personalakten, besondere Kategorien personenbezogener Daten, Konstruktions-/Tarifgeheimnisse | **nein** — nur lokal/On-Prem; Ausnahme nur mit ausdrücklichem DSGVO-/TISAX-Sign-off |

---

## 1. Lokale Verarbeitung / Datenisolation

- Alle Daten entstehen und bleiben **lokal** auf dem überwachten Rechner; der Dienst hat keinen Netzwerkverkehr
  (WMI/CIM nur gegen den lokalen Rechner, `CimSession.Create(null)`).
- Zugriff auf die Logs haben laut ACL nur Administratoren und SYSTEM (`Set-SecureACLs.ps1`; Lücken in
  `security/THREAT_MODEL.md` Bedrohung 2). Die Log-Analyse läuft ebenfalls lokal; CSV-Exporte landen im aktuellen
  Verzeichnis des Aufrufers.
- Keine Air-Gap-Anforderung; ein Weitertransport der Logs (SIEM, Fileshare) liegt außerhalb des Projekts und braucht
  eine eigene Bewertung beim Betreiber.

---

## 2. Minimalprinzip (Data Minimization)

- An externe Dienste oder LLMs wird **nichts** weitergegeben.
- Erfasst wird je Prozessereignis: Ereignistyp, Prozessname, PID, Owner-SID, Name und PID des Elternprozesses,
  Pfad der ausführbaren Datei, vollständige Kommandozeile, Zeitstempel, Rechnername.
- Steuerung des Umfangs nur über Include-/Exclude-Filter. Die **Kommandozeile** ist das sensibelste Feld (kann Kennwörter,
  Tokens, Dateinamen mit Personenbezug enthalten) und wird heute ungekürzt und unmaskiert protokolliert → I5
  (Maskierung + Option zum Abschalten). Keine Längenbegrenzung.
- Benutzernamen werden nicht aufgelöst (nur SID) — schwacher Pseudonymisierungseffekt, für Administratoren jederzeit
  umkehrbar.

---

## 3. Output-Dateien und Speicherorte

| Datei | Inhalt | Sensitivität |
|-------|--------|-------------|
| `C:\ProgramData\ProcessMonitorService\logs\service-<Datum>[_NNN].json` | Prozessereignisse (siehe oben) + Status-/Fehlermeldungen | Vertraulich (personenbezogen, ggf. Secrets in Kommandozeilen) |
| `C:\ProgramData\ProcessMonitorService\logs\startup_error_<Zeitstempel>.log` | Start-/Fehlermeldungen des Bootstrap-Loggers | Intern |
| `ProcessEvents_<Zeitstempel>.csv` (Export von `process-JSONFiles.ps1`, aktuelles Verzeichnis) | Gefilterte Prozessereignisse inkl. Kommandozeile und SID | Vertraulich |
| Konsolenausgabe im CLI-Modus | wie JSON-Log, als Text | Vertraulich (Bildschirm, ggf. Konsolen-Mitschnitt) |

- Output-Dateien gehören ausschließlich in das ACL-geschützte Datenverzeichnis; CSV-Exporte nur in Verzeichnisse mit
  vergleichbarem Schutz.
- `.gitignore`: Build-Ausgabe (`bin/`, `obj/`, `publish/`), `*.log`, CSV-Exporte `ProcessEvents_*.csv`,
  Umgebungs-`appsettings` außer Development, `/docs/local/`, `/.internal-terms` (siehe `docs/BOOTSTRAP.md`).
  Echte Logdateien dürfen nie als Test- oder Beispieldaten ins Repository; Beispiele (README) sind fiktiv.

---

## 4. Zugriffsschutz

- Schreiben: nur der Dienst (LocalSystem). Lesen/Auswerten: Administratoren des Rechners.
- Least Privilege: Datenverzeichnis mit Besitzer Administratoren, ohne Vererbung, nur `S-1-5-32-544` und `S-1-5-18`
  (Ziel von I2); Programmverzeichnis nur für Administratoren/SYSTEM/TrustedInstaller schreibbar (Bedrohung 1).
- Remote-Deployment nutzt Administrator-Credentials des Zielrechners; sie werden nicht gespeichert
  (`architecture/REMOTE_EXECUTION.md`).
- Secret-Speicherung und Rotation: siehe `docs/operations/SECRETS_MANAGEMENT.md`

---

## 5. Datenhaltung / Retention

- Serilog-Datei-Senke: Roll täglich und bei 10 MB, **maximal 30 Dateien** (`retainedFileCountLimit`) — ältere werden
  automatisch gelöscht. Je nach Ereignisaufkommen entspricht das wenigen Tagen bis rund einem Monat (K11).
- Daten werden akkumuliert (Append), nicht überschrieben.
- `startup_error_*.log` und CSV-Exporte werden **nicht** automatisch gelöscht — verantwortlich ist der Administrator
  des Rechners bzw. wer den Export erzeugt (K4).
- Die angemessene Aufbewahrungsdauer legt der Betreiber fest (Zweckbindung); Anpassung über `appsettings.json`.

---

## 6. Compliance-Checkliste für neue Features

- [ ] Keine neuen persistenten Speicher ohne Retention-Konzept
- [ ] Kein Logging von sensiblen Inhalten (Passwörter, Nutzdaten, PII) über das dokumentierte Ereignisformat hinaus
- [ ] Externe Services nur nach Compliance-Prüfung einführen (heute: keine)
- [ ] Neues Log-Feld → Tabelle in Abschnitt 2/3 und `docs/features/PROCESS_MONITORING.md` aktualisieren
- [ ] Keine echten Log-Auszüge (SIDs, Rechnernamen, Kommandozeilen) in Issues, README, Tests oder `docs/`
