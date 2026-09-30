[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'OnlineAlpha-Common.ps1')

# Only inspect this test's own PowerShell process. Never start/stop the alpha or write its state.
$script:identityProbeProcess = Microsoft.PowerShell.Management\Get-Process -Id $PID
$expectedTicks = $script:identityProbeProcess.StartTime.ToUniversalTime().Ticks.ToString()
$captured = Get-OnlineIdentity $script:identityProbeProcess 'identity-test' 'Test-OnlineIdentity.ps1'
if ($captured.Pid -ne $PID -or $captured.StartedUtcTicks -ne $expectedTicks -or
    -not [string]::Equals($captured.Executable, $script:identityProbeProcess.Path, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Actual process identity did not match.'
}
if ($null -eq (Find-OnlineOwnedProcess $captured)) { throw 'The captured test process must pass unchanged ownership checks.' }

$script:identityProbeCalls = 0
$script:identityProbeMode = 'transient'
function Get-Process {
    [CmdletBinding()]
    param([int]$Id)
    if ($Id -ne $script:identityProbeProcess.Id) { throw 'Unexpected PID lookup.' }
    $script:identityProbeCalls++
    if ($script:identityProbeMode -eq 'transient' -and $script:identityProbeCalls -gt 1) { return $script:identityProbeProcess }
    $start = $script:identityProbeProcess.StartTime
    if ($script:identityProbeMode -eq 'reused') { $start = $start.AddSeconds(1) }
    $fake = [pscustomobject]@{ Id = $Id; StartTime = $start; Path = $null }
    $fake | Add-Member -MemberType ScriptMethod -Name Refresh -Value { }
    return $fake
}
try {
    $retried = Get-OnlineIdentity $script:identityProbeProcess 'identity-test' 'Test-OnlineIdentity.ps1'
    if ($script:identityProbeCalls -ne 2 -or $retried.Executable -ne $captured.Executable -or $retried.StartedUtcTicks -ne $expectedTicks) {
        throw 'A transient missing path must reacquire exactly the original process.'
    }
    $script:identityProbeMode = 'reused'
    $rejectedReuse = $false
    try { Get-OnlineIdentity $script:identityProbeProcess 'identity-test' 'Test-OnlineIdentity.ps1' | Out-Null }
    catch { $rejectedReuse = $_.Exception.Message -match 'identity changed' }
    if (-not $rejectedReuse) { throw 'A reused PID must be rejected.' }

    $script:identityProbeMode = 'missing'; $script:identityProbeCalls = 0
    $timer = [Diagnostics.Stopwatch]::StartNew(); $rejectedMissing = $false
    try { Get-OnlineIdentity $script:identityProbeProcess 'identity-test' 'Test-OnlineIdentity.ps1' | Out-Null }
    catch { $rejectedMissing = $_.Exception.Message -match 'executable path remained unavailable' }
    if (-not $rejectedMissing -or $script:identityProbeCalls -lt 2) { throw 'A persistently absent path must time out without returning an identity.' }
    [pscustomobject]@{ Status = 'PASS'; Checks = 4; OwnProcessId = $PID; MissingPathElapsedMs = $timer.ElapsedMilliseconds }
}
finally { Remove-Item -LiteralPath Function:\Get-Process }
