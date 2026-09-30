[CmdletBinding()]
param(
    [ValidateSet('All', 'Services', 'Unity', 'Build', 'Manifest')]
    [string]$Stage = 'All',
    [string]$UnityPath = 'D:\Unity\Editors\6000.3.23f1\Editor\Unity.exe',
    [ValidateRange(60, 14400)]
    [int]$TimeoutSeconds = 3600
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$alphaProject = (Resolve-Path -LiteralPath (Split-Path -Parent $PSScriptRoot)).Path
$alphaResults = Join-Path $alphaProject 'TestResults'
[void](New-Item -ItemType Directory -Path $alphaResults -Force)
. (Join-Path $PSScriptRoot 'Unity-Batch.ps1')
$alphaShell = (Get-Process -Id $PID).Path
$alphaSteps = [System.Collections.Generic.List[object]]::new()
$alphaStarted = [DateTime]::UtcNow

function Invoke-AlphaStep {
    param([string]$Name, [string]$Executable, [string[]]$Arguments, [string]$WorkingDirectory)
    $outPath = Join-Path $alphaResults ("alpha-" + $Name + '.stdout.log')
    $errorPath = Join-Path $alphaResults ("alpha-" + $Name + '.stderr.log')
    $argumentLine = ($Arguments | ForEach-Object { ConvertTo-UnityProcessArgument $_ }) -join ' '
    $began = [DateTime]::UtcNow
    Write-Host "Alpha verification: $Name"
    $process = Start-Process -FilePath $Executable -ArgumentList $argumentLine -WorkingDirectory $WorkingDirectory -WindowStyle Hidden -RedirectStandardOutput $outPath -RedirectStandardError $errorPath -PassThru
    try {
        # Windows PowerShell may otherwise release a short-lived child's handle before ExitCode is read.
        $alphaProcessHandle = $process.Handle
        while (-not $process.WaitForExit(1000)) {
            if (([DateTime]::UtcNow - $began).TotalSeconds -ge $TimeoutSeconds) {
                $process.Kill()
                [void]$process.WaitForExit(10000)
                throw "Alpha step $Name exceeded $TimeoutSeconds seconds. Inspect its logs; an independently launched Unity child may still be finishing its own bounded run."
            }
        }
        $process.WaitForExit()
        $process.Refresh()
        $code = $process.ExitCode
        if ($null -eq $code) { throw "Alpha step $Name exited without an observable exit code. Inspect $outPath and $errorPath." }
        $alphaSteps.Add([ordered]@{ Name = $Name; Passed = ($code -eq 0); ExitCode = $code; Seconds = [Math]::Round(([DateTime]::UtcNow - $began).TotalSeconds, 3); Stdout = "TestResults/alpha-$Name.stdout.log"; Stderr = "TestResults/alpha-$Name.stderr.log" })
        if ($code -ne 0) { throw "Alpha step $Name failed with exit code $code. Inspect $outPath and $errorPath." }
    }
    finally { $process.Dispose() }
}

function Get-AlphaArtifact {
    param([string]$Path)
    $file = Get-Item -LiteralPath $Path
    $relative = $file.FullName.Substring($alphaProject.Length + 1).Replace('\', '/')
    return [ordered]@{ Path = $relative; Bytes = $file.Length; Sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash; LastWriteUtc = $file.LastWriteTimeUtc.ToString('o') }
}

function Write-AlphaManifest {
    param([string]$Result)
    $artifacts = [System.Collections.Generic.List[object]]::new()
    $playerDirectory = Join-Path $alphaProject 'Builds/Windows'
    if (Test-Path -LiteralPath $playerDirectory -PathType Container) {
        foreach ($file in Get-ChildItem -LiteralPath $playerDirectory -Recurse -File | Sort-Object FullName) { $artifacts.Add((Get-AlphaArtifact $file.FullName)) }
    }
    foreach ($relative in @('Server/AuthorityWorker/out/Emberfield.Authority.dll', 'Server/AuthorityWorker/out/Emberfield.Authority.runtimeconfig.json', 'TestResults/EditMode.xml', 'TestResults/PlayMode.xml', 'TestResults/build-summary-StandaloneWindows64.txt', 'TestResults/offline-matches.json', 'TestResults/movement-final.json')) {
        $path = Join-Path $alphaProject $relative
        if (Test-Path -LiteralPath $path -PathType Leaf) { $artifacts.Add((Get-AlphaArtifact $path)) }
    }
    # Explicit source allowlist: never enumerate Server/data, local accounts, environment files or reports.
    $sourceLines = [System.Collections.Generic.List[string]]::new()
    foreach ($relative in @('Assets/Game', 'Server/AuthorityWorker')) {
        $directory = Join-Path $alphaProject $relative
        foreach ($file in Get-ChildItem -LiteralPath $directory -Recurse -File | Where-Object { $_.Extension -in @('.cs', '.shader', '.asmdef', '.json') -and $_.FullName -notmatch '[\\/]out[\\/]' } | Sort-Object FullName) {
            $sourceLines.Add($file.FullName.Substring($alphaProject.Length + 1).Replace('\', '/') + ' ' + (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash)
        }
    }
    foreach ($file in Get-ChildItem -LiteralPath (Join-Path $alphaProject 'Server') -File | Where-Object { $_.Extension -in @('.mjs', '.json') } | Sort-Object FullName) {
        $sourceLines.Add($file.FullName.Substring($alphaProject.Length + 1).Replace('\', '/') + ' ' + (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash)
    }
    $hasher = [Security.Cryptography.SHA256]::Create()
    try { $sourceHash = [BitConverter]::ToString($hasher.ComputeHash([Text.Encoding]::UTF8.GetBytes(($sourceLines | Sort-Object) -join "`n"))).Replace('-', '') }
    finally { $hasher.Dispose() }
    $revision = (& git -C $alphaProject rev-parse HEAD 2>$null | Out-String).Trim()
    $dirty = -not [string]::IsNullOrWhiteSpace((& git -C $alphaProject status --porcelain 2>$null | Out-String))
    $manifest = [ordered]@{
        Schema = 1; StartedUtc = $alphaStarted.ToString('o'); FinishedUtc = [DateTime]::UtcNow.ToString('o'); RequestedStage = $Stage; Result = $Result
        Scope = 'Local verification only. All runs services tests, Unity verification, a Windows build, the two-client online smoke and the main-menu-to-skirmish product walkthrough. Other stages are partial; Manifest only inventories existing artifacts. Physical-device and hosted-service validation is separate.'
        Revision = $revision; WorkingTreeDirty = $dirty; CurrentSourceSha256 = $sourceHash; SourceFileCount = $sourceLines.Count
        StepsExecutedThisRun = @($alphaSteps.ToArray()); Artifacts = @($artifacts.ToArray())
        Privacy = 'Only source and build artifact hashes are included. No accounts, credentials, environment values, machine identifiers or local diagnostic records are included.'
        Publication = 'No installation, deployment, service startup, remote write or upload is performed.'
    }
    $path = Join-Path $alphaResults 'alpha-release-manifest.json'
    [IO.File]::WriteAllText($path, ($manifest | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
    Write-Host "Alpha manifest: $path"
}

try {
    if ($Stage -in @('All', 'Services')) {
        Invoke-AlphaStep -Name 'authority-build' -Executable $alphaShell -WorkingDirectory $alphaProject -Arguments @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $PSScriptRoot 'Build-Authority.ps1'), '-UnityPath', $UnityPath)
        $node = (Get-Command node -ErrorAction Stop).Source
        $nodeTests = @(Get-ChildItem -LiteralPath (Join-Path $alphaProject 'Server/tests') -Filter '*.test.mjs' -File | Sort-Object Name | ForEach-Object { $_.FullName })
        if ($nodeTests.Count -eq 0) { throw 'No server tests were found.' }
        Invoke-AlphaStep -Name 'server-tests' -Executable $node -WorkingDirectory (Join-Path $alphaProject 'Server') -Arguments (@('--experimental-sqlite', '--test') + $nodeTests)
    }
    if ($Stage -in @('All', 'Unity')) {
        Invoke-AlphaStep -Name 'unity-tests' -Executable $alphaShell -WorkingDirectory $alphaProject -Arguments @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $PSScriptRoot 'Verify-Unity.ps1'), '-UnityPath', $UnityPath, '-Stage', 'All', '-TimeoutSeconds', [Math]::Min($TimeoutSeconds, 7200).ToString())
    }
    if ($Stage -in @('All', 'Build')) {
        Invoke-AlphaStep -Name 'windows-build' -Executable $alphaShell -WorkingDirectory $alphaProject -Arguments @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $PSScriptRoot 'Build-Unity.ps1'), '-UnityPath', $UnityPath, '-Target', 'Windows', '-TimeoutSeconds', [Math]::Min($TimeoutSeconds, 7200).ToString())
    }
    if ($Stage -eq 'All') {
        Invoke-AlphaStep -Name 'online-player-smoke' -Executable $alphaShell -WorkingDirectory $alphaProject -Arguments @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $PSScriptRoot 'Smoke-Online.ps1'), '-Width', '1280', '-Height', '720', '-TimeoutSeconds', '120')
        Invoke-AlphaStep -Name 'product-player-smoke' -Executable $alphaShell -WorkingDirectory $alphaProject -Arguments @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $PSScriptRoot 'Smoke-AlphaProduct.ps1'), '-Width', '1280', '-Height', '720', '-TimeoutSeconds', '180')
    }
    Write-AlphaManifest -Result $(if ($Stage -eq 'All') { 'Passed' } elseif ($Stage -eq 'Manifest') { 'InventoryOnly' } else { 'PartialRunPassed' })
    exit 0
}
catch {
    Write-AlphaManifest -Result 'Failed'
    Write-Error -Message $_.Exception.Message -ErrorAction Continue
    exit 1
}
