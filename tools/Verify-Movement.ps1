[CmdletBinding()]
param(
    [ValidateSet('Baseline', 'Final')]
    [string]$Mode = 'Baseline',
    [ValidatePattern('^[A-Za-z0-9_-]*$')]
    [string]$Label = '',
    [string]$Counts = '50,100,200,300,500',
    [string]$Scenarios = 'OpenField,WideCorridor,NarrowChoke,CrossingGroups,DynamicObstacle,Unreachable',
    [ValidateRange(1, 600)]
    [int]$Seconds = 120,
    [ValidateRange(1, 10)]
    [int]$Repetitions = 1,
    [string]$UnityPath = 'D:\Unity\Editors\6000.3.23f1\Editor\Unity.exe',
    [string]$ProjectPath = '',
    [ValidateRange(30, 7200)]
    [int]$TimeoutSeconds = 1800
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($ProjectPath)) { $ProjectPath = Split-Path -Parent $PSScriptRoot }
if ([string]::IsNullOrWhiteSpace($Label)) { $Label = $Mode.ToLowerInvariant() }
. (Join-Path $PSScriptRoot 'Unity-Batch.ps1')

try {
    $batchPaths = Resolve-UnityBatchPaths -UnityPath $UnityPath -ProjectPath $ProjectPath
    $resultPath = Join-Path $batchPaths.Results "movement-$Label.json"
    if (Test-Path -LiteralPath $resultPath) { Remove-Item -LiteralPath $resultPath -Force }
    $arguments = @('-nographics', '-quit', '-executeMethod', 'Emberfield.Editor.MovementVerification.Run',
        '-movementMode', $Mode, '-movementLabel', $Label, '-movementCounts', $Counts,
        '-movementScenarios', $Scenarios, '-movementSeconds', $Seconds.ToString(), '-movementRepetitions', $Repetitions.ToString())
    Invoke-UnityBatch -UnityPath $batchPaths.Editor -ProjectPath $batchPaths.Project -LogPath (Join-Path $batchPaths.Results "movement-$Label.log") -Arguments $arguments -TimeoutSeconds $TimeoutSeconds
    if (-not (Test-Path -LiteralPath $resultPath -PathType Leaf)) { throw "Movement runner produced no JSON: $resultPath." }
    $report = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
    if (-not $report.RunComplete -or @($report.Cases).Count -eq 0) { throw 'Movement report is incomplete.' }
    if ($Mode -eq 'Final' -and -not $report.RequiredGatesPassed) { throw 'Final movement acceptance failed; inspect the JSON and Markdown report.' }
    Write-Host "Movement $Mode captured: $(@($report.Cases).Count) cases. Required acceptance: $($report.RequiredGatesPassed). JSON: $resultPath"
    exit 0
}
catch {
    Write-Error -Message $_.Exception.Message -ErrorAction Continue
    exit 1
}
