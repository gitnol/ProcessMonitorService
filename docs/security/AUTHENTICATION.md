# Authentication und Authorization – ProcessMonitorService

---

## Auth-Anforderung

Der Dienst hat **keine Auth innerhalb der App** und keine Schnittstelle nach außen:

- Berechtigungen kommen vom Windows-Konto. Als Dienst: **LocalSystem** (`install.ps1` setzt kein `obj=`); im CLI-Modus:
  das angemeldete Konto, das Mitglied der Administratoren sein muss (`Program.cs` prüft
  `WindowsBuiltInRole.Administrator` und beendet sich sonst).
- Adminrechte sind fachlich nötig: WMI-Ereignisse auf `Win32_Process` und `GetOwnerSid` für Prozesse anderer Benutzer.
  Ein Konto mit weniger Rechten ist nicht untersucht (Backlog-Kandidat, falls Least Privilege gefordert ist).
- Autorisierung zum Lesen der Ergebnisse erfolgt ausschließlich über die NTFS-ACL des Datenverzeichnisses
  (`security/DATA_HANDLING.md` Abschnitt 4).

---

## Outbound-Auth (Aufrufe an externe Services)

| Service | Auth-Mechanismus | Credential-Quelle | Cache-Strategie |
|---|---|---|---|
| WMI/CIM lokal (`root\cimv2`) | Windows-Auth des Prozesskontos | keine (Prozessidentität) | `CimSession` einmal pro Dienstlaufzeit (`Lazy<CimSession>`) |
| WinRM zum Zielrechner (`Deploy-ProcessMonitorService.ps1`) | Negotiate/Kerberos über `Invoke-Command -Credential` | `-Credential` (`PSCredential`) oder interaktiv `Get-Credential` | nur im Speicher der aufrufenden PowerShell-Sitzung |
| SMB-Admin-Share des Zielrechners (Robocopy) | Windows-Auth des **aufrufenden** Benutzers (nicht `-Credential`, K7) | angemeldeter Benutzer | — |

---

## Credential-Quellen

| Umgebung | Quelle | Bemerkung |
|---|---|---|
| Dienstbetrieb | keine — LocalSystem | keine Secrets im Dienst |
| Remote-Deployment | `Get-Credential` bzw. übergebenes `PSCredential` | nicht persistieren, nicht in Skripte schreiben; README-Beispiel `$mycredentials = (Get-Credential)` hält sie nur in der Sitzung |
| Entwicklung | keine | `UserSecretsId` in der `.csproj` ist ungenutzt |

Vollständige Regeln: `docs/operations/SECRETS_MANAGEMENT.md`.

---

## Security-Checkliste

- [ ] Keine Hardcoded-Secrets im Quellcode oder in den Skripten
- [ ] `appsettings.Production.json` und weitere Umgebungsdateien in `.gitignore`
- [ ] Credentials für das Deployment nur als `PSCredential`, nie als Klartext-Parameter (`-Password` o. Ä.)
- [ ] Keine Credentials oder Token in Logausgaben (`Write-LogMessage`, Serilog)
- [ ] Dienstkonto bewusst gewählt und dokumentiert (heute LocalSystem)

---

## Pflege

Trigger: neuer Service / neue Credentials / Wechsel des Dienstkontos → diese Datei
+ `architecture/OVERVIEW.md` aktualisieren.
