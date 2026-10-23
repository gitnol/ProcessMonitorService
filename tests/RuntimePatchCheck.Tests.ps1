#Requires -Version 5.1
# Pester-Tests fuer die Patchstand-Pruefung der gebuendelten .NET-Runtime beim self-contained Publish
# (I8, docs/security/THREAT_MODEL.md Bedrohung 7). Die Tests rufen das echte `dotnet publish` auf und
# veroeffentlichen in ein temporaeres Verzeichnis (TestDrive), nie in den ueblichen Publish-Ordner.
# Voraussetzung: .NET SDK im PATH; der Runtime-Pack wird beim ersten Lauf von nuget.org geladen.

BeforeAll {
    $script:ProjectFile = (Resolve-Path (Join-Path $PSScriptRoot '..\ProcessMonitorService.csproj')).Path

    function Invoke-TestPublish {
        param(
            [Parameter(Mandatory)] [string] $OutDir,
            [string[]] $ExtraArgs = @()
        )
        $publishArgs = @('publish', $script:ProjectFile, '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true',
            '-o', $OutDir, '-nologo') + $ExtraArgs
        $output = & dotnet @publishArgs 2>&1 | Out-String
        [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = $output }
    }

    function Get-BundledRuntimePackVersion {
        param([Parameter(Mandatory)] [string] $DepsJsonPath)
        $match = Select-String -Path $DepsJsonPath -Pattern 'runtimepack\.Microsoft\.NETCore\.App\.Runtime\.win-x64/(\d+\.\d+\.\d+)' |
            Select-Object -First 1
        if (-not $match) { return $null }
        [version]$match.Matches[0].Groups[1].Value
    }
}

Describe 'Patchstand-Pruefung der gebuendelten Runtime beim self-contained Publish' -Tag 'Build' {

    It 'bricht den Publish ab, wenn die Mindestversion ueber dem gebuendelten Runtime-Pack liegt' {
        $outDir = Join-Path $TestDrive 'zu-hoch'
        $result = Invoke-TestPublish -OutDir $outDir -ExtraArgs @('-p:MinimumBundledRuntimeVersion=99.0.0')

        $result.ExitCode | Should -Not -Be 0 -Because "der Publish muss scheitern. Ausgabe:`n$($result.Output)"
        $result.Output | Should -Match 'PMS0001'
        Join-Path $outDir 'ProcessMonitorService.exe' | Should -Not -Exist
    }

    It 'laesst den Publish mit der konfigurierten Mindestversion durch; deps.json buendelt mindestens diese Runtime' {
        $configured = (& dotnet msbuild $script:ProjectFile -getProperty:MinimumBundledRuntimeVersion -nologo | Out-String).Trim()
        $configured | Should -Match '^\d+\.\d+\.\d+$' -Because 'die Mindestversion muss im Projekt konfiguriert sein'
        [version]$configured | Should -BeGreaterOrEqual ([version]'9.0.21') -Because 'Sweep 2026-10-23: Runtime-Pack <= 9.0.20 hat High-Advisories'

        $outDir = Join-Path $TestDrive 'konfiguriert'
        $result = Invoke-TestPublish -OutDir $outDir
        $result.ExitCode | Should -Be 0 -Because "der Publish muss gelingen. Ausgabe:`n$($result.Output)"

        $bundled = Get-BundledRuntimePackVersion -DepsJsonPath (Join-Path $outDir 'ProcessMonitorService.deps.json')
        $bundled | Should -Not -BeNullOrEmpty
        $bundled | Should -BeGreaterOrEqual ([version]$configured)
    }
}
