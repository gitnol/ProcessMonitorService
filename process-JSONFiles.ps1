# Pfad zu den JSON-Datei(en). Beachte den Asterisk
$jsonFilePath = "C:\programdata\ProcessMonitorService\logs\service-*.json"

if (Test-Path -Path $jsonFilePath) {
    Write-Host("Existiert: $jsonFilePath")
    $files = Get-ChildItem -Path $jsonFilePath
    Write-host("Zutreffende Dateien: {0}`n{1}" -f $files.count, ($files -join "`n"))
    # Lese die Datei und verarbeite jede Zeile
    $jsonObjects = Get-Content $jsonFilePath | ForEach-Object {
        # Konvertiere jede Zeile von JSON zu einem PSCustomObject
        $_ | ConvertFrom-Json
    }

    # Beispiel zur Weiterverarbeitung
    # $jsonObjects | ForEach-Object {
    #     # Hier kannst du auf die Eigenschaften zugreifen, z.B.:
    #     $timestamp = $_.'@t'
    #     $messageTemplate = $_.'@mt'
    #     $sourceContext = $_.SourceContext
    #     $properties = $_.$Properties

    #     # Beispiel: Ausgabe der Zeitstempel und Nachrichten
    #     Write-Output "Timestamp: $timestamp, Message: $messageTemplate, Source: $sourceContext"
    # }

    # Filtert auf relevante Einträge
    $jsonObjects | Where-Object {
        ($_.SourceContext -eq "ProcessmonitorWorker") -and 
        ($_.EventType -in ("Start", "Stop")) -and
        ($_.ProcessName -eq "chrome.exe")
    }
}
else {
    Write-Error("Existiert nicht: $jsonFilePath ")
}