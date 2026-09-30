[CmdletBinding()]
param(
    [string]$ProjectPath = '',
    [string]$BaselinePath = '',
    [string]$OptimizedPath = '',
    [string]$BaselineRenderPath = '',
    [string]$OptimizedRenderPath = '',
    [string]$OutputPrefix = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ([string]::IsNullOrWhiteSpace($ProjectPath)) { $ProjectPath = Split-Path -Parent $PSScriptRoot }
$project = (Resolve-Path -LiteralPath $ProjectPath).Path
if ([string]::IsNullOrWhiteSpace($BaselinePath)) { $BaselinePath = Join-Path $project 'TestResults/offline-performance-baseline.json' }
if ([string]::IsNullOrWhiteSpace($OptimizedPath)) { $OptimizedPath = Join-Path $project 'TestResults/offline-performance-optimized.json' }
if ([string]::IsNullOrWhiteSpace($OutputPrefix)) { $OutputPrefix = Join-Path $project 'TestResults/offline-performance-comparison' }
$OutputPrefix = [IO.Path]::GetFullPath($OutputPrefix)
[void](New-Item -ItemType Directory -Path (Split-Path -Parent $OutputPrefix) -Force)
$comparison = [ordered]@{
    Utc = [DateTime]::UtcNow.ToString('o')
    ComparisonValid = $false
    PerformanceBudgetPassed = $null
    Method = 'Compare 26 keyed CPU cases: five synthetic sizes plus both factions/modes at early and mid checkpoints, each repeated twice. Rules/map/fixture/runner/method/platform, full windows, observed start/end state INCLUDING both visibility/exploration masks, and ordered command/result traces must match before timing is compared. Fingerprints are observable-state evidence, not full private-state or cross-platform determinism proof. CPU p95 values are per-run quantiles; repetition summaries are means/ranges of two quantiles, never pooled p95. A 5% and non-overlapping-range screen is descriptive, not statistical significance. Navigation has only total/max timing, no p95. GC/heap cover the whole diagnostic loop; heap change is not allocated bytes. No ordinary FPS or mobile performance claim.'
    BaselinePath = $BaselinePath; OptimizedPath = $OptimizedPath
    BaselineRenderPath = $BaselineRenderPath; OptimizedRenderPath = $OptimizedRenderPath
    ValidationError = $null; Cpu = $null; Rendered = $null
}

function Require([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
function Read-Report([string]$Path) {
    Require (Test-Path -LiteralPath $Path -PathType Leaf) "Missing report: $Path"
    return (Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json)
}
function Assert-Same($Before, $After, [string[]]$Fields, [string]$Context) {
    foreach ($field in $Fields) {
        $a = $Before.$field | ConvertTo-Json -Depth 8 -Compress
        $b = $After.$field | ConvertTo-Json -Depth 8 -Compress
        Require ($a -ceq $b) "$Context changed field $field. Baseline=$a; optimized=$b"
    }
}
function Assert-Hash([string]$Hash, [string]$Context) { Require ($Hash -cmatch '^[A-Fa-f0-9]{64}$') "Invalid SHA256: $Context" }
function Case-Key($Case, [switch]$WithoutRepetition) {
    $key = '{0}/{1}/{2}/{3}/{4}' -f $Case.Fixture, $Case.RequestedMovers, $Case.Mode, $Case.PlayerOneFaction, $Case.RequestedWarmupTicks
    if (-not $WithoutRepetition) { $key += '/' + $Case.Repetition }
    return $key
}
function Expected-Keys {
    foreach ($count in @(50, 100, 200, 300, 500)) { foreach ($rep in @(1, 2)) { "SyntheticMovers/$count/Conquest/aven/0/$rep" } }
    foreach ($mode in @('Conquest', 'Dominion')) { foreach ($faction in @('aven', 'serevin')) {
        foreach ($rep in @(1, 2)) { "ShippedEarly/0/$mode/$faction/0/$rep"; "ShippedMid/0/$mode/$faction/6000/$rep" }
    } }
}
$identityFields = @('FixtureVersion', 'Description', 'RequestedSampleTicks', 'RequestedWarmupTicks', 'MeasuredTicks', 'StartTick', 'EndTick',
    'WindowAvailable', 'MatchFinished', 'MatchReason', 'WinnerId', 'StartStateSha256', 'EndStateSha256', 'CommandTraceSha256',
    'InitialUnits', 'FinalUnits', 'PeakUnits', 'PeakMovingUnits', 'PeakProjectiles', 'SurvivingRequestedMovers',
    'PeakActiveResearch', 'PeakActiveCharters', 'PeakCharterAffectedUnits', 'PeakBoostedProducers', 'PeakGatherers',
    'AiThinks', 'AiCommands', 'AiAccepted', 'ScriptCommands', 'ScriptAccepted', 'ScriptResults')

function Validate-Cpu($Report, [string]$Name) {
    Require ($Report.Complete -and $Report.RepetitionsMatch -and $Report.SyntheticFixtureValid) "$Name did not complete valid matching repetitions."
    Require ($Report.FixtureVersion -ceq 'offline-cpu-v1') "$Name has an unsupported fixture version."
    Require ($Report.StressSeconds -eq 120 -and $Report.ShippedSeconds -eq 60 -and $Report.MidStartSeconds -eq 300 -and $Report.IncludeShipped) "$Name does not cover the declared full-duration fixture."
    Require ($Report.Repetitions -eq 2 -and $Report.VerifiedRepetitionPairs -eq 13 -and @($Report.Cases).Count -eq 26) "$Name must contain 26 cases and 13 verified repetition pairs."
    Require ($Report.FingerprintMethod.Contains('both visible/explored masks')) "$Name fingerprint does not declare fog state coverage."
    foreach ($field in @('RulesSha256', 'MapSha256', 'SourceSha256', 'RunnerSha256')) { Assert-Hash $Report.$field "$Name/$field" }
    $indexed = @{}
    foreach ($case in $Report.Cases) {
        $key = Case-Key $case
        Require (-not $indexed.ContainsKey($key)) "$Name duplicates case $key."
        Require ($case.FixtureVersion -ceq $Report.FixtureVersion) "$Name/$key has a different fixture version."
        Require ($case.WindowAvailable -and -not $case.MatchFinished -and $case.RepetitionMatchesFirst) "$Name/$key has an unavailable, ended or nonmatching window."
        $duration = if ($case.Fixture -eq 'SyntheticMovers') { 2400 } else { 1200 }
        Require ($case.RequestedSampleTicks -eq $duration -and $case.MeasuredTicks -eq $duration -and $case.EndTick - $case.StartTick -eq $duration) "$Name/$key has a truncated or inconsistent sample."
        Require ($case.StartTick -eq $case.RequestedWarmupTicks -and $case.WorldTicks.Samples -eq $duration -and $case.AiTicks.Samples -eq $duration) "$Name/$key has inconsistent tick windows."
        foreach ($field in @('StartStateSha256', 'EndStateSha256', 'CommandTraceSha256')) { Assert-Hash $case.$field "$Name/$key/$field" }
        if ($case.RequestedMovers -gt 0) {
            Require ($case.SurvivingRequestedMovers -eq $case.RequestedMovers -and $case.ScriptAccepted -eq $case.ScriptCommands) "$Name/$key lost requested movers or rejected scripted workload."
        }
        if ($case.AllocationMeasurementAvailable) {
            Require ($case.AllocationProbeObservedBytes -ge 4096 -and $case.WorldAllocatedBytes -ge 0 -and $case.AiAllocatedBytes -ge 0 -and $case.ScriptAllocatedBytes -ge 0) "$Name/$key has unverified allocation values."
        }
        else { Require ($case.WorldAllocatedBytes -eq -1 -and $case.AiAllocatedBytes -eq -1 -and $case.ScriptAllocatedBytes -eq -1) "$Name/$key must use -1 for unavailable allocation values." }
        $indexed.Add($key, $case)
    }
    foreach ($key in Expected-Keys) { Require ($indexed.ContainsKey($key)) "$Name is missing required case $key." }
    foreach ($key in @($indexed.Keys | Sort-Object)) {
        if (-not $key.EndsWith('/2')) { continue }
        $first = $indexed[$key.Substring(0, $key.Length - 1) + '1']
        Assert-Same $first $indexed[$key] $identityFields "$Name repetition $key"
    }
    return $indexed
}
function Metric([double]$Before, [double]$After, [bool]$Available = $true) {
    if (-not $Available) { return [pscustomobject]@{ Available = $false; Baseline = -1; Optimized = -1; ChangePercent = $null } }
    Require (-not [double]::IsNaN($Before) -and -not [double]::IsInfinity($Before) -and $Before -ge 0 -and -not [double]::IsNaN($After) -and -not [double]::IsInfinity($After) -and $After -ge 0) 'Invalid negative/non-finite performance value.'
    $change = if ($Before -gt 0) { ($After - $Before) / $Before * 100 } else { $null }
    return [pscustomobject]@{ Available = $true; Baseline = $Before; Optimized = $After; ChangePercent = $change }
}
function Cpu-Row($Before, $After) {
    $available = $Before.AllocationMeasurementAvailable -and $After.AllocationMeasurementAvailable
    return [pscustomobject]@{
        Key = (Case-Key $Before); Group = (Case-Key $Before -WithoutRepetition); Repetition = $Before.Repetition
        StartStateSha256 = $Before.StartStateSha256; EndStateSha256 = $Before.EndStateSha256; CommandTraceSha256 = $Before.CommandTraceSha256
        WorldP95Milliseconds = (Metric $Before.WorldTicks.P95Milliseconds $After.WorldTicks.P95Milliseconds)
        MovingWorldP95Milliseconds = (Metric $Before.MovingWorldTicks.P95Milliseconds $After.MovingWorldTicks.P95Milliseconds ($Before.MovingWorldTicks.Samples -gt 0 -and $After.MovingWorldTicks.Samples -gt 0))
        AiThinkP95Milliseconds = (Metric $Before.AiThinkingTicks.P95Milliseconds $After.AiThinkingTicks.P95Milliseconds ($Before.AiThinkingTicks.Samples -gt 0 -and $After.AiThinkingTicks.Samples -gt 0))
        MoveCommandP95Milliseconds = (Metric $Before.MoveCommands.P95Milliseconds $After.MoveCommands.P95Milliseconds ($Before.MoveCommands.Samples -gt 0 -and $After.MoveCommands.Samples -gt 0))
        MoveCommandSamples = $Before.MoveCommands.Samples
        NavigationTotalMilliseconds = (Metric $Before.NavigationMilliseconds $After.NavigationMilliseconds)
        NavigationMaxQueryMilliseconds = (Metric $Before.MaxNavigationQueryMilliseconds $After.MaxNavigationQueryMilliseconds)
        PathQueries = (Metric $Before.PathQueries $After.PathQueries); AttackQueries = (Metric $Before.AttackQueries $After.AttackQueries)
        ConnectivityRebuilds = (Metric $Before.ConnectivityRebuilds $After.ConnectivityRebuilds)
        SharedFieldBuilds = (Metric $Before.SharedFieldBuilds $After.SharedFieldBuilds); SharedFieldCacheHits = (Metric $Before.SharedFieldCacheHits $After.SharedFieldCacheHits)
        RecoveryQueries = (Metric $Before.RecoveryQueries $After.RecoveryQueries)
        AllocationComparable = $available
        WorldAllocatedBytes = (Metric $Before.WorldAllocatedBytes $After.WorldAllocatedBytes $available)
        AiAllocatedBytes = (Metric $Before.AiAllocatedBytes $After.AiAllocatedBytes $available)
        ScriptAllocatedBytes = (Metric $Before.ScriptAllocatedBytes $After.ScriptAllocatedBytes $available)
        GcCollectionsBaseline = @($Before.Gen0Collections, $Before.Gen1Collections, $Before.Gen2Collections)
        GcCollectionsOptimized = @($After.Gen0Collections, $After.Gen1Collections, $After.Gen2Collections)
        HeapBeforeBaselineBytes = $Before.ManagedHeapBeforeBytes; HeapPeakBaselineBytes = $Before.ManagedHeapPeakBytes; HeapAfterBaselineBytes = $Before.ManagedHeapAfterBytes
        HeapBeforeOptimizedBytes = $After.ManagedHeapBeforeBytes; HeapPeakOptimizedBytes = $After.ManagedHeapPeakBytes; HeapAfterOptimizedBytes = $After.ManagedHeapAfterBytes
    }
}
function Repeat-Summary($Rows, [string]$Field) {
    $values = @($Rows | ForEach-Object { $_.$Field })
    if (@($values | Where-Object { -not $_.Available }).Count -gt 0) { return [pscustomobject]@{ Available = $false; Interpretation = 'Unavailable'; BaselineMean = -1; OptimizedMean = -1; ChangePercent = $null; BaselineMin = -1; BaselineMax = -1; OptimizedMin = -1; OptimizedMax = -1 } }
    $a = $values.Baseline | Measure-Object -Average -Minimum -Maximum
    $b = $values.Optimized | Measure-Object -Average -Minimum -Maximum
    $change = if ($a.Average -gt 0) { ($b.Average - $a.Average) / $a.Average * 100 } else { $null }
    $interpretation = 'Overlapping repetition ranges or change below 5%; noise-sensitive.'
    if ($null -ne $change -and $change -le -5 -and $b.Maximum -lt $a.Minimum) { $interpretation = 'Consistent reduction in these two repetitions; not a significance test.' }
    if ($null -ne $change -and $change -ge 5 -and $b.Minimum -gt $a.Maximum) { $interpretation = 'Consistent increase in these two repetitions; inspect regression.' }
    return [pscustomobject]@{ Available = $true; BaselineMean = $a.Average; OptimizedMean = $b.Average; ChangePercent = $change; BaselineMin = $a.Minimum; BaselineMax = $a.Maximum; OptimizedMin = $b.Minimum; OptimizedMax = $b.Maximum; Interpretation = $interpretation }
}
function Validate-Render($Report, [string]$Name) {
    Require ($Report.Passed -and $Report.CaptureHasPixels -and $Report.DevelopmentBuild -and -not $Report.BatchPlayer -and -not $Report.SampleBufferOverflow) "$Name rendered measurement did not pass."
    Require ($Report.Fixture -ceq 'amber-crossing-aven-dominion-natural-ai-6000-v1' -and $Report.SampleStartTick -eq 6060 -and $Report.SampleEndTick -eq 6460 -and $Report.SimulationTicks -eq 400) "$Name has the wrong rendered workload."
    Require ($Report.FrameSamples -ge 120 -and $Report.RenderedCameraFrames -eq $Report.FrameSamples -and $Report.FrameMilliseconds.Samples -eq $Report.FrameSamples) "$Name lacks matching rendered-frame proof."
    Require ($Report.WorldTickMilliseconds.Samples -eq 400 -and $Report.AiPairPerTickMilliseconds.Samples -eq 400 -and $Report.PresentationMilliseconds.Samples -eq $Report.FrameSamples -and $Report.RenderAndReadbackMilliseconds.Samples -eq $Report.FrameSamples) "$Name has inconsistent CPU sample counts."
    Require ($Report.SimulationToWallRatio -ge .95 -and $Report.SimulationToWallRatio -le 1.05 -and $Report.VSyncCount -eq 0 -and $Report.TargetFrameRate -eq -1 -and $Report.GraphicsApi -ceq 'Direct3D11') "$Name did not meet the declared real-time/rendering method."
    foreach ($field in @('RulesSha256', 'MapSha256', 'CheckpointSha256', 'SampleStartSha256', 'SampleEndSha256')) { Assert-Hash $Report.$field "$Name/$field" }
}
function Compare-Render($Before, $After) {
    Validate-Render $Before 'Baseline render'; Validate-Render $After 'Optimized render'
    Assert-Same $Before $After @('Method', 'Fixture', 'Unity', 'RulesSha256', 'MapSha256', 'CheckpointSha256', 'SampleStartSha256', 'SampleEndSha256',
        'Width', 'Height', 'Quality', 'RenderPipeline', 'RenderScale', 'MsaaSamples', 'CameraPosition', 'CameraRotation', 'OrthographicSize',
        'OperatingSystem', 'Cpu', 'LogicalProcessors', 'MemoryMegabytes', 'Gpu', 'GraphicsApi', 'GraphicsMemoryMegabytes',
        'PrewarmTicks', 'WarmupTicks', 'WarmupSeconds', 'RequestedSampleSeconds', 'SampleStartTick', 'SampleEndTick', 'SimulationTicks',
        'AiThinks', 'DeathsDuringSample', 'ProjectilePeak', 'Checkpoint', 'Initial', 'Final') 'Rendered workload'
    $metrics = [ordered]@{}
    foreach ($field in @('FrameMilliseconds', 'WorldTickMilliseconds', 'AiPairPerTickMilliseconds', 'PresentationMilliseconds', 'RenderAndReadbackMilliseconds')) {
        $metrics[$field] = [ordered]@{ P50 = (Metric $Before.$field.P50 $After.$field.P50); P95 = (Metric $Before.$field.P95 $After.$field.P95); P99 = (Metric $Before.$field.P99 $After.$field.P99); Maximum = (Metric $Before.$field.Maximum $After.$field.Maximum) }
    }
    $beforeGpuSource = if ($null -ne $Before.PSObject.Properties['GpuTimingSource']) { $Before.GpuTimingSource } else { 'FrameTimingManager (legacy report)' }
    $afterGpuSource = if ($null -ne $After.PSObject.Properties['GpuTimingSource']) { $After.GpuTimingSource } else { 'FrameTimingManager (legacy report)' }
    $gpu = $Before.GpuTimingAvailable -and $After.GpuTimingAvailable -and $Before.GpuFrameMilliseconds.Samples -gt 0 -and $After.GpuFrameMilliseconds.Samples -gt 0 -and $beforeGpuSource -ceq $afterGpuSource
    $counterRows = @()
    foreach ($counter in $Before.Counters) {
        $matches = @($After.Counters | Where-Object { $_.Name -ceq $counter.Name })
        Require ($matches.Count -eq 1) "Rendered counter missing or duplicated: $($counter.Name)"
        $other = $matches[0]
        Require ($counter.Unit -ceq $other.Unit) "Rendered counter units changed: $($counter.Name)"
        $available = $counter.Available -and $other.Available
        $counterRows += [pscustomobject]@{ Name = $counter.Name; Unit = $counter.Unit; P95 = (Metric $counter.Values.P95 $other.Values.P95 $available); Mean = (Metric $counter.Values.Mean $other.Values.Mean $available); BaselineReason = $counter.Reason; OptimizedReason = $other.Reason }
    }
    return [ordered]@{
        WorkloadMatched = $true; BaselineBuildGuid = $Before.BuildGuid; OptimizedBuildGuid = $After.BuildGuid
        Width = $Before.Width; Height = $Before.Height; RenderScale = $Before.RenderScale
        Interpretation = 'One before/after rendered pair. Descriptive serialized URP/Canvas/readback throughput only; no significance, ordinary display FPS, isolated-camera GPU, mobile or performance-budget pass. Frame counts vary with throughput, but the exact400-tick simulation workload and camera/state hashes match. Frame-level profiler counters and delayed GPU telemetry are advisory.'
        BaselineFrameSamples = $Before.FrameSamples; OptimizedFrameSamples = $After.FrameSamples
        Metrics = $metrics; GpuP95Milliseconds = (Metric $Before.GpuFrameMilliseconds.P95 $After.GpuFrameMilliseconds.P95 $gpu)
        GpuSourceBaseline = $beforeGpuSource; GpuSourceOptimized = $afterGpuSource
        Counters = $counterRows; GcCollectionsBaseline = $Before.GcCollections; GcCollectionsOptimized = $After.GcCollections
        HeapBeforeBaselineBytes = $Before.ManagedHeapBeforeBytes; HeapAfterBaselineBytes = $Before.ManagedHeapAfterBytes
        HeapBeforeOptimizedBytes = $After.ManagedHeapBeforeBytes; HeapAfterOptimizedBytes = $After.ManagedHeapAfterBytes
    }
}
function Number([double]$Value, [string]$Format = '0.000') { return $Value.ToString($Format, [Globalization.CultureInfo]::InvariantCulture) }
function Pair($Metric) {
    if (-not $Metric.Available) { return 'unavailable' }
    $delta = if ($null -eq $Metric.ChangePercent) { 'no % baseline' } else { (Number $Metric.ChangePercent '0.0') + '%' }
    return (Number $Metric.Baseline) + ' -> ' + (Number $Metric.Optimized) + ' (' + $delta + ')'
}
function Write-Comparison {
    [IO.File]::WriteAllText($OutputPrefix + '.json', ($comparison | ConvertTo-Json -Depth 15), [Text.UTF8Encoding]::new($false))
    $text = [Text.StringBuilder]::new()
    [void]$text.AppendLine('# Offline performance comparison').AppendLine()
    [void]$text.AppendLine($comparison.Method).AppendLine()
    [void]$text.AppendLine('Validation: **' + $(if ($comparison.ComparisonValid) { 'matched workload' } else { 'FAILED' }) + '**. Performance-budget pass: not evaluated.').AppendLine()
    [void]$text.AppendLine('Baseline: `' + $BaselinePath + '`; optimized: `' + $OptimizedPath + '`.').AppendLine()
    if ($null -ne $comparison.ValidationError) { [void]$text.AppendLine('Failure: ' + $comparison.ValidationError) }
    if ($comparison.ComparisonValid) {
        [void]$text.AppendLine('Source SHA256: `' + $comparison.Cpu.BaselineSourceSha256 + '` -> `' + $comparison.Cpu.OptimizedSourceSha256 + '`. All 26 start/end hashes (including fog) and ordered command traces match. Both runs independently repeat all 13 workloads.').AppendLine()
        [void]$text.AppendLine('| Key: fixture / N / mode / P1 / warmup tick / repetition | World p95 ms | AI think p95 ms | Move command p95 ms | Navigation total ms |').AppendLine('| --- | --- | --- | --- | --- |')
        foreach ($row in $comparison.Cpu.Cases) { [void]$text.AppendLine('| ' + $row.Key + ' | ' + (Pair $row.WorldP95Milliseconds) + ' | ' + (Pair $row.AiThinkP95Milliseconds) + ' | ' + (Pair $row.MoveCommandP95Milliseconds) + ' | ' + (Pair $row.NavigationTotalMilliseconds) + ' |') }
        [void]$text.AppendLine().AppendLine('Positive change means slower/more; negative means faster/less. Synthetic move p95 has only three command samples per repetition. Navigation includes path, attack and connectivity work across World/AI/script windows; no navigation p95 was collected.').AppendLine()
        [void]$text.AppendLine('| Workload | Mean of two World p95 values, baseline -> optimized ms | World interpretation |').AppendLine('| --- | --- | --- |')
        foreach ($row in $comparison.Cpu.RepetitionSummaries) { [void]$text.AppendLine('| ' + $row.Key + ' | ' + (Number $row.WorldP95.BaselineMean) + ' -> ' + (Number $row.WorldP95.OptimizedMean) + ' | ' + $row.WorldP95.Interpretation + ' |') }
        [void]$text.AppendLine().AppendLine('| Case | GC collections 0/1/2 baseline -> optimized | Allocation World / AI / script bytes | Heap after baseline -> optimized bytes |').AppendLine('| --- | --- | --- | --- |')
        foreach ($row in $comparison.Cpu.Cases) {
            $allocated = if ($row.AllocationComparable) { (Pair $row.WorldAllocatedBytes) + ' / ' + (Pair $row.AiAllocatedBytes) + ' / ' + (Pair $row.ScriptAllocatedBytes) } else { 'unavailable (-1), not zero' }
            [void]$text.AppendLine('| ' + $row.Key + ' | ' + ($row.GcCollectionsBaseline -join '/') + ' -> ' + ($row.GcCollectionsOptimized -join '/') + ' | ' + $allocated + ' | ' + $row.HeapAfterBaselineBytes + ' -> ' + $row.HeapAfterOptimizedBytes + ' |')
        }
        [void]$text.AppendLine().AppendLine('Heap values include runtime state, caches and diagnostics, with no forced collection; compare allocations only when both capability probes passed. JSON includes per-repetition query counters, GC/heap values and repetition ranges for World, AI, move commands and navigation.').AppendLine()
        if ($null -ne $comparison.Rendered) {
            [void]$text.AppendLine('## Rendered pair').AppendLine().AppendLine($comparison.Rendered.Interpretation).AppendLine()
            [void]$text.AppendLine('Builds: `' + $comparison.Rendered.BaselineBuildGuid + '` -> `' + $comparison.Rendered.OptimizedBuildGuid + '`. Output ' + $comparison.Rendered.Width + 'x' + $comparison.Rendered.Height + ', URP render scale ' + $comparison.Rendered.RenderScale + '.').AppendLine()
            [void]$text.AppendLine('| Metric | p50 ms | p95 ms |').AppendLine('| --- | --- | --- |')
            foreach ($key in $comparison.Rendered.Metrics.Keys) { [void]$text.AppendLine('| ' + $key + ' | ' + (Pair $comparison.Rendered.Metrics[$key].P50) + ' | ' + (Pair $comparison.Rendered.Metrics[$key].P95) + ' |') }
            [void]$text.AppendLine().AppendLine('Delayed platform GPU p95: ' + (Pair $comparison.Rendered.GpuP95Milliseconds) + '.').AppendLine()
            [void]$text.AppendLine('| Completed-frame recorder | Units | Mean before -> after | p95 before -> after |').AppendLine('| --- | --- | --- | --- |')
            foreach ($counter in $comparison.Rendered.Counters) { [void]$text.AppendLine('| ' + $counter.Name + ' | ' + $counter.Unit + ' | ' + (Pair $counter.Mean) + ' | ' + (Pair $counter.P95) + ' |') }
        }
        else { [void]$text.AppendLine('No rendered pair was supplied. This comparison establishes no rendered-performance result.') }
    }
    [IO.File]::WriteAllText($OutputPrefix + '.md', $text.ToString(), [Text.UTF8Encoding]::new($false))
}

try {
    Require (($BaselineRenderPath.Length -eq 0) -eq ($OptimizedRenderPath.Length -eq 0)) 'Supply both rendered paths or neither.'
    $baseline = Read-Report $BaselinePath; $optimized = Read-Report $OptimizedPath
    $beforeCases = Validate-Cpu $baseline 'Baseline'; $afterCases = Validate-Cpu $optimized 'Optimized'
    Assert-Same $baseline $optimized @('FixtureVersion', 'RulesSha256', 'MapSha256', 'RunnerSha256', 'MeasurementMethod', 'FingerprintMethod', 'WarmupMethod',
        'UnityVersion', 'Runtime', 'OperatingSystem', 'Processor', 'ProcessorCount', 'SystemMemoryMegabytes', 'GraphicsDevice', 'StopwatchFrequency',
        'StressSeconds', 'ShippedSeconds', 'MidStartSeconds', 'Repetitions', 'IncludeShipped') 'CPU protocol/platform'
    $rows = @()
    foreach ($key in @($beforeCases.Keys | Sort-Object)) {
        $before = $beforeCases[$key]; $after = $afterCases[$key]
        Assert-Same $before $after $identityFields "CPU case $key"
        foreach ($field in @('WorldTicks', 'MovingWorldTicks', 'AiTicks', 'AiThinkingTicks', 'ScriptBatches', 'MoveCommands')) { Require ($before.$field.Samples -eq $after.$field.Samples) "CPU case $key changed $field sample count." }
        $rows += Cpu-Row $before $after
    }
    $groups = @()
    foreach ($group in ($rows | Group-Object Group | Sort-Object Name)) {
        $groups += [pscustomobject]@{ Key = $group.Name; Repetitions = $group.Count; WorldP95 = (Repeat-Summary $group.Group 'WorldP95Milliseconds'); AiThinkP95 = (Repeat-Summary $group.Group 'AiThinkP95Milliseconds'); MoveCommandP95 = (Repeat-Summary $group.Group 'MoveCommandP95Milliseconds'); NavigationTotal = (Repeat-Summary $group.Group 'NavigationTotalMilliseconds') }
    }
    $comparison.Cpu = [ordered]@{ CasesCompared = 26; IndependentRepetitionPairsPerRun = 13; FogStateIncluded = $true; BaselineSourceSha256 = $baseline.SourceSha256; OptimizedSourceSha256 = $optimized.SourceSha256; SourceChanged = $baseline.SourceSha256 -cne $optimized.SourceSha256; RulesSha256 = $baseline.RulesSha256; MapSha256 = $baseline.MapSha256; Cases = $rows; RepetitionSummaries = $groups }
    if ($BaselineRenderPath.Length -gt 0) { $comparison.Rendered = Compare-Render (Read-Report $BaselineRenderPath) (Read-Report $OptimizedRenderPath) }
    $comparison.ComparisonValid = $true
    Write-Comparison
    Write-Host "Matched all 26 CPU workloads and repetitions. Comparison: $OutputPrefix.md (performance budgets are not inferred)."
    exit 0
}
catch {
    $comparison.ValidationError = $_.Exception.Message
    $comparison.ComparisonValid = $false
    $comparison.Cpu = $null; $comparison.Rendered = $null
    Write-Comparison
    Write-Error -Message $_.Exception.Message -ErrorAction Continue
    exit 1
}
