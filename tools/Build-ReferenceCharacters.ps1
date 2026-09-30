[CmdletBinding()]
param(
    [string]$UnityPath = 'D:\Unity\Editors\6000.3.23f1\Editor\Unity.exe',
    [string]$ProjectPath = '',
    [switch]$AllowPartial,
    [switch]$BuildPlayer,
    [switch]$CapturePlayer,
    [ValidateRange(60,7200)][int]$TimeoutSeconds = 1800
)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($ProjectPath)) { $ProjectPath = Split-Path -Parent $PSScriptRoot }
. (Join-Path $PSScriptRoot 'Unity-Batch.ps1')
$batch = Resolve-UnityBatchPaths -UnityPath $UnityPath -ProjectPath $ProjectPath
$arguments = @('-quit','-executeMethod','Emberfield.Editor.ReferenceCharacterBaker.Run')
if (-not $AllowPartial) { $arguments += '-requireAllCharacters' }
Invoke-UnityBatch -UnityPath $batch.Editor -ProjectPath $batch.Project -LogPath (Join-Path $batch.Results 'reference-characters-import.log') -TimeoutSeconds $TimeoutSeconds -Arguments $arguments
if ($BuildPlayer -or $CapturePlayer) {
    Invoke-UnityBatch -UnityPath $batch.Editor -ProjectPath $batch.Project -LogPath (Join-Path $batch.Results 'reference-characters-build.log') -TimeoutSeconds $TimeoutSeconds -Arguments @('-quit','-executeMethod','Emberfield.Editor.ProjectTools.BuildWindows')
}
if ($CapturePlayer) {
    $player = Join-Path $batch.Project 'Builds/Windows/Emberfield.exe'
    $output = Join-Path $batch.Project 'Artifacts/ArtReview/reference-characters/player'
    [void](New-Item -ItemType Directory -Path $output -Force)
    $log = Join-Path $output 'player.log'
    $args = @('-batchmode','-force-d3d11','-screen-fullscreen','0','-screen-width','1600','-screen-height','900','-logFile',$log,'-characterCollectionReview',$output)
    $line = ($args | ForEach-Object { ConvertTo-UnityProcessArgument $_ }) -join ' '
    $process = Start-Process -FilePath $player -ArgumentList $line -WorkingDirectory $batch.Project -WindowStyle Hidden -PassThru
    try {
        $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
        while (-not $process.WaitForExit(1000)) {
            if ([DateTime]::UtcNow -gt $deadline) { $process.Kill(); throw "Character review timed out. Inspect $log" }
        }
        if ($process.ExitCode -ne 0) { throw "Character review failed. Inspect $log" }
        $report = Get-Content -LiteralPath (Join-Path $output 'collection-review.json') -Raw | ConvertFrom-Json
        if (-not $report.passed -or @($report.characters).Count -ne 26) { throw 'The complete native character review did not pass.' }
    } finally { $process.Dispose() }
}
Write-Host 'Reference character import and requested checks completed.'
