param(
    [Parameter(Mandatory = $true)][string]$SnapshotPath,
    [Parameter(Mandatory = $true)][string]$OutputPath,
    [string]$UnityPath = 'D:\Unity\Editors\6000.3.23f1\Editor\Unity.exe',
    [string]$BundleId = 'com.emberfield.jpedrero',
    [string]$Version = '0.3.1',
    [int]$BuildNumber = 2,
    [string]$ServerUrl = '',
    [int]$TimeoutSeconds = 7200
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'Unity-Batch.ps1')
$snapshot = (Resolve-Path -LiteralPath $SnapshotPath).Path
if (-not (Test-Path -LiteralPath (Join-Path $snapshot '.emberfield-ios-package'))) {
    throw 'Use tools/package_ios.py prepare first. Only isolated snapshots can be exported.'
}
if (@(Get-Process Unity -ErrorAction SilentlyContinue).Count -gt 0) {
    throw 'Close the existing Unity editor before exporting: only one Unity may run at once.'
}
if (-not [IO.Path]::IsPathRooted($OutputPath)) { throw 'OutputPath must be absolute.' }
$output = [IO.Path]::GetFullPath($OutputPath)
if ((Test-Path -LiteralPath $output) -and @(Get-ChildItem -LiteralPath $output -Force).Count -gt 0) {
    throw 'OutputPath must be a new or empty directory.'
}
$log = Join-Path (Split-Path -Parent $snapshot) 'unity-ios-export.log'
$values = @{
    EMBERFIELD_IOS_OUTPUT = $output
    EMBERFIELD_BUNDLE_ID = $BundleId
    EMBERFIELD_IOS_VERSION = $Version
    EMBERFIELD_IOS_BUILD_NUMBER = $BuildNumber.ToString([Globalization.CultureInfo]::InvariantCulture)
    EMBERFIELD_SERVER_URL = $ServerUrl
}
$previous = @{}
try {
    foreach ($entry in $values.GetEnumerator()) {
        $previous[$entry.Key] = [Environment]::GetEnvironmentVariable($entry.Key, 'Process')
        [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process')
    }
    Invoke-UnityBatch -UnityPath $UnityPath -ProjectPath $snapshot -LogPath $log `
        -Arguments @('-quit', '-nographics', '-buildTarget', 'iOS', '-executeMethod', 'Emberfield.Editor.IosBuildTools.Export') `
        -TimeoutSeconds $TimeoutSeconds
    if (-not (Test-Path -LiteralPath (Join-Path $output 'Unity-iPhone.xcodeproj/project.pbxproj'))) {
        throw "Unity did not produce an Xcode project. Inspect $log."
    }
    Write-Host "Xcode project exported: $output"
}
finally {
    foreach ($entry in $previous.GetEnumerator()) {
        [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process')
    }
}
