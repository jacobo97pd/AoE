[CmdletBinding()]
param(
    [string]$ProjectPath = '',
    [string]$PlayerPath = '',
    [ValidatePattern('^[A-Za-z0-9_-]+$')][string]$Label = 'baseline',
    [ValidateRange(640, 7680)][int]$Width = 1280,
    [ValidateRange(480, 4320)][int]$Height = 720,
    # Passed through to the player, e.g. -emberfieldProceduralUnits to measure without the Meshy art.
    [string[]]$ExtraArguments = @(),
    # Renders as a phone of this tier (-emberfieldMobileTier); baseline is the Mobile quality level with the shipped
    # pipeline asset untouched. Empty measures the desktop.
    [ValidateSet('', 'baseline', 'low', 'mid', 'high', 'auto')][string]$MobileTier = ''
)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($ProjectPath)) { $ProjectPath = Split-Path -Parent $PSScriptRoot }
. (Join-Path $PSScriptRoot 'Unity-Batch.ps1')
try {
    $project = (Resolve-Path -LiteralPath $ProjectPath).Path
    if ([string]::IsNullOrWhiteSpace($PlayerPath)) { $PlayerPath = Join-Path $project 'Builds\Windows\Emberfield.exe' }
    $player = (Resolve-Path -LiteralPath $PlayerPath).Path
    $output = Join-Path $project "TestResults\OfflinePlayer-${Label}-${Width}x${Height}"
    [void](New-Item -ItemType Directory -Path $output -Force)
    foreach ($name in @('offline-render.json', 'profiler-art.png', 'profiler-close.png', 'player.log', 'allocations.raw')) {
        $stale = Join-Path $output $name
        if (Test-Path -LiteralPath $stale) { Remove-Item -LiteralPath $stale -Force }
    }
    $arguments = @('-force-d3d11', '-screen-fullscreen', '0', '-screen-width', [string]$Width, '-screen-height', [string]$Height,
        '-logFile', (Join-Path $output 'player.log'), '-emberfieldOffline', 'Dominion', '-emberfieldFaction', 'aven', '-emberfieldOfflineProfile', $output) + $ExtraArguments
    if ($MobileTier) { $arguments += @('-emberfieldMobileTier', $MobileTier) }
    $argumentLine = ($arguments | ForEach-Object { ConvertTo-UnityProcessArgument $_ }) -join ' '
    $process = Start-Process -FilePath $player -ArgumentList $argumentLine -WorkingDirectory $project -WindowStyle Hidden -PassThru
    try {
        $deadline = [DateTime]::UtcNow.AddSeconds(180)
        while (-not $process.WaitForExit(1000)) {
            if ([DateTime]::UtcNow -ge $deadline) { $process.Kill(); throw "Offline player timed out. Inspect $output\player.log." }
        }
        if ($process.ExitCode -ne 0) { throw "Player exited $($process.ExitCode). Inspect $output\player.log." }
    }
    finally { $process.Dispose() }
    $reportPath = Join-Path $output 'offline-render.json'
    if (-not (Test-Path -LiteralPath $reportPath)) { throw 'Player produced no offline rendering report.' }
    $report = Get-Content -LiteralPath $reportPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if (-not $report.Passed -or -not $report.CaptureHasPixels) { throw "Offline measurement failed: $reportPath" }
    if ($report.Width -ne $Width -or $report.Height -ne $Height -or $report.Fixture -ne 'amber-crossing-aven-dominion-natural-ai-6000-v1') { throw "Wrong resolution or fixture: $reportPath" }
    if ($report.VSyncCount -ne 0 -or $report.TargetFrameRate -ne -1 -or $report.BatchPlayer -or $report.GraphicsApi -ne 'Direct3D11') { throw "Wrong rendering mode: $reportPath" }
    if ($report.SampleStartTick -ne 6060 -or $report.SampleEndTick -ne 6460 -or $report.SimulationTicks -ne 400 -or $report.RenderedCameraFrames -ne $report.FrameSamples) { throw "Workload or render proof mismatch: $reportPath" }
    if ($report.SimulationToWallRatio -lt .95 -or $report.SimulationToWallRatio -gt 1.05) { throw "Simulation did not keep real time: $reportPath" }
    if (Select-String -LiteralPath (Join-Path $output 'player.log') -Pattern 'Exception:|Error:|Shader error') { throw "Runtime error: $output\player.log" }
    $expectedTier = if ($MobileTier -eq 'auto') { $report.MobileTier } elseif ($MobileTier) { $MobileTier } else { 'desktop' }
    # Players built before the tiers report no MobileTier; they can only have measured the desktop.
    if (($MobileTier -or $report.MobileTier) -and $report.MobileTier -ne $expectedTier -or ($MobileTier -eq 'auto' -and $report.MobileTier -notin @('low', 'mid', 'high'))) { throw "Wrong mobile tier '$($report.MobileTier)': $reportPath" }
    Write-Host ("Offline serialized offscreen: frame p50 {0:N2} ms / p95 {1:N2} ms; World.Tick p95 {2:N3} ms; AI p95 {3:N3} ms; view p95 {4:N3} ms; {5} rendered samples. GPU available: {6}. {7}" -f $report.FrameMilliseconds.P50, $report.FrameMilliseconds.P95, $report.WorldTickMilliseconds.P95, $report.AiPairPerTickMilliseconds.P95, $report.PresentationMilliseconds.P95, $report.FrameSamples, $report.GpuTimingAvailable, $reportPath)
    Write-Host ("Frame tail/pacing: p99 {0:N2} ms / p99.9 {1:N2} ms / max {2:N2} ms; stddev {3:N2} ms; {4} hitch frame(s) above {5:N1} ms; World.Tick p99 {6:N3} ms / max {7:N3} ms." -f $report.FrameMilliseconds.P99, $report.FrameMilliseconds.P999, $report.FrameMilliseconds.Maximum, $report.FrameMilliseconds.StdDev, $report.HitchCount, $report.HitchThresholdMilliseconds, $report.WorldTickMilliseconds.P99, $report.WorldTickMilliseconds.Maximum)
    $counter = @{}; foreach ($entry in $report.Counters) { $counter[$entry.Name] = $entry }
    function Median([string]$name) { if ($counter.ContainsKey($name) -and $counter[$name].Available) { $counter[$name].Values.P50 } else { -1 } }
    Write-Host ("Rendering as {0}: scale {1:N2}, MSAA {2}, shadows {3}/{4} cascade(s)/{5} m; median batches {6:N0}, draw calls {7:N0}, SetPass {8:N0}, triangles {9:N0}, vertices {10:N0}, shadow casters {11:N0}, texture memory {12:N0} MB; CPU main p95 {13:N2} ms, render p95 {14:N2} ms." -f $report.MobileTier, $report.RenderScale, $report.MsaaSamples, $report.ShadowResolution, $report.ShadowCascades, $report.ShadowDistance, (Median 'Batches Count'), (Median 'Draw Calls Count'), (Median 'SetPass Calls Count'), (Median 'Triangles Count'), (Median 'Vertices Count'), (Median 'Shadow Casters Count'), ((Median 'Texture Memory') / 1MB), $report.CpuMainThreadFrameMilliseconds.P95, $report.CpuRenderThreadFrameMilliseconds.P95)
    $gcAlloc = $counter['GC Allocated In Frame']
    if ($gcAlloc -and $gcAlloc.Available) { Write-Host ("GC allocated/frame: median {0:N0} B / p95 {1:N0} B / max {2:N0} B; collections during sample (gen0/1/2) {3}/{4}/{5}." -f $gcAlloc.Values.P50, $gcAlloc.Values.P95, $gcAlloc.Values.Maximum, $report.GcCollections[0], $report.GcCollections[1], $report.GcCollections[2]) }
    Write-Host ("Cold first-use proxy (one-time post-prewarm SyncPresentation): {0:N2} ms." -f $report.InitialPresentationSyncMilliseconds)
    if ($report.WorstFrames -and @($report.WorstFrames).Count -gt 0) {
        $top = @($report.WorstFrames)[0]
        Write-Host ("Worst sampled frame: {0:N2} ms at tick {1} (tick {2:N2} / ai {3:N2} / view {4:N2} / render {5:N2} / other {6:N2} ms) - dominant: {7}." -f $top.TotalMilliseconds, $top.Tick, $top.WorldTickMilliseconds, $top.AiMilliseconds, $top.PresentationMilliseconds, $top.RenderMilliseconds, $top.UnaccountedMilliseconds, $top.DominantMarker)
    }
    exit 0
}
catch { Write-Error -Message $_.Exception.Message -ErrorAction Continue; exit 1 }
