# Shared process handling for the repository's Unity verification/build wrappers.
Set-StrictMode -Version Latest

function Resolve-UnityBatchPaths {
    param([string]$UnityPath, [string]$ProjectPath)

    if (-not (Test-Path -LiteralPath $UnityPath -PathType Leaf)) {
        throw "Unity executable not found: $UnityPath. Supply -UnityPath for the installed editor."
    }
    $resolvedEditor = (Resolve-Path -LiteralPath $UnityPath).Path
    $resolvedProject = (Resolve-Path -LiteralPath $ProjectPath).Path.TrimEnd('\', '/')
    foreach ($requiredFolder in @('Assets', 'Packages', 'ProjectSettings')) {
        if (-not (Test-Path -LiteralPath (Join-Path $resolvedProject $requiredFolder) -PathType Container)) {
            throw "Not a Unity project: missing $requiredFolder in $resolvedProject."
        }
    }
    $resultsDirectory = Join-Path $resolvedProject 'TestResults'
    [void](New-Item -ItemType Directory -Path $resultsDirectory -Force)
    return [pscustomobject]@{
        Editor = $resolvedEditor
        Project = $resolvedProject
        Results = $resultsDirectory
    }
}

function Assert-UnityProjectAvailable {
    param([string]$ProjectPath)

    # Unity's own project lock remains authoritative if process inspection is unavailable
    # or another editor starts after this check. Never bypass that lock.
    try {
        $editorProcesses = @(Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" -ErrorAction Stop)
    }
    catch {
        Write-Warning 'Could not inspect existing editor processes; Unity will enforce its project lock.'
        return
    }
    $expectedProject = $ProjectPath.Replace('/', '\').TrimEnd('\')
    foreach ($editorProcess in $editorProcesses) {
        if ([string]::IsNullOrWhiteSpace($editorProcess.CommandLine)) { continue }
        $projectArgument = [regex]::Match($editorProcess.CommandLine, '(?i)(?:^|\s)-projectPath\s+(?:"([^"]+)"|(\S+))')
        if (-not $projectArgument.Success) { continue }
        $openProject = $projectArgument.Groups[1].Value
        if ([string]::IsNullOrEmpty($openProject)) { $openProject = $projectArgument.Groups[2].Value }
        $openProject = $openProject.Replace('/', '\').TrimEnd('\')
        if ([string]::Equals($openProject, $expectedProject, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Unity process $($editorProcess.ProcessId) already has this project open. Close that editor before running batch verification/builds."
        }
    }
}

function Remove-ForeignEditorTypeDb {
    param([string]$ProjectPath, [string]$ExecutionProjectPath)

    # A build run from a scratch project that shares this Library rewrites the editor type database
    # with the scratch project's assembly paths. The project's own editor then cannot map scripts to
    # classes ("The referenced script (Unknown) is missing"). The file is a build-only cache, so
    # removing it returns the editor to its own compiled assemblies.
    if ([string]::Equals($ProjectPath.TrimEnd('\', '/'), $ExecutionProjectPath.TrimEnd('\', '/'), [StringComparison]::OrdinalIgnoreCase)) { return }
    $typeDb = Join-Path $ProjectPath 'Library/BuildPlayerData/Editor/TypeDb-All.json'
    if (-not (Test-Path -LiteralPath $typeDb -PathType Leaf)) { return }
    if (Select-String -LiteralPath $typeDb -SimpleMatch (Split-Path -Leaf $ExecutionProjectPath) -Quiet) {
        Remove-Item -LiteralPath $typeDb -Force
        Write-Host "Removed the editor type database written by $ExecutionProjectPath."
    }
}

function ConvertTo-UnityProcessArgument {
    param([string]$Value)

    # ProcessStartInfo on Windows PowerShell takes one command line. Escape each
    # argument for Windows argv parsing, including trailing backslashes.
    if ($Value.Contains('"')) { throw 'Unity arguments must not contain embedded quotation marks.' }
    $quotedValue = [regex]::Replace($Value, '(\\+)$', '$1$1')
    return '"' + $quotedValue + '"'
}

function Invoke-UnityBatch {
    param(
        [string]$UnityPath,
        [string]$ProjectPath,
        [string]$LogPath,
        [string[]]$Arguments,
        [int]$TimeoutSeconds = 1200
    )

    Assert-UnityProjectAvailable -ProjectPath $ProjectPath
    if (Test-Path -LiteralPath $LogPath) { Remove-Item -LiteralPath $LogPath -Force }
    $editorArguments = @('-batchmode', '-projectPath', $ProjectPath, '-logFile', $LogPath) + $Arguments
    $argumentLine = ($editorArguments | ForEach-Object { ConvertTo-UnityProcessArgument $_ }) -join ' '
    Write-Host "Running Unity. Log: $LogPath"
    $unityProcess = Start-Process -FilePath $UnityPath -ArgumentList $argumentLine -WorkingDirectory $ProjectPath -WindowStyle Hidden -PassThru
    try {
        $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
        while (-not $unityProcess.WaitForExit(1000)) {
            if ([DateTime]::UtcNow -ge $deadline) {
                $unityProcess.Kill()
                [void]$unityProcess.WaitForExit(10000)
                throw "Unity timed out after $TimeoutSeconds seconds. Only the editor process launched by this script was stopped. Inspect $LogPath."
            }
        }
        $exitCode = $unityProcess.ExitCode
        if ($exitCode -ne 0) { throw "Unity exited with code $exitCode. Inspect $LogPath." }
        if (-not (Test-Path -LiteralPath $LogPath -PathType Leaf)) { throw "Unity produced no log at $LogPath." }
        $failureLines = @(Select-String -LiteralPath $LogPath -Pattern '(?i)\berror CS\d+\b|Scripts have compiler errors|Aborting batchmode due to failure|another Unity instance is running with this project' | Select-Object -First 8)
        if ($failureLines.Count -gt 0) {
            $failureExcerpt = ($failureLines | ForEach-Object { $_.Line }) -join [Environment]::NewLine
            throw "Unity reported a compilation or batch failure in ${LogPath}:`n$failureExcerpt"
        }
    }
    finally {
        $unityProcess.Dispose()
    }
}

function Assert-UnityTestResult {
    param([string]$ResultPath)

    if (-not (Test-Path -LiteralPath $ResultPath -PathType Leaf)) { throw "Test runner did not create $ResultPath." }
    [xml]$testDocument = Get-Content -LiteralPath $ResultPath -Raw
    $testRun = $testDocument.SelectSingleNode('/test-run')
    if ($null -eq $testRun) { throw "No NUnit test-run element in $ResultPath." }
    $testCases = @($testDocument.SelectNodes('//test-case'))
    $failedCases = @($testDocument.SelectNodes('//test-case[@result="Failed" or @result="Inconclusive"]'))
    $passedCases = @($testDocument.SelectNodes('//test-case[@result="Passed"]'))
    $runResult = $testRun.GetAttribute('result')
    if ($runResult -ne 'Passed' -or $testCases.Count -eq 0 -or $passedCases.Count -eq 0 -or $failedCases.Count -gt 0 -or $testRun.GetAttribute('failed') -notin @('', '0')) {
        throw "Tests did not pass: result=$runResult, passed=$($passedCases.Count), failed/inconclusive=$($failedCases.Count), total=$($testCases.Count). Inspect $ResultPath."
    }
    Write-Host "Tests passed: $($passedCases.Count) passed, $($testCases.Count) total. Results: $ResultPath"
}
