[CmdletBinding()]
param(
    [ValidateSet('Windows', 'Android')]
    [string]$Target = 'Windows',
    [string]$UnityPath = 'D:\Unity\Editors\6000.3.23f1\Editor\Unity.exe',
    [string]$ProjectPath = '',
    [ValidateRange(30, 7200)]
    [int]$TimeoutSeconds = 1800
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($ProjectPath)) { $ProjectPath = Split-Path -Parent $PSScriptRoot }
. (Join-Path $PSScriptRoot 'Unity-Batch.ps1')

try {
    $batchPaths = Resolve-UnityBatchPaths -UnityPath $UnityPath -ProjectPath $ProjectPath
    $buildMethod = "Emberfield.Editor.ProjectTools.Build$Target"
    $buildTarget = if ($Target -eq 'Windows') { 'Win64' } else { 'Android' }
    # The editor entry point validates support, creates the local output path and
    # throws on a failed BuildReport. Never install a platform module implicitly.
    Invoke-UnityBatch -UnityPath $batchPaths.Editor -ProjectPath $batchPaths.Project -LogPath (Join-Path $batchPaths.Results "build-$Target.log") -TimeoutSeconds $TimeoutSeconds -Arguments @('-nographics', '-quit', '-buildTarget', $buildTarget, '-executeMethod', $buildMethod)
    Write-Host "Unity $Target build entry point completed. Inspect the build log for artifact details."
    exit 0
}
catch {
    Write-Error -Message $_.Exception.Message -ErrorAction Continue
    exit 1
}
finally {
    # A build from a shell project that shares this Library (MobileTierBuildProject) leaves the shell's editor type
    # database behind, and the project's own editor then loads every script as "(Unknown) missing".
    if ($batchPaths) { Remove-ForeignEditorTypeDb -ProjectPath (Split-Path -Parent $PSScriptRoot) -ExecutionProjectPath $batchPaths.Project }
}
