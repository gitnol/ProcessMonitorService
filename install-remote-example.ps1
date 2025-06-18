#Requires -RunAsAdministrator
#Requires -Version 5.0

<#
.SYNOPSIS
    Installs ProcessMonitorService on a remote machine
.DESCRIPTION
    This script uninstalls any existing service, copies new files, and installs the service on a remote machine.
.PARAMETER TargetHost
    The hostname or IP address of the target machine
.PARAMETER LocalSourcePath
    Path to the local files to be deployed
.PARAMETER RemoteServicePath
    Path on the remote machine where files will be copied
.PARAMETER Credential
    Credentials for remote machine access
.EXAMPLE
    .\Deploy-ProcessMonitorService.ps1 -TargetHost "MYHOSTNAME" -LocalSourcePath "D:\GIT\ProcessMonitorService\bin\Release\*"
.EXAMPLE
    # If you want to run this script interactively and provide credentials at runtime:
    $mycredentials = (Get-Credential);
    .\Deploy-ProcessMonitorService.ps1 -TargetHost "MYHOSTNAME" -Credential $mycredentials
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$TargetHost = "MYHOSTNAME",
    
    [Parameter(Mandatory = $false)]
    [string]$LocalSourcePath = "D:\GIT\gitnol\ProcessMonitorService\ProcessMonitorService\bin\Release\net9.0-windows\win-x64\publish\*",
    
    [Parameter(Mandatory = $false)]
    [string]$RemoteServicePath = "C:\Program Files\ProcessMonitorService",
    
    [Parameter(Mandatory = $false)]
    [PSCredential]$Credential
)

# Konstanten
$MAX_RETRIES = 3

# Funktionen
function Write-LogMessage {
    param(
        [string]$Message,
        [ValidateSet("Info", "Warning", "Error", "Success")]
        [string]$Level = "Info"
    )
    
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $color = switch ($Level) {
        "Info" { "White" }
        "Warning" { "Yellow" }
        "Error" { "Red" }
        "Success" { "Green" }
    }
    
    Write-Host "[$timestamp] [$Level] $Message" -ForegroundColor $color
}

function Test-RemoteConnection {
    param([string]$ComputerName, [PSCredential]$Credential)
    
    try {
        Write-LogMessage "Teste Invoke-Command mit Credentials..."
        $testScript = { $env:COMPUTERNAME }
        $result = Invoke-Command -ComputerName $ComputerName -ScriptBlock $testScript -Credential $Credential -ErrorAction Stop
        Write-LogMessage "Remote-Ausführung mit Credentials erfolgreich: $result" -Level "Success"
        return $true
    }
    catch {
        Write-LogMessage "Verbindung zu $ComputerName fehlgeschlagen: $($_.Exception.Message)" -Level "Error"
        return $false
    }
}

function Invoke-RemoteScriptWithRetry {
    param(
        [string]$ComputerName,
        [scriptblock]$ScriptBlock,
        [PSCredential]$Credential,
        [string]$Description,
        [int]$MaxRetries = $MAX_RETRIES
    )
    
    for ($i = 1; $i -le $MaxRetries; $i++) {
        try {
            Write-LogMessage "$Description (Versuch $i/$MaxRetries)"
            $result = Invoke-Command -ComputerName $ComputerName -ScriptBlock $ScriptBlock -Credential $Credential -ErrorAction Stop
            Write-LogMessage "$Description erfolgreich abgeschlossen" -Level "Success"
            return $result
        }
        catch {
            Write-LogMessage "$Description fehlgeschlagen (Versuch $i/$MaxRetries): $($_.Exception.Message)" -Level "Warning"
            if ($i -eq $MaxRetries) {
                throw "Alle Versuche für '$Description' fehlgeschlagen: $($_.Exception.Message)"
            }
            Start-Sleep -Seconds 5
        }
    }
}

function Copy-FilesWithValidation {
    param(
        [string]$Source,
        [string]$Destination,
        [string]$TargetHost
    )
    
    # Prüfe ob Quelldateien existieren
    if (-not (Test-Path $Source)) {
        throw "Quelldateien nicht gefunden: $Source"
    }
    
    # Erstelle Zielverzeichnis falls nötig
    $remoteDestination = "\\$TargetHost\C$\Program Files\ProcessMonitorService"
    if (-not (Test-Path $remoteDestination)) {
        Write-LogMessage "Erstelle Zielverzeichnis: $remoteDestination"
        New-Item -Path $remoteDestination -ItemType Directory -Force | Out-Null
    }
    
    # Kopiere Dateien mit Robocopy für bessere Performance
    $robocopyArgs = @(
        (Split-Path $Source -Parent),
        $remoteDestination,
        (Split-Path $Source -Leaf),
        "/E", "/R:3", "/W:5", "/MT:8", "/NP"
    )
    
    Write-LogMessage "Kopiere Dateien mit Robocopy..."
    & robocopy @robocopyArgs
    
    # Robocopy Exit Codes: 0-7 sind erfolgreich, >7 sind Fehler
    if ($LASTEXITCODE -gt 7) {
        throw "Robocopy fehlgeschlagen mit Exit Code: $LASTEXITCODE"
    }
    
    Write-LogMessage "Dateien erfolgreich kopiert" -Level "Success"
}

# Hauptskript
try {
    Write-LogMessage "=== ProcessMonitorService Deployment gestartet ===" -Level "Info"
    Write-LogMessage "Zielhost: $TargetHost"
    Write-LogMessage "Quelldateien: $LocalSourcePath"
    
    # Credentials abrufen falls nicht übergeben
    if (-not $Credential) {
        $Credential = Get-Credential -Message "Bitte geben Sie die Anmeldeinformationen für $TargetHost ein:"
        if (-not $Credential) {
            throw "Keine Anmeldeinformationen bereitgestellt"
        }
    }
    
    # Verbindung testen
    Write-LogMessage "Teste Verbindung zu $TargetHost..."
    if (-not (Test-RemoteConnection -ComputerName $TargetHost -Credential $Credential)) {
        throw "Verbindung zu $TargetHost nicht möglich"
    }
    Write-LogMessage "Verbindung erfolgreich" -Level "Success"
    
    # Service deinstallieren
    $uninstallScript = {
        $scriptPath = "C:\Program Files\ProcessMonitorService\uninstall.ps1"
        if (Test-Path $scriptPath) {
            Write-Output "Führe Deinstallation aus..."
            & $scriptPath
            Write-Output "Deinstallation abgeschlossen"
        }
        else {
            Write-Output "Uninstall-Script nicht gefunden - überspringe Deinstallation"
        }
    }
    
    Invoke-RemoteScriptWithRetry -ComputerName $TargetHost -ScriptBlock $uninstallScript -Credential $Credential -Description "Service-Deinstallation"
    
    # Dateien kopieren
    Write-LogMessage "Kopiere Dateien..."
    Copy-FilesWithValidation -Source $LocalSourcePath -Destination $RemoteServicePath -TargetHost $TargetHost
    
    # Service installieren
    $installScript = {
        $scriptPath = "C:\Program Files\ProcessMonitorService\install.ps1"
        if (Test-Path $scriptPath) {
            Write-Output "Führe Installation aus..."
            & $scriptPath
            Write-Output "Installation abgeschlossen"
        }
        else {
            throw "Install-Script nicht gefunden: $scriptPath"
        }
    }
    
    Invoke-RemoteScriptWithRetry -ComputerName $TargetHost -ScriptBlock $installScript -Credential $Credential -Description "Service-Installation"
    
    Write-LogMessage "=== Deployment erfolgreich abgeschlossen ===" -Level "Success"
}
catch {
    Write-LogMessage "=== Deployment fehlgeschlagen ===" -Level "Error"
    Write-LogMessage $_.Exception.Message -Level "Error"
    exit 1
}
finally {
    Write-LogMessage "=== Deployment-Prozess beendet ==="
}