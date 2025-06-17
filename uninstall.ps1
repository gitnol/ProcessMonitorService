param (
    [string]$ServiceName = "ProcessMonitorService"
)

if (-not ($PSVersionTable.PSVersion.Major -ge 7)) {
    Write-Error "Dieses Skript erfordert PowerShell Version 7 oder höher."
    exit 1
}

if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
    Stop-Service -Name $ServiceName -Force
    sc.exe delete $ServiceName | Out-Null
    Write-Host "Dienst $ServiceName wurde gestoppt und gelöscht."
} else {
    Write-Host "Dienst $ServiceName ist nicht installiert."
}
