param (
    [string]$ServiceName = "ProcessMonitorService"
)

#Requires -Version 7
#Requires -RunAsAdministrator

# Funktion für sichere Service-Deinstallation
function Remove-WindowsService {
    param (
        [string]$Name
    )
    
    try {
        $service = Get-Service -Name $Name -ErrorAction SilentlyContinue
        
        if ($null -eq $service) {
            Write-Host "✓ Dienst '$Name' ist nicht installiert." -ForegroundColor Yellow
            return $true
        }
        
        Write-Host "Stoppe Dienst '$Name'..." -ForegroundColor Yellow
        
        # Dienst stoppen mit Timeout
        if ($service.Status -eq 'Running') {
            Stop-Service -Name $Name -Force -ErrorAction Stop
            
            # Warten bis Dienst vollständig gestoppt ist
            $timeout = 30
            $timer = 0
            do {
                Start-Sleep -Seconds 1
                $timer++
                $service = Get-Service -Name $Name -ErrorAction SilentlyContinue
            } while ($service.Status -ne 'Stopped' -and $timer -lt $timeout)
            
            if ($service.Status -ne 'Stopped') {
                throw "Dienst konnte nicht innerhalb von $timeout Sekunden gestoppt werden."
            }
        }
        
        Write-Host "Lösche Dienst '$Name'..." -ForegroundColor Yellow
        
        # Dienst löschen
        $result = & sc.exe delete $Name 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "Fehler beim Löschen des Dienstes: $result"
        }
        
        Write-Host "✓ Dienst '$Name' wurde erfolgreich entfernt." -ForegroundColor Green
        return $true
        
    } catch {
        Write-Host "✗ Fehler beim Entfernen des Dienstes '$Name': $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

# Funktion für automatischen Administrator-Neustart
function Start-AsAdministrator {
    try {
        Write-Host "Starte PowerShell mit Administrator-Rechten neu..." -ForegroundColor Yellow
        
        # Parameter für Neustart zusammenstellen
        $arguments = @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "`"$($MyInvocation.MyCommand.Path)`"")
        
        # Ursprüngliche Parameter hinzufügen
        foreach ($param in $PSBoundParameters.GetEnumerator()) {
            $arguments += "-$($param.Key)"
            $arguments += "`"$($param.Value)`""
        }
        
        Start-Process -FilePath "powershell.exe" -Verb RunAs -ArgumentList $arguments
        exit 0
        
    } catch {
        Write-Host "✗ Fehler beim Neustart mit Administrator-Rechten: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

# Hauptlogik
try {
    Write-Host "=== Service Deinstallation ===" -ForegroundColor Cyan
    Write-Host "Service: $ServiceName" -ForegroundColor White
    Write-Host ""
    
    # Administrator-Rechte prüfen (redundant wegen #Requires, aber für bessere Fehlermeldung)
    if (-not ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        Write-Host "✗ Administrator-Rechte erforderlich!" -ForegroundColor Red
        Write-Host ""
        Write-Host "Lösungsoptionen:" -ForegroundColor Yellow
        Write-Host "• PowerShell als Administrator öffnen" -ForegroundColor White
        Write-Host "• Rechtsklick auf PowerShell → 'Als Administrator ausführen'" -ForegroundColor White
        Write-Host ""
        
        $restart = Read-Host "Automatisch mit Administrator-Rechten neu starten? (j/n)"
        if ($restart -match '^[jJyY]$') {
            Start-AsAdministrator
        }
        
        throw "Deinstallation abgebrochen - Administrator-Rechte erforderlich."
    }
    
    Write-Host "✓ Administrator-Rechte bestätigt" -ForegroundColor Green
    Write-Host ""
    
    # Service deinstallieren
    $success = Remove-WindowsService -Name $ServiceName
    
    Write-Host ""
    if ($success) {
        Write-Host "=== Deinstallation erfolgreich abgeschlossen ===" -ForegroundColor Green
        exit 0
    } else {
        Write-Host "=== Deinstallation mit Fehlern beendet ===" -ForegroundColor Red
        exit 1
    }
    
} catch {
    Write-Host ""
    Write-Host "✗ Kritischer Fehler: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "=== Deinstallation fehlgeschlagen ===" -ForegroundColor Red
    exit 1
}