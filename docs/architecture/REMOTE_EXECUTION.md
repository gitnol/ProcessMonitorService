# Remote Execution – ProcessMonitorService

> **Herkunft:** übernommen aus dem Template-Stack `powershell-automation`
> (`stacks/powershell-automation/architecture/REMOTE_EXECUTION.md`, docs_template v27@5ce0f84), weil der Stack
> `csharp-console` keine Doku für Remote-Deployment per PowerShell liefert (`docs/BACKLOG.md` → Stack-Lücken).
> Auf WinRM reduziert; der SSH/SCP-Teil der Vorlage entfällt (kein Linux-Ziel).

Betrifft ausschließlich `Deploy-ProcessMonitorService.ps1`. Der Dienst selbst führt keinen Remote-Code aus.

---

## Ziel-Systeme

| Hostname / Typ | Verbindungsart | Auth-Verfahren | Remote-Aktion |
|----------------|---------------|---------------|--------------|
| Windows-Clients/-Server (Parameter `-TargetHost`, je Aufruf einer) | WinRM (`Invoke-Command`) | `PSCredential` (`-Credential` oder `Get-Credential`), Negotiate/Kerberos | `uninstall.ps1` und `install.ps1` aus `C:\Program Files\ProcessMonitorService` ausführen |
| dieselben Rechner | SMB, Admin-Share `\\<Ziel>\C$` | **Identität des aufrufenden Benutzers** — nicht die übergebenen Credentials | Publish-Ordner per Robocopy nach `C:\Program Files\ProcessMonitorService` kopieren |

Konkrete Zielrechner gehören nicht in dieses öffentliche Repository (`docs/local/`, `CONVENTIONS_COMMON.md` 3.1).

---

## Ablauf `Deploy-ProcessMonitorService.ps1`

1. Credentials abfragen, falls nicht übergeben.
2. `Test-RemoteConnection`: `Invoke-Command { $env:COMPUTERNAME }` mit den Credentials.
3. `Invoke-RemoteScriptWithRetry` (3 Versuche, 5 s Pause): vorhandenes `uninstall.ps1` remote ausführen.
4. `Copy-FilesWithValidation`: Zielordner auf dem Admin-Share anlegen, `robocopy <Quelle> \\<Ziel>\C$\Program Files\ProcessMonitorService <Muster> /E /R:3 /W:5 /MT:8 /NP`; Exit-Code > 7 = Fehler.
5. `Invoke-RemoteScriptWithRetry`: `install.ps1` remote ausführen (registriert und startet den Dienst, setzt ACLs).

---

## WinRM (Windows → Windows)

### Voraussetzungen auf dem Zielrechner

```powershell
# WinRM aktivieren (einmalig, auf dem Zielrechner, als Admin)
Enable-PSRemoting -Force

# Verbindungstest vom ausführenden Rechner
Test-WSMan -ComputerName <Zielrechner>
```

Zusätzlich für Schritt 4: Admin-Share `C$` erreichbar (Firewall-Regel „Datei- und Druckerfreigabe“) und der
**aufrufende** Benutzer hat dort Schreibrechte.

### Variablen in remote ScriptBlocks

```powershell
# Lokale Variable → $using: Prefix
$LocalVariable = "wert"
Invoke-Command -ComputerName <Zielrechner> -ScriptBlock {
    Write-Output $using:LocalVariable   # korrekt
    Write-Output $LocalVariable         # FEHLER: im remote Scope nicht definiert
}
```

Ist-Stand: Die ScriptBlocks im Deploy-Skript verwenden keine lokalen Variablen; der Zielpfad
`C:\Program Files\ProcessMonitorService` ist im ScriptBlock fest verdrahtet (deshalb wirkt `-RemoteServicePath` nicht, K7).

**Lokale Funktionen sind in remote ScriptBlocks nicht verfügbar.** Das Deploy-Skript ruft remote nur die mitkopierten,
eigenständig lauffähigen Skripte `install.ps1`/`uninstall.ps1` auf (Vorlage „Option B: Funktion in separater .ps1-Datei“);
`Write-LogMessage` wird nur lokal verwendet. So soll es bleiben.

### Fehlerbehandlung in remote ScriptBlocks

```powershell
# Fehler aus remote Block zurück zum lokalen Host propagieren
try {
    Invoke-Command -ComputerName <Zielrechner> -ScriptBlock {
        $ErrorActionPreference = 'Stop'
        # ... Aktion ...
    } -ErrorAction Stop
}
catch {
    Write-LogMessage "Fehler auf <Zielrechner>: $($_.Exception.Message)" -Level "Error"
}
```

Ist-Stand: `Invoke-Command … -ErrorAction Stop` ist gesetzt, `$ErrorActionPreference = 'Stop'` **im** ScriptBlock nicht.
`install.ps1` beendet sich im Fehlerfall mit `exit 1`, wartet vorher aber auf `Read-Host` — im Remote-Kontext nicht
für unbeaufsichtigten Betrieb geeignet (Verhalten über WinRM ungeprüft, I7).

**Logging-Regel:** Logging-Funktionen nach dem `Invoke-Command`-Block aufrufen, nicht darin. Rückgabewerte aus dem
remote Block als Variable fangen und danach loggen (so umgesetzt in `Invoke-RemoteScriptWithRetry`).

### PSSession für mehrere Operationen (Copy + Invoke) — Zielbild für I7

```powershell
# Session einmal öffnen, für Copy-Item und Invoke-Command wiederverwenden — Kopie läuft mit denselben Credentials
$Session = New-PSSession -ComputerName <Zielrechner> -Credential $Credential

try {
    Invoke-Command -Session $Session -ScriptBlock { & "$using:RemoteServicePath\uninstall.ps1" }
    Copy-Item -Path "$LocalSourcePath" -Destination $RemoteServicePath -ToSession $Session -Recurse -Force
    Invoke-Command -Session $Session -ScriptBlock { & "$using:RemoteServicePath\install.ps1" }
}
finally {
    Remove-PSSession -Session $Session
}
```

---

## Remote-Dateien und Cleanup

Das Deploy-Skript kopiert keine temporären Dateien (keine Installer, keine Zertifikate); der Zielordner ist das
dauerhafte Programmverzeichnis. Alte Dateien werden **nicht** entfernt (Robocopy ohne `/MIR`) — veraltete DLLs aus
früheren Versionen bleiben liegen. Bei einem Wechsel der Runtime-Version (I3) den Zielordner vorher leeren
(nach der Deinstallation) oder `/MIR` bewusst einsetzen.

---

## Checkliste vor Commit

- [ ] Alle `Invoke-Command`-Blöcke: lokale Variablen nur via `$using:` referenziert
- [ ] Lokale Funktionen (`Write-LogMessage`, Helfer) nicht direkt in remote Blöcken aufgerufen
- [ ] `$ErrorActionPreference = 'Stop'` am Anfang jedes remote ScriptBlocks
- [ ] Remote ausgeführte Skripte ohne interaktive Abfragen (`Read-Host`, `Get-Credential`)
- [ ] Kopierweg nutzt dieselben Credentials wie `Invoke-Command` (oder bewusst dokumentierte Ausnahme)
- [ ] Zielpfad aus dem Parameter, nicht fest verdrahtet
- [ ] PSSession in `finally`-Block geschlossen (`Remove-PSSession`)
