[CmdletBinding()]
param(
    # allocations.raw written by a development player run with -emberfieldAllocationCapture <frames>
    # (Profile-OfflinePlayer.ps1 / Profile-MovementPlayer.ps1 -ExtraArguments -emberfieldAllocationCapture,300).
    [Parameter(Mandatory = $true)][string]$Capture,
    [string]$Report = '',
    [string]$UnityPath = 'D:\Unity\Editors\6000.3.23f1\Editor\Unity.exe',
    [string]$ProjectPath = '',
    [ValidateRange(60, 3600)][int]$TimeoutSeconds = 900
)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($ProjectPath)) { $ProjectPath = Split-Path -Parent $PSScriptRoot }
. (Join-Path $PSScriptRoot 'Unity-Batch.ps1')
try {
    $capturePath = (Resolve-Path -LiteralPath $Capture).Path
    if ([string]::IsNullOrWhiteSpace($Report)) { $Report = [IO.Path]::ChangeExtension($capturePath, '.txt') }
    $reportPath = [IO.Path]::GetFullPath($Report)
    if (Test-Path -LiteralPath $reportPath) { Remove-Item -LiteralPath $reportPath -Force }
    $batchPaths = Resolve-UnityBatchPaths -UnityPath $UnityPath -ProjectPath $ProjectPath
    Invoke-UnityBatch -UnityPath $batchPaths.Editor -ProjectPath $batchPaths.Project -LogPath (Join-Path $batchPaths.Results 'allocation-report.log') -TimeoutSeconds $TimeoutSeconds -Arguments @('-nographics', '-quit', '-executeMethod', 'Emberfield.Editor.AllocationReport.Run', '-allocationCapture', $capturePath, '-allocationReport', $reportPath)
    if (-not (Test-Path -LiteralPath $reportPath)) { throw "No report written. Inspect $(Join-Path $batchPaths.Results 'allocation-report.log')." }
    Get-Content -LiteralPath $reportPath -TotalCount 40 | Write-Host
    Write-Host "Full report: $reportPath"
    exit 0
}
catch { Write-Error -Message $_.Exception.Message -ErrorAction Continue; exit 1 }
