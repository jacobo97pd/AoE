[CmdletBinding()]
param(
    [string]$UnityPath = 'D:\Unity\Editors\6000.3.23f1\Editor\Unity.exe',
    [string]$ProjectPath = ''
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($ProjectPath)) { $ProjectPath = Split-Path -Parent $PSScriptRoot }
$project = (Resolve-Path -LiteralPath $ProjectPath).Path
$editor = (Resolve-Path -LiteralPath $UnityPath).Path
$module = Join-Path (Split-Path -Parent $editor) 'Data\PlaybackEngines\AndroidPlayer'
$settings = Get-Content -LiteralPath (Join-Path $project 'ProjectSettings\ProjectSettings.asset') -Raw
$hasModule = Test-Path -LiteralPath $module
$sdk = Join-Path $module 'SDK'
$ndk = Join-Path $module 'NDK'
$jdk = Join-Path $module 'OpenJDK'
$adb = Join-Path $sdk 'platform-tools\adb.exe'
$devices = @()
if (Test-Path -LiteralPath $adb) { $devices = @(& $adb devices -l | Where-Object { $_ -match '\sdevice\s' }) }
$blockers = [Collections.Generic.List[string]]::new()
if (-not $hasModule) { $blockers.Add('Matching Unity Android Build Support module is not installed.') }
if (-not (Test-Path -LiteralPath $sdk)) { $blockers.Add('Bundled Android SDK is not installed.') }
if (-not (Test-Path -LiteralPath $ndk)) { $blockers.Add('Bundled Android NDK is not installed.') }
if (-not (Test-Path -LiteralPath $jdk)) { $blockers.Add('Bundled OpenJDK is not installed.') }
if ($devices.Count -eq 0) { $blockers.Add('No ready Android device was detected through the bundled adb.') }
$report = [ordered]@{
    Utc = [DateTime]::UtcNow.ToString('O')
    Editor = $editor
    Project = $project
    ModuleInstalled = $hasModule
    BundledSdkPresent = (Test-Path -LiteralPath $sdk)
    BundledNdkPresent = (Test-Path -LiteralPath $ndk)
    BundledJdkPresent = (Test-Path -LiteralPath $jdk)
    Arm64Configured = ($settings -match 'AndroidTargetArchitectures:\s*2')
    MinimumApi26Configured = ($settings -match 'AndroidMinSdkVersion:\s*26')
    ApplicationIdConfigured = ($settings -match 'com\.emberfield\.prototype')
    ConnectedDevices = $devices
    CanAttemptBundledDeviceBuild = ($hasModule -and (Test-Path -LiteralPath $sdk) -and (Test-Path -LiteralPath $ndk) -and (Test-Path -LiteralPath $jdk))
    Blockers = $blockers.ToArray()
    ChangedInstallations = $false
}
$results = Join-Path $project 'TestResults'
[void](New-Item -ItemType Directory -Path $results -Force)
$json = $report | ConvertTo-Json -Depth 5
[IO.File]::WriteAllText((Join-Path $results 'android-readiness.json'), $json + [char]10, [Text.UTF8Encoding]::new($false))
$json
