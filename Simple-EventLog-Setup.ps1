#Requires -RunAsAdministrator

# Einfaches PowerShell Script zum Erstellen der Event Log-Quelle
# Muss als Administrator ausgeführt werden!

$SourceName = "ProcessMonitorService"
$LogName = "Application"

Write-Host "ProcessMonitorService Event Log Setup" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan

try {
    # Prüfen ob Event Source bereits existiert
    if ([System.Diagnostics.EventLog]::SourceExists($SourceName)) {
        Write-Host "Event Log-Quelle '$SourceName' existiert bereits." -ForegroundColor Yellow
    } else {
        Write-Host "Erstelle Event Log-Quelle '$SourceName'..." -ForegroundColor White
        [System.Diagnostics.EventLog]::CreateEventSource($SourceName, $LogName)
        Write-Host "Event Log-Quelle erfolgreich erstellt!" -ForegroundColor Green
        
        # Kurz warten
        Start-Sleep -Seconds 2
    }
    
    # Test-Eintrag erstellen
    Write-Host "Erstelle Test-Eintrag..." -ForegroundColor White
    $EventLog = New-Object System.Diagnostics.EventLog($LogName)
    $EventLog.Source = $SourceName
    $EventLog.WriteEntry("ProcessMonitorService Event Log-Quelle Setup erfolgreich - $(Get-Date)", "Information", 1000)
    $EventLog.Dispose()
    
    Write-Host "Test-Eintrag erstellt!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Setup erfolgreich abgeschlossen!" -ForegroundColor Green
    Write-Host "Öffnen Sie den Event Viewer (eventvwr.msc) um den Eintrag zu prüfen." -ForegroundColor White
    
} catch {
    Write-Host "FEHLER: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Stellen Sie sicher, dass Sie Administrator-Rechte haben." -ForegroundColor Yellow
}

Write-Host ""
Read-Host "Drücken Sie Enter zum Beenden"