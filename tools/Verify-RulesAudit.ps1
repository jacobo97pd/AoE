# Rules audit harness: compiles the Unity-free simulation, the scripted player and
# tools/RulesAudit.cs with Unity's Roslyn, then runs it from the project root.
#   ./tools/Verify-RulesAudit.ps1 --probes
#   ./tools/Verify-RulesAudit.ps1 --match --map sapphire_coast,amber_crossing --faction aven,serevin --verbose
[CmdletBinding(PositionalBinding = $false)]
param([string]$UnityPath = 'D:\Unity\Editors\6000.3.23f1\Editor\Unity.exe', [string]$OutputDirectory = 'TestResults\RulesAudit\Harness',
    [Parameter(ValueFromRemainingArguments = $true)][object[]]$HarnessArgs)
$ErrorActionPreference = 'Stop'
# PowerShell binds "--map a,b,c" as a nested array; the harness expects one comma-separated value.
$HarnessArgs = @($HarnessArgs | ForEach-Object { if ($_ -is [array]) { $_ -join ',' } else { [string]$_ } })
$project = Split-Path -Parent $PSScriptRoot
$editorData = Join-Path (Split-Path -Parent $UnityPath) 'Data'
$compiler = Join-Path $editorData 'DotNetSdkRoslyn\csc.dll'
$compilerHost = Join-Path $editorData 'NetCoreRuntime\dotnet.exe'
$runtimeLine = @(& dotnet --list-runtimes | Where-Object { $_ -match '^Microsoft.NETCore.App 8\.' })[-1]
if ($runtimeLine -notmatch '^Microsoft.NETCore.App ([\d.]+) \[(.+)\]') { throw '.NET 8 runtime is required.' }
$runtime = Join-Path $Matches[2] $Matches[1]
# A separate -OutputDirectory lets a new build run while long samples still hold the previous one.
$binary = Join-Path $project $OutputDirectory
New-Item -ItemType Directory -Path $binary -Force | Out-Null
$response = @('/nologo', '/target:exe', '/langversion:9', '/optimize+', '/warn:4', '/nowarn:0649', '/nostdlib+', ('/out:"' + (Join-Path $binary 'RulesAudit.dll') + '"'))
$response += Get-ChildItem -LiteralPath $runtime -Filter '*.dll' | ForEach-Object {
    try { [Reflection.AssemblyName]::GetAssemblyName($_.FullName) | Out-Null; '/reference:"' + $_.FullName + '"' } catch [BadImageFormatException] { }
}
$response += Get-ChildItem -LiteralPath (Join-Path $project 'Assets\Game\Simulation') -Filter '*.cs' | ForEach-Object { '"' + $_.FullName + '"' }
$response += '"' + (Join-Path $project 'Assets\Game\Diagnostics\ScriptedCommander.cs') + '"'
$response += '"' + (Join-Path $project 'tools\RulesAudit.cs') + '"'
[IO.File]::WriteAllLines((Join-Path $binary 'compile.rsp'), $response, [Text.UTF8Encoding]::new($false))
& $compilerHost $compiler ('@' + (Join-Path $binary 'compile.rsp'))
if ($LASTEXITCODE -ne 0) { throw 'Rules audit compilation failed.' }
[IO.File]::WriteAllText((Join-Path $binary 'RulesAudit.runtimeconfig.json'), '{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"8.0.0"},"rollForward":"LatestPatch"}}')
New-Item -ItemType Directory -Path (Join-Path $project 'TestResults\RulesAudit') -Force | Out-Null
Push-Location $project
try { & dotnet (Join-Path $binary 'RulesAudit.dll') @HarnessArgs; if ($LASTEXITCODE -ne 0) { throw 'Rules audit failed.' } } finally { Pop-Location }
