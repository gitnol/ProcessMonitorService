New-Item -Path "C:\ProgramData\ProcessMonitorService" -ItemType Directory -Force

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