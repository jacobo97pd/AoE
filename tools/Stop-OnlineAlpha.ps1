[CmdletBinding()]
param([string]$StateDirectory = 'D:/CodexTooling/online-validation/alpha')
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'OnlineAlpha-Common.ps1')
$stateRoot = [IO.Path]::GetFullPath($StateDirectory)
$statePath = Join-Path $stateRoot 'state.json'
if (-not (Test-Path -LiteralPath $statePath -PathType Leaf)) { Write-Host 'No managed alpha state exists. No processes were touched.'; return }
$controlLock = [IO.File]::Open((Join-Path $stateRoot 'control.lock'), [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
try {
    $state = Get-Content -LiteralPath $statePath -Raw -Encoding UTF8 | ConvertFrom-Json
    $workers = @($state.Authorities) + @(Get-OnlineWorkers $state.Service $state.AuthorityPath)
    $checks = @(Stop-OnlineOwnedProcess $state.Tunnel; Stop-OnlineOwnedProcess $state.Service)
    foreach ($worker in @($workers | Sort-Object Pid -Unique)) { $checks += Stop-OnlineOwnedProcess $worker }
    $state.Ready = $false; $state.StoppedUtc = [DateTime]::UtcNow.ToString('o'); $state.StopChecks = $checks
    Write-OnlineState $statePath $state
    $checks | Format-Table Purpose, Pid, Status -AutoSize
    Write-Host 'Owned alpha processes stopped or already absent. Persistent accounts, results and logs were preserved. Active matches are recovered as aborted on the next server start.'
}
finally { $controlLock.Dispose() }
