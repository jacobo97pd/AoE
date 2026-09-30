[CmdletBinding()]
param(
    [ValidateRange(1024, 3840)][int]$Width = 1280,
    [ValidateRange(720, 2160)][int]$Height = 720,
    [ValidateRange(30, 600)][int]$TimeoutSeconds = 600
)
$ErrorActionPreference = 'Stop'
$productProject = (Resolve-Path -LiteralPath (Split-Path -Parent $PSScriptRoot)).Path
. (Join-Path $PSScriptRoot 'Unity-Batch.ps1')
$productOutput = Join-Path $productProject ("TestResults/ExpansionProduct-{0}x{1}-{2}" -f $Width, $Height, [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))
[void](New-Item -ItemType Directory -Path $productOutput -Force)
$productExe = Join-Path $productProject 'Builds/Windows/Emberfield.exe'
$productArguments = @('-screen-fullscreen', '0', '-screen-width', [string]$Width, '-screen-height', [string]$Height, '-logFile', (Join-Path $productOutput 'player.log'), '-emberfieldExpansionSmoke', $productOutput)
$productLine = ($productArguments | ForEach-Object { ConvertTo-UnityProcessArgument $_ }) -join ' '
$productProcess = Start-Process -FilePath $productExe -ArgumentList $productLine -WorkingDirectory $productProject -WindowStyle Hidden -PassThru
try {
    $retainedHandle = $productProcess.Handle
    if (-not $productProcess.WaitForExit($TimeoutSeconds * 1000)) { $productProcess.Kill(); throw 'Expansion walkthrough timed out.' }
    $productProcess.WaitForExit(); $productProcess.Refresh()
    if ($productProcess.ExitCode -ne 0) { throw "Expansion walkthrough exited with $($productProcess.ExitCode). See $productOutput" }
    $report = Get-Content -LiteralPath (Join-Path $productOutput 'expansion-smoke.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    if (-not $report.Passed -or $report.Width -ne $Width -or $report.Height -ne $Height) { throw "Expansion walkthrough failed. See $productOutput" }
    if (Select-String -LiteralPath (Join-Path $productOutput 'player.log') -Pattern 'Exception:|Error:|Shader error') { throw 'Runtime log contains an error.' }
    Write-Host "Expansion walkthrough passed: $($report.VisibleButtonClicks) visible controls; build $($report.BuildGuid); $productOutput"
}
finally { if (-not $productProcess.HasExited) { $productProcess.Kill() }; $productProcess.Dispose() }
