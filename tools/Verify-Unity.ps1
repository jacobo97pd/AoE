[CmdletBinding()]
param(
    [ValidateSet('All', 'Compile', 'EditMode', 'PlayMode', 'Combat', 'Factions', 'Offline', 'Movement')]
    [string]$Stage = 'All',
    [string]$UnityPath = 'D:\Unity\Editors\6000.3.23f1\Editor\Unity.exe',
    [string]$ProjectPath = '',
    [ValidateRange(30, 7200)]
    [int]$TimeoutSeconds = 1200
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($ProjectPath)) { $ProjectPath = Split-Path -Parent $PSScriptRoot }
. (Join-Path $PSScriptRoot 'Unity-Batch.ps1')

try {
    $batchPaths = Resolve-UnityBatchPaths -UnityPath $UnityPath -ProjectPath $ProjectPath
    if ($Stage -in @('All', 'Compile')) {
        Invoke-UnityBatch -UnityPath $batchPaths.Editor -ProjectPath $batchPaths.Project -LogPath (Join-Path $batchPaths.Results 'compile.log') -TimeoutSeconds $TimeoutSeconds -Arguments @('-nographics', '-quit', '-executeMethod', 'Emberfield.Editor.ProjectTools.Verify')
        Write-Host 'Unity compile/verification completed.'
    }
    foreach ($testStage in @('EditMode', 'PlayMode')) {
        if ($Stage -notin @('All', $testStage)) { continue }
        $resultPath = Join-Path $batchPaths.Results "$testStage.xml"
        if (Test-Path -LiteralPath $resultPath) { Remove-Item -LiteralPath $resultPath -Force }
        # Do not add -quit: Unity Test Framework owns the test process lifecycle.
        # PlayMode retains graphics for meaningful scene/presentation integration.
        $testArguments = @('-runTests', '-testPlatform', $testStage, '-testResults', $resultPath)
        if ($testStage -eq 'EditMode') { $testArguments += '-nographics' }
        Invoke-UnityBatch -UnityPath $batchPaths.Editor -ProjectPath $batchPaths.Project -LogPath (Join-Path $batchPaths.Results "$testStage.log") -TimeoutSeconds $TimeoutSeconds -Arguments $testArguments
        Assert-UnityTestResult -ResultPath $resultPath
    }
    if ($Stage -in @('All', 'Combat')) {
        $balancePath = Join-Path $batchPaths.Results 'combat-balance.md'
        if (Test-Path -LiteralPath $balancePath) { Remove-Item -LiteralPath $balancePath -Force }
        Invoke-UnityBatch -UnityPath $batchPaths.Editor -ProjectPath $batchPaths.Project -LogPath (Join-Path $batchPaths.Results 'combat-balance.log') -TimeoutSeconds $TimeoutSeconds -Arguments @('-nographics', '-quit', '-executeMethod', 'Emberfield.Editor.CombatVerification.Run')
        if (-not (Test-Path -LiteralPath $balancePath) -or (Get-Content -LiteralPath $balancePath -Raw) -notmatch 'Passed: True') { throw 'Shipped combat balance verification did not pass.' }
        Write-Host 'Shipped combat balance passed: 12 scenarios.'
    }
    if ($Stage -in @('All', 'Factions')) {
        $factionPath = Join-Path $batchPaths.Results 'faction-counters.md'
        if (Test-Path -LiteralPath $factionPath) { Remove-Item -LiteralPath $factionPath -Force }
        Invoke-UnityBatch -UnityPath $batchPaths.Editor -ProjectPath $batchPaths.Project -LogPath (Join-Path $batchPaths.Results 'faction-counters.log') -TimeoutSeconds $TimeoutSeconds -Arguments @('-nographics', '-quit', '-executeMethod', 'Emberfield.Editor.FactionVerification.Run')
        if (-not (Test-Path -LiteralPath $factionPath) -or (Get-Content -LiteralPath $factionPath -Raw) -notmatch 'Passed: True') { throw 'Shipped faction counter verification did not pass.' }
        Write-Host 'Shipped faction counter checks passed: 8 scenarios.'
    }
    if ($Stage -in @('All', 'Offline')) {
        $offlinePath = Join-Path $batchPaths.Results 'offline-matches.json'
        if (Test-Path -LiteralPath $offlinePath) { Remove-Item -LiteralPath $offlinePath -Force }
        Invoke-UnityBatch -UnityPath $batchPaths.Editor -ProjectPath $batchPaths.Project -LogPath (Join-Path $batchPaths.Results 'offline-matches.log') -TimeoutSeconds $TimeoutSeconds -Arguments @('-nographics', '-quit', '-executeMethod', 'Emberfield.Editor.OfflineMatchVerification.Run')
        if (-not (Test-Path -LiteralPath $offlinePath)) { throw 'Offline match verification produced no report.' }
        if (-not (Select-String -LiteralPath (Join-Path $batchPaths.Results 'offline-matches.log') -SimpleMatch 'EMBERFIELD_OFFLINE_MATCHES_OK')) { throw 'Natural offline matches did not all complete.' }
        Write-Host 'Offline matches completed: four faction/mode cases.'
    }
    if ($Stage -in @('All', 'Movement')) {
        $movementPath = Join-Path $batchPaths.Results 'movement-final.json'
        if (Test-Path -LiteralPath $movementPath) { Remove-Item -LiteralPath $movementPath -Force }
        Invoke-UnityBatch -UnityPath $batchPaths.Editor -ProjectPath $batchPaths.Project -LogPath (Join-Path $batchPaths.Results 'movement-final.log') -TimeoutSeconds $TimeoutSeconds -Arguments @('-nographics', '-quit', '-executeMethod', 'Emberfield.Editor.MovementVerification.Run', '-movementMode', 'Final', '-movementLabel', 'final')
        if (-not (Test-Path -LiteralPath $movementPath)) { throw 'Movement verification produced no report.' }
        $movement = Get-Content -LiteralPath $movementPath -Raw | ConvertFrom-Json
        if (-not $movement.RunComplete -or -not $movement.RequiredGatesPassed -or @($movement.Cases).Count -ne 30) { throw 'Movement verification did not pass its full matrix.' }
        Write-Host 'Movement measured: 30 cases; required acceptance passed.'
    }
    exit 0
}
catch {
    Write-Error -Message $_.Exception.Message -ErrorAction Continue
    exit 1
}
