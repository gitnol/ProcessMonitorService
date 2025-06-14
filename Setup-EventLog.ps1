#Requires -RunAsAdministrator

<#
.SYNOPSIS
    Erstellt die Event Log-Quelle für ProcessMonitorService

.DESCRIPTION
    Dieses Script erstellt die benötigte Event Log-Quelle "ProcessMonitorService" 
    im Application-Log und führt einen Test durch.

.EXAMPLE
    .\Setup-EventLog.ps1
    
.NOTES
    - Muss als Administrator ausgeführt werden
    - Erstellt Event Source: ProcessMonitorService
    - Ziel-Log: Application
#>

param(
    [string]$SourceName = "ProcessMonitorService",
    [string]$LogName = "Application"
)

# Farben für bessere Lesbarkeit
$ErrorColor = "Red"
$WarningColor = "Yellow" 
$SuccessColor = "Green"
$InfoColor = "Cyan"

function Write-ColorOutput {
    param(
        [string]$Message,
        [string]$Color = "White"
    )
    Write-Host $Message -ForegroundColor $Color
}

function Test-AdminRights {
    $currentUser = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($currentUser)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Test-EventSource {
    param([string]$Source)
    
    try {
        return [System.Diagnostics.EventLog]::SourceExists($Source)
    }
    catch {
        Write-ColorOutput "Fehler beim Prüfen der Event Source: $($_.Exception.Message)" $ErrorColor
        return $false
    }
}

function New-EventSource {
    param(
        [string]$Source,
        [string]$Log
    )
    
    try {
        [System.Diagnostics.EventLog]::CreateEventSource($Source, $Log)
        Write-ColorOutput "Event Log-Quelle '$Source' erfolgreich erstellt!" $SuccessColor
        return $true
    }
    catch {
        Write-ColorOutput "Fehler beim Erstellen der Event Source: $($_.Exception.Message)" $ErrorColor
        return $false
    }
}

function Write-TestEvent {
    param(
        [string]$Source,
        [string]$Log
    )
    
    try {
        $eventLog = New-Object System.Diagnostics.EventLog($Log)
        $eventLog.Source = $Source
        
        $message = "ProcessMonitorService Event Log-Quelle wurde erfolgreich eingerichtet. Setup durchgeführt am $(Get-Date -Format 'dd.MM.yyyy HH:mm:ss')"
        $eventLog.WriteEntry($message, [System.Diagnostics.EventLogEntryType]::Information, 1000)
        
        Write-ColorOutput "Test-Eintrag im Event Log erstellt (Event ID: 1000)" $SuccessColor
        return $true
    }
    catch {
        Write-ColorOutput "Fehler beim Schreiben des Test-Eintrags: $($_.Exception.Message)" $ErrorColor
        return $false
    }
    finally {
        if ($eventLog) {
            $eventLog.Dispose()
        }
    }
}

function Show-EventLogInfo {
    param([string]$Source)
    
    Write-ColorOutput "`n=== Event Log Informationen ===" $InfoColor
    Write-ColorOutput "Event Source: $Source" $InfoColor
    Write-ColorOutput "Ziel-Log: Application" $InfoColor
    Write-ColorOutput "`nSo öffnen Sie den Event Viewer:" $InfoColor
    Write-ColorOutput "1. Windows + R drücken" $InfoColor
    Write-ColorOutput "2. 'eventvwr.msc' eingeben und Enter drücken" $InfoColor
    Write-ColorOutput "3. Navigieren zu: Windows-Protokolle > Anwendung" $InfoColor
    Write-ColorOutput "4. Nach Quelle '$Source' filtern" $InfoColor
}

# Hauptprogramm
Clear-Host
Write-ColorOutput "=============================================" $InfoColor
Write-ColorOutput "ProcessMonitorService Event Log Setup" $InfoColor
Write-ColorOutput "=============================================" $InfoColor
Write-ColorOutput ""

# Administrator-Rechte prüfen
if (-not (Test-AdminRights)) {
    Write-ColorOutput "FEHLER: Dieses Script muss als Administrator ausgeführt werden!" $ErrorColor
    Write-ColorOutput "" 
    Write-ColorOutput "So führen Sie das Script als Administrator aus:" $WarningColor
    Write-ColorOutput "1. PowerShell als Administrator öffnen" $WarningColor
    Write-ColorOutput "2. Zu diesem Verzeichnis navigieren" $WarningColor  
    Write-ColorOutput "3. .\Setup-EventLog.ps1 ausführen" $WarningColor
    Write-ColorOutput ""
    Write-ColorOutput "Oder: Rechtsklick auf das Script > 'Mit PowerShell als Administrator ausführen'" $WarningColor
    Write-ColorOutput ""
    Read-Host "Drücken Sie Enter zum Beenden"
    exit 1
}

Write-ColorOutput "✓ Administrator-Rechte bestätigt" $SuccessColor
Write-ColorOutput ""

# Event Source prüfen
Write-ColorOutput "Prüfe Event Log-Quelle '$SourceName'..." $InfoColor

if (Test-EventSource -Source $SourceName) {
    Write-ColorOutput "Event Log-Quelle '$SourceName' existiert bereits." $WarningColor
    
    $choice = Read-Host "Möchten Sie trotzdem einen Test-Eintrag erstellen? (j/N)"
    if ($choice -match '^[jJyY]') {
        Write-ColorOutput "`nErstelle Test-Eintrag..." $InfoColor
        if (Write-TestEvent -Source $SourceName -Log $LogName) {
            Show-EventLogInfo -Source $SourceName
        }
    }
} else {
    Write-ColorOutput "Event Log-Quelle '$SourceName' existiert nicht. Wird erstellt..." $InfoColor
    
    if (New-EventSource -Source $SourceName -Log $LogName) {
        # Kurz warten, damit die Quelle verfügbar wird
        Write-ColorOutput "Warte 2 Sekunden..." $InfoColor
        Start-Sleep -Seconds 2
        
        # Test-Eintrag erstellen
        Write-ColorOutput "Erstelle Test-Eintrag..." $InfoColor
        if (Write-TestEvent -Source $SourceName -Log $LogName) {
            Show-EventLogInfo -Source $SourceName
        }
    }
}

Write-ColorOutput "`n=============================================" $SuccessColor
Write-ColorOutput "Setup abgeschlossen!" $SuccessColor
Write-ColorOutput "=============================================" $SuccessColor
Write-ColorOutput ""
Write-ColorOutput "Der ProcessMonitorService kann jetzt Event Log-Einträge schreiben." $InfoColor
Write-ColorOutput ""
Read-Host "Drücken Sie Enter zum Beenden"