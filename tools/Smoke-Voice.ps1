[CmdletBinding()]
param(
    [ValidateRange(1024, 3840)][int]$Width = 1280,
    [ValidateRange(720, 2160)][int]$Height = 720,
    [ValidateRange(30, 600)][int]$TimeoutSeconds = 240,
    [string]$PlayerPath = ''
)
# Packaged voice-command acceptance on a development player. It never opens the microphone:
# Windows compiles both phrase lists, then scripted phrases travel the recognizer path.
$ErrorActionPreference = 'Stop'
$voiceProject = (Resolve-Path -LiteralPath (Split-Path -Parent $PSScriptRoot)).Path
. (Join-Path $PSScriptRoot 'Unity-Batch.ps1')
if ([string]::IsNullOrWhiteSpace($PlayerPath)) { $PlayerPath = Join-Path $voiceProject 'Builds/PirateCrew/PirateCrew.exe' }
if (-not (Test-Path -LiteralPath $PlayerPath -PathType Leaf)) { throw "Development player not found: $PlayerPath. Build it with tools/Build-PirateCrew.ps1 -PlayerOnly." }
$voiceOutput = Join-Path $voiceProject ("TestResults/Voice-{0}x{1}-{2}" -f $Width, $Height, [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))
[void](New-Item -ItemType Directory -Path $voiceOutput -Force)
$voiceArguments = @('-screen-fullscreen', '0', '-screen-width', [string]$Width, '-screen-height', [string]$Height,
    '-logFile', (Join-Path $voiceOutput 'player.log'), '-emberfieldVoiceSmoke', $voiceOutput, '-emberfieldOffline', 'Conquest')
$voiceLine = ($voiceArguments | ForEach-Object { ConvertTo-UnityProcessArgument $_ }) -join ' '
$voiceProcess = Start-Process -FilePath $PlayerPath -ArgumentList $voiceLine -WorkingDirectory $voiceProject -WindowStyle Hidden -PassThru
try {
    $retainedHandle = $voiceProcess.Handle
    if (-not $voiceProcess.WaitForExit($TimeoutSeconds * 1000)) { $voiceProcess.Kill(); throw "Voice smoke timed out. See $voiceOutput" }
    $voiceProcess.WaitForExit(); $voiceProcess.Refresh()
    $reportPath = Join-Path $voiceOutput 'voice-smoke.json'
    if (-not (Test-Path -LiteralPath $reportPath)) { throw "Voice smoke wrote no report (exit $($voiceProcess.ExitCode)). See $voiceOutput" }
    $report = Get-Content -LiteralPath $reportPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($voiceProcess.ExitCode -ne 0 -or -not $report.Passed) { throw "Voice smoke failed: $($report.Failure) See $voiceOutput" }
    if (Select-String -LiteralPath (Join-Path $voiceOutput 'player.log') -Pattern 'Exception:|Error:|Shader error') { throw "Runtime log contains an error. See $voiceOutput" }
    Write-Host ("Voice smoke passed: {0} commands; Windows speech {1}; phrases es {2} ({3}), en {4} ({5}); build {6}; {7}" -f
        $report.CommandsCarriedOut, $report.WindowsSpeechSupported, $report.SpanishPhrases, $report.SpanishListAccepted,
        $report.EnglishPhrases, $report.EnglishListAccepted, $report.BuildGuid, $voiceOutput)
}
finally { if (-not $voiceProcess.HasExited) { $voiceProcess.Kill() }; $voiceProcess.Dispose() }
