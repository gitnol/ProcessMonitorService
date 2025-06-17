# Funktion zur Überprüfung der Administrator-Rechte

if (-not ($PSVersionTable.PSVersion.Major -ge 7)) {
    Write-Error "Dieses Skript erfordert PowerShell Version 7 oder höher."
    exit 1
}
function Test-IsAdministrator {
    try {
        $currentUser = [Security.Principal.WindowsIdentity]::GetCurrent()
        $principal = New-Object Security.Principal.WindowsPrincipal($currentUser)
        return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    }
    catch {
        Write-Error "Fehler bei der Überprüfung der Administrator-Rechte: $($_.Exception.Message)"
        return $false
    }
}

# Administrator-Rechte überprüfen
Write-Host "Überprüfe Administrator-Rechte..." -ForegroundColor Yellow

if (-not (Test-IsAdministrator)) {
    Write-Host ""
    Write-Host "FEHLER: Dieses Skript muss mit Administrator-Rechten ausgeführt werden!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Lösungsoptionen:" -ForegroundColor Cyan
    Write-Host "1. PowerShell als Administrator öffnen und Skript erneut ausführen" -ForegroundColor White
    Write-Host "2. Rechtsklick auf PowerShell -> 'Als Administrator ausführen'" -ForegroundColor White
    Write-Host "3. Aus einer Administrator-Eingabeaufforderung: powershell.exe -File `"$($MyInvocation.MyCommand.Path)`"" -ForegroundColor White
    Write-Host ""
    
    # Automatischer Neustart mit Administrator-Rechten anbieten
    $restart = Read-Host "Soll das Skript automatisch mit Administrator-Rechten neu gestartet werden? (j/n)"
    if ($restart -eq 'j' -or $restart -eq 'J' -or $restart -eq 'y' -or $restart -eq 'Y') {
        try {
            Write-Host "Starte PowerShell mit Administrator-Rechten neu..." -ForegroundColor Yellow
            
            # Alle Parameter für den Neustart sammeln
            $arguments = "-File `"$($MyInvocation.MyCommand.Path)`""
            if ($PSBoundParameters.Count -gt 0) {
                foreach ($param in $PSBoundParameters.GetEnumerator()) {
                    $arguments += " -$($param.Key) `"$($param.Value)`""
                }
            }
            
            Start-Process PowerShell -Verb RunAs -ArgumentList $arguments
            exit 0
        }
        catch {
            Write-Host "Fehler beim Neustart: $($_.Exception.Message)" -ForegroundColor Red
        }
    }
    
    Write-Host "Installation abgebrochen." -ForegroundColor Red
    Read-Host "Drücke Enter zum Beenden"
    exit 1
}

Write-Host "✓ Administrator-Rechte bestätigt" -ForegroundColor Green
Write-Host ""

Write-Host "Erstelle Ordner C:\ProgramData\ProcessMonitorService ..." -ForegroundColor Yellow
New-Item -Path "C:\ProgramData\ProcessMonitorService" -ItemType Directory -Force
Write-Host "✓ Ordner C:\ProgramData\ProcessMonitorService erstellt" -ForegroundColor Green

Write-Host "Setze ACLs auf C:\ProgramData\ProcessMonitorService ..." -ForegroundColor Yellow

# ACL-Objekt erstellen und konfigurieren
$acl = Get-Acl "C:\ProgramData\ProcessMonitorService"
$acl.SetAccessRuleProtection($true, $false)  # Vererbung entfernen

# Administratoren-Gruppe
$adminSid = [System.Security.Principal.SecurityIdentifier]"S-1-5-32-544"
$adminRule = New-Object System.Security.AccessControl.FileSystemAccessRule($adminSid, "FullControl", "ContainerInherit,ObjectInherit", "None", "Allow")
$acl.SetAccessRule($adminRule)

# SYSTEM
$systemSid = [System.Security.Principal.SecurityIdentifier]"S-1-5-18"
$systemRule = New-Object System.Security.AccessControl.FileSystemAccessRule($systemSid, "FullControl", "ContainerInherit,ObjectInherit", "None", "Allow")
$acl.SetAccessRule($systemRule)

# ACL anwenden
Set-Acl "C:\ProgramData\ProcessMonitorService" $acl

Write-Host "✓ ACLs auf C:\ProgramData\ProcessMonitorService gesetzt" -ForegroundColor Green