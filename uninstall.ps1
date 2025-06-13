param (
    [string]$ServiceName = "ProcessMonitorService"
)

if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
    Stop-Service -Name $ServiceName -Force
    sc.exe delete $ServiceName | Out-Null
    Write-Host "Dienst $ServiceName wurde gestoppt und gelöscht."
} else {
    Write-Host "Dienst $ServiceName ist nicht installiert."
}
