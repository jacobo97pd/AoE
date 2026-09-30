[CmdletBinding()]
param([ValidateRange(60,7200)][int]$Seconds = 1800, [ValidateSet('all','amber_crossing','sapphire_coast','sunscar_basin')][string]$Map = 'all',
    [ValidateSet('all','aven','serevin','miraj','skeld','solar','verdant','ashen','drakeforged')][string]$Faction = 'all',
    [ValidateSet('all','Conquest','Dominion')][string]$Mode = 'all', [switch]$FixturesOnly, [switch]$NaturalOnly,
    [string]$Output = 'TestResults/ExpansionSimulation/expansion-verification.json', [string]$UnityPath = 'D:\Unity\Editors\6000.3.23f1\Editor\Unity.exe')
$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
$editorData = Join-Path (Split-Path -Parent $UnityPath) 'Data'
$compiler = Join-Path $editorData 'DotNetSdkRoslyn\csc.dll'
$compilerHost = Join-Path $editorData 'NetCoreRuntime\dotnet.exe'
$runtimeLine = @(& dotnet --list-runtimes | Where-Object { $_ -match '^Microsoft.NETCore.App 8\.' })[-1]
if ($runtimeLine -notmatch '^Microsoft.NETCore.App ([\d.]+) \[(.+)\]') { throw '.NET 8 runtime is required.' }
$runtime = Join-Path $Matches[2] $Matches[1]
$binary = Join-Path $project 'TestResults\ExpansionSimulation\Verifier'
New-Item -ItemType Directory -Path $binary -Force | Out-Null
$response = @('/nologo', '/target:exe', '/langversion:9', '/optimize+', '/warn:4', '/nowarn:0649', '/nostdlib+', ('/out:"' + (Join-Path $binary 'ExpansionVerifier.dll') + '"'))
$response += Get-ChildItem -LiteralPath $runtime -Filter '*.dll' | ForEach-Object {
    try { [Reflection.AssemblyName]::GetAssemblyName($_.FullName) | Out-Null; '/reference:"' + $_.FullName + '"' } catch [BadImageFormatException] { }
}
$response += Get-ChildItem -LiteralPath (Join-Path $project 'Assets\Game\Simulation') -Filter '*.cs' | ForEach-Object { '"' + $_.FullName + '"' }
$response += '"' + (Join-Path $project 'tools\ExpansionVerification.cs') + '"'
$responsePath = Join-Path $binary 'compile.rsp'
[IO.File]::WriteAllLines($responsePath, $response, [Text.UTF8Encoding]::new($false))
& $compilerHost $compiler ('@' + $responsePath)
if ($LASTEXITCODE -ne 0) { throw 'Expansion verifier compilation failed.' }
[IO.File]::WriteAllText((Join-Path $binary 'ExpansionVerifier.runtimeconfig.json'), '{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"8.0.0"},"rollForward":"LatestPatch"}}')
$arguments = @((Join-Path $binary 'ExpansionVerifier.dll'), '--seconds', $Seconds, '--map', $Map, '--faction', $Faction, '--mode', $Mode, '--output', $Output)
if ($FixturesOnly) { $arguments += '--fixtures-only' }; if ($NaturalOnly) { $arguments += '--natural-only' }
Push-Location $project
try { & dotnet @arguments; if ($LASTEXITCODE -ne 0) { throw 'Expansion verification failed.' } } finally { Pop-Location }
