param (
    [string]$ExePath = "$(Split-Path -Parent $MyInvocation.MyCommand.Definition)\ProcessMonitorService.exe",
    [string]$ServiceName = "ProcessMonitorService"
)

if (-not ($PSVersionTable.PSVersion.Major -ge 7)) {
    Write-Error "Dieses Skript erfordert PowerShell Version 7 oder höher."
    exit 1
}


# Funktion zur Überprüfung der Administrator-Rechte
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

# Prüfen ob Service-Datei existiert
Write-Host "Überprüfe Service-Datei: $ExePath" -ForegroundColor Yellow

if (-not (Test-Path $ExePath)) {
    Write-Host "FEHLER: Service-Executable nicht gefunden: $ExePath" -ForegroundColor Red
    Write-Host "Bitte überprüfen Sie den Pfad zur ausführbaren Datei." -ForegroundColor Yellow
    Read-Host "Drücke Enter zum Beenden"
    exit 1
}

Write-Host "✓ Service-Datei gefunden" -ForegroundColor Green
Write-Host ""

try {
    # Prüfen ob Service bereits existiert
    Write-Host "Überprüfe existierenden Service '$ServiceName'..." -ForegroundColor Yellow
    
    $existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    
    if ($existingService) {
        Write-Host "Service '$ServiceName' existiert bereits." -ForegroundColor Yellow
        
        # Service stoppen falls er läuft
        if ($existingService.Status -eq 'Running') {
            Write-Host "Stoppe laufenden Service..." -ForegroundColor Yellow
            Stop-Service -Name $ServiceName -Force -ErrorAction Stop
            Write-Host "✓ Service gestoppt" -ForegroundColor Green
        }
        
        # Service löschen
        Write-Host "Lösche existierenden Service..." -ForegroundColor Yellow
        sc.exe delete $ServiceName
        if ($LASTEXITCODE -ne 0) {
            throw "Fehler beim Löschen des existierenden Services (Exit Code: $LASTEXITCODE)"
        }
        Write-Host "✓ Existierender Service gelöscht" -ForegroundColor Green
        
        # Kurz warten damit Windows den Service vollständig entfernt
        Start-Sleep -Seconds 2
    }
    
    # Neuen Service erstellen
    Write-Host "Erstelle neuen Service '$ServiceName'..." -ForegroundColor Yellow
    
    $result = sc.exe create $ServiceName binPath= "`"$ExePath`"" start= auto DisplayName= "Process Monitor Service"
    if ($LASTEXITCODE -ne 0) {
        throw "Fehler beim Erstellen des Services (Exit Code: $LASTEXITCODE): $result"
    }
    Write-Host "✓ Service erstellt" -ForegroundColor Green
    
    # Service-Beschreibung setzen
    Write-Host "Setze Service-Beschreibung..." -ForegroundColor Yellow
    sc.exe description $ServiceName "Überwacht Prozess-Start und -Stop Ereignisse für konfigurierte Anwendungen"
    
    # Service starten
    Write-Host "Starte Service '$ServiceName'..." -ForegroundColor Yellow
    
    Start-Service -Name $ServiceName -ErrorAction Stop
    
    # Service-Status überprüfen
    $service = Get-Service -Name $ServiceName
    if ($service.Status -eq 'Running') {
        Write-Host "✓ Service erfolgreich gestartet" -ForegroundColor Green
    }
    else {
        Write-Host "⚠ Service erstellt, aber Status: $($service.Status)" -ForegroundColor Yellow
    }
    
    Write-Host ""
    Write-Host "=== INSTALLATION ERFOLGREICH ===" -ForegroundColor Green
    Write-Host "Service Name: $ServiceName" -ForegroundColor White
    Write-Host "Service Status: $($service.Status)" -ForegroundColor White
    Write-Host "Executable: $ExePath" -ForegroundColor White
    Write-Host ""
    Write-Host "Service-Management:" -ForegroundColor Cyan
    Write-Host "- Status prüfen: Get-Service -Name $ServiceName" -ForegroundColor White
    Write-Host "- Service stoppen: Stop-Service -Name $ServiceName" -ForegroundColor White
    Write-Host "- Service starten: Start-Service -Name $ServiceName" -ForegroundColor White
    Write-Host "- Service deinstallieren: sc.exe delete $ServiceName" -ForegroundColor White
    Write-Host ""
    
}
catch {
    Write-Host ""
    Write-Host "FEHLER bei der Service-Installation:" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host ""
    Write-Host "Mögliche Lösungen:" -ForegroundColor Yellow
    Write-Host "1. Überprüfen Sie, ob die Datei '$ExePath' existiert" -ForegroundColor White
    Write-Host "2. Stellen Sie sicher, dass keine andere Instanz des Services läuft" -ForegroundColor White
    Write-Host "3. Überprüfen Sie die Windows Event Logs für weitere Details" -ForegroundColor White
    Write-Host ""
    
    Read-Host "Drücke Enter zum Beenden"
    exit 1
}

Write-Host "Installation abgeschlossen." -ForegroundColor Green

./Set-SecureACLs.ps1
