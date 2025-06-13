param (
    [string]$ExePath = "$(Split-Path -Parent $MyInvocation.MyCommand.Definition)\bin\Release\net9.0-windows\win-x64\publish\ProcessMonitorService.exe",
    [string]$ServiceName = "ProcessMonitorService"
)

# Dienst anlegen
sc.exe create $ServiceName binPath= "`"$ExePath`"" start= auto

# Dienst starten
Start-Service -Name $ServiceName

Write-Host "Dienst $ServiceName wurde installiert und gestartet."
