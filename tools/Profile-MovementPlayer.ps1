[CmdletBinding()]
param(
    [string]$ProjectPath = '',
    [ValidateSet(50, 100, 200, 300, 500)][int[]]$Counts = @(50, 100, 200, 300, 500),
    [ValidateSet('OpenField', 'WideCorridor', 'NarrowChoke', 'CrossingGroups', 'DynamicObstacle', 'Unreachable')][string]$Scenario = 'OpenField',
    [int]$Width = 1280,
    [int]$Height = 720,
    [ValidateRange(10, 90)][int]$Seconds = 20,
    # Another player to measure (an earlier build kept for an A/B), and a label keeping its reports apart.
    [string]$PlayerPath = '',
    [ValidatePattern('^[A-Za-z0-9_-]*$')][string]$Label = '',
    # Passed through to the player, e.g. -emberfieldAllocationCapture,300 to record where allocations come from.
    [string[]]$ExtraArguments = @()
)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($ProjectPath)) { $ProjectPath = Split-Path -Parent $PSScriptRoot }
. (Join-Path $PSScriptRoot 'Unity-Batch.ps1')
try {
    $project = (Resolve-Path -LiteralPath $ProjectPath).Path
    if ([string]::IsNullOrWhiteSpace($PlayerPath)) { $PlayerPath = Join-Path $project 'Builds\Windows\Emberfield.exe' }
    if (-not (Test-Path -LiteralPath $PlayerPath)) { throw 'Build the Windows development player first.' }
    $player = (Resolve-Path -LiteralPath $PlayerPath).Path
    $suffix = if ($Label) { "-$Label" } else { '' }
    foreach ($count in $Counts) {
        $output = Join-Path $project "TestResults\MovementPlayer-${Scenario}-${count}-${Width}x${Height}${suffix}"
        [void](New-Item -ItemType Directory -Path $output -Force)
        foreach ($name in @('movement-player.json', 'stress.png', 'player.log', 'allocations.raw')) {
            $stale = Join-Path $output $name
            if (Test-Path -LiteralPath $stale) { Remove-Item -LiteralPath $stale -Force }
        }
        $arguments = @('-force-d3d11', '-screen-fullscreen', '0', '-screen-width', [string]$Width, '-screen-height', [string]$Height,
            '-logFile', (Join-Path $output 'player.log'), '-emberfieldMovementProbe', $output, '-stressCount', [string]$count, '-stressScenario', $Scenario, '-stressSeconds', [string]$Seconds) + $ExtraArguments
        $argumentLine = ($arguments | ForEach-Object { ConvertTo-UnityProcessArgument $_ }) -join ' '
        $process = Start-Process -FilePath $player -ArgumentList $argumentLine -WorkingDirectory $project -WindowStyle Hidden -PassThru
        try {
            $deadline = [DateTime]::UtcNow.AddSeconds($Seconds + 60)
            while (-not $process.WaitForExit(1000)) {
                if ([DateTime]::UtcNow -ge $deadline) { $process.Kill(); throw "Movement player timed out. Inspect $output\player.log." }
            }
            if ($process.ExitCode -ne 0) { throw "Player exited $($process.ExitCode). Inspect $output\player.log." }
        }
        finally { $process.Dispose() }
        $reportPath = Join-Path $output 'movement-player.json'
        if (-not (Test-Path -LiteralPath $reportPath)) { throw 'Player produced no movement report.' }
        $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
        if (-not $report.Passed -or $report.Sample.Units -ne $count) { throw "Movement measurement failed: $reportPath" }
        if ($report.Width -ne $Width -or $report.Height -ne $Height -or $report.Sample.Scenario -ne $Scenario) { throw "Player did not use the requested resolution/scenario: $reportPath" }
        if ($report.VSyncCount -ne 0 -or $report.TargetFrameRate -ne -1 -or $report.BatchPlayer) { throw "Player did not use the declared uncapped rendering mode: $reportPath" }
        if (Select-String -LiteralPath (Join-Path $output 'player.log') -Pattern 'Exception:|Error:|Shader error') { throw "Runtime error: $output\player.log" }
        Write-Host ("{0} units: frame p50 {1:N2} ms / p95 {2:N2} ms; tick p95 {3:N3} ms; {4} samples. {5}" -f $count, $report.Sample.FrameP50Milliseconds, $report.Sample.FrameP95Milliseconds, $report.Sample.TickP95Milliseconds, $report.Sample.FrameSamples, $reportPath)
        Write-Host ("{0} units presentation p50/p95/p99 {1:N3} / {2:N3} / {3:N3} ms (view {4:N3} / {5:N3} / {6:N3} ms)." -f $count, $report.Sample.PresentationP50Milliseconds, $report.Sample.PresentationP95Milliseconds, $report.Sample.PresentationP99Milliseconds, $report.Sample.ViewP50Milliseconds, $report.Sample.ViewP95Milliseconds, $report.Sample.ViewP99Milliseconds)
        Write-Host ("{0} units tail/pacing: frame p99 {1:N2} ms / p99.9 {2:N2} ms / max {3:N2} ms; stddev {4:N2} ms; {5} hitch frame(s) above {6:N1} ms; tick p99 {7:N3} ms / max {8:N3} ms." -f $count, $report.Sample.FrameP99Milliseconds, $report.Sample.FrameP999Milliseconds, $report.Sample.FrameMaxMilliseconds, $report.Sample.FrameStdDevMilliseconds, $report.Sample.FrameHitchCount, $report.Sample.FrameHitchThresholdMilliseconds, $report.Sample.TickP99Milliseconds, $report.Sample.TickMaxMilliseconds)
    }
    exit 0
}
catch { Write-Error -Message $_.Exception.Message -ErrorAction Continue; exit 1 }
