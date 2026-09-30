<#
.SYNOPSIS
Records the fortification and siege showcase, then encodes it to video.

.DESCRIPTION
Runs the Windows player on the authored siege fixture, writing one JPEG per presented frame plus a frames.csv
index, then cuts those frames into videos with Blender's sequencer. There is no ffmpeg on this machine, which is
why Blender does the encoding.

Frames go to D: by default: this workstation's C: drive sits above 95% full and a run writes on the order of a
gigabyte. Nothing is written into the repository.

The frame rate of the recording says nothing about the frame rate of the game. Capture reads every frame back
from the GPU synchronously, which dominates the loop. Use tools/Profile-OfflinePlayer.ps1 for that question.
#>
[CmdletBinding()]
param(
    [string]$OutputRoot = 'D:\EmberfieldRecordings',
    [string]$BlenderPath = 'D:\CodexTooling\blender-portable\blender.exe',
    [string]$Encoder = 'D:\CodexTooling\pirate-crew\claude\encode_match.py',
    [ValidateRange(640, 3840)][int]$Width = 1920,
    [ValidateRange(360, 2160)][int]$Height = 1080,
    [ValidateRange(60, 7200)][int]$TimeoutSeconds = 2400,
    [switch]$SkipEncode
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$project = (Resolve-Path -LiteralPath (Split-Path -Parent $PSScriptRoot)).Path
$player = Join-Path $project 'Builds/Windows/Emberfield.exe'
if (-not (Test-Path -LiteralPath $player -PathType Leaf)) {
    throw "No player at $player. Run tools/Build-Unity.ps1 -Target Windows first."
}

$stamp = (Get-Date).ToString('yyyyMMdd-HHmmss')
$run = Join-Path $OutputRoot "siege-$stamp"
[void](New-Item -ItemType Directory -Path $run -Force)

# A run writes roughly a gigabyte of JPEGs. Refuse rather than fill the drive and fail halfway through.
$drive = (Get-Item $run).PSDrive
if ($drive.Free -lt 4GB) {
    throw "Only $([Math]::Round($drive.Free / 1GB, 1)) GB free on $($drive.Name): -- a recording needs about 2 GB of headroom."
}
Write-Host "Recording to $run ($([Math]::Round($drive.Free / 1GB, 1)) GB free on $($drive.Name):)"

$arguments = @(
    '-screen-width', $Width, '-screen-height', $Height, '-screen-fullscreen', '0',
    '-emberfieldSiegeShowcase', $run
)
$log = Join-Path $run 'player.log'
$process = Start-Process -FilePath $player -ArgumentList $arguments -PassThru -RedirectStandardOutput $log
try {
    # Windows PowerShell releases a short-lived child's handle before ExitCode can be read unless it is touched.
    $handle = $process.Handle
    if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
        $process.Kill(); [void]$process.WaitForExit(10000)
        throw "The showcase exceeded $TimeoutSeconds seconds. Inspect $log."
    }
    $process.WaitForExit(); $process.Refresh()
    if ($process.ExitCode -ne 0) { throw "The showcase exited with $($process.ExitCode). Inspect $log." }
}
finally { $process.Dispose() }

$report = Join-Path $run 'showcase.json'
if (-not (Test-Path -LiteralPath $report -PathType Leaf)) { throw "The showcase wrote no report. Inspect $log." }
$summary = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
$frames = Join-Path $run 'frames'
$written = @(Get-ChildItem -LiteralPath $frames -Filter 'frame-*.jpg' -File).Count
if ($written -ne $summary.frames) { throw "The report claims $($summary.frames) frames but $written were written." }
if ($summary.wallsRaised -lt 5) { throw "Only $($summary.wallsRaised) wall segments were raised; the fortification did not happen." }

$size = [Math]::Round((Get-ChildItem -LiteralPath $frames -File | Measure-Object Length -Sum).Sum / 1GB, 2)
Write-Host "Recorded $($summary.frames) frames ($($summary.battleFrames) battle) over $($summary.simulatedSeconds)s simulated; $size GB."
Write-Host "Fortification: $($summary.wallsRaised) wall segments raised. Deaths: $($summary.deaths)."

if ($SkipEncode) { Write-Host "Skipping the encode. Frames are in $frames."; return }
if (-not (Test-Path -LiteralPath $BlenderPath -PathType Leaf)) {
    Write-Warning "No Blender at $BlenderPath, so the frames were not encoded. Pass -BlenderPath or -SkipEncode."
    return
}

$video = Join-Path $run 'video'
[void](New-Item -ItemType Directory -Path $video -Force)
& $BlenderPath --background --factory-startup --python $Encoder -- $frames $video $Width $Height
if ($LASTEXITCODE -ne 0) { throw "Blender failed to encode. Frames remain in $frames." }

foreach ($name in @('timelapse.mp4', 'batallas.mp4')) {
    $path = Join-Path $video $name
    if (Test-Path -LiteralPath $path -PathType Leaf) {
        Write-Host ("  {0}  {1} MB" -f $name, [Math]::Round((Get-Item $path).Length / 1MB, 1))
    }
}
Write-Host "Video in $video. Stills beside the report in $run."
