[CmdletBinding()]
param(
    [ValidateRange(1024, 3840)][int]$Width = 1280,
    [ValidateRange(720, 2160)][int]$Height = 720,
    [ValidateRange(30, 600)][int]$TimeoutSeconds = 180,
    # Development-player flags for A/B runs of the same build, e.g. -emberfieldFrameRate 60 -emberfieldNoPrewarm.
    [string[]]$ExtraArguments = @(),
    # A visible window, for pacing: a hidden one has no display refresh, so vertical sync cannot pace it.
    [switch]$Visible
)
$ErrorActionPreference = 'Stop'
$productProject = (Resolve-Path -LiteralPath (Split-Path -Parent $PSScriptRoot)).Path
. (Join-Path $PSScriptRoot 'Unity-Batch.ps1')
$productOutput = Join-Path $productProject ("TestResults/AlphaProduct-{0}x{1}-{2}" -f $Width, $Height, [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))
[void](New-Item -ItemType Directory -Path $productOutput -Force)
$productExe = Join-Path $productProject 'Builds/Windows/Emberfield.exe'
# A hidden window has no display refresh for vertical sync (the shipped default) to wait on, so unless the caller
# chose otherwise it runs at the 60 fps cap instead of as fast as it can.
if (-not $Visible -and $ExtraArguments -notcontains '-emberfieldFrameRate') { $ExtraArguments += @('-emberfieldFrameRate', '60') }
$productArguments = @('-screen-fullscreen', '0', '-screen-width', [string]$Width, '-screen-height', [string]$Height, '-logFile', (Join-Path $productOutput 'player.log'), '-emberfieldProductSmoke', $productOutput) + $ExtraArguments
$productLine = ($productArguments | ForEach-Object { ConvertTo-UnityProcessArgument $_ }) -join ' '
$productProcess = Start-Process -FilePath $productExe -ArgumentList $productLine -WorkingDirectory $productProject -WindowStyle $(if ($Visible) { 'Normal' } else { 'Hidden' }) -PassThru
try {
    $retainedHandle = $productProcess.Handle
    if (-not $productProcess.WaitForExit($TimeoutSeconds * 1000)) { $productProcess.Kill(); throw 'Product walkthrough timed out.' }
    $productProcess.WaitForExit(); $productProcess.Refresh()
    if ($productProcess.ExitCode -ne 0) { throw "Product walkthrough exited with $($productProcess.ExitCode). See $productOutput" }
    $report = Get-Content -LiteralPath (Join-Path $productOutput 'product-smoke.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    if (-not $report.Passed -or $report.Width -ne $Width -or $report.Height -ne $Height) { throw "Product walkthrough failed. See $productOutput" }
    if (Select-String -LiteralPath (Join-Path $productOutput 'player.log') -Pattern 'Exception:|Error:|Shader error') { throw 'Runtime log contains an error.' }
    Write-Host "Product walkthrough passed: $($report.VisibleButtonClicks) visible controls; build $($report.BuildGuid); $productOutput"
}
finally { if (-not $productProcess.HasExited) { $productProcess.Kill() }; $productProcess.Dispose() }
