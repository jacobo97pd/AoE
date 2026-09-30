# Simulation tick harness: compiles the Unity-free simulation and tools/SimulationTicks.cs with
# Unity's Roslyn, then runs fixed scripted workloads from the project root. Each run prints the
# state hash every --hash-every ticks and times every World.Tick; two builds whose hashes match
# are bit-identical tick for tick.
#   ./tools/Verify-SimulationTicks.ps1 --scenario all --json TestResults/SimulationTicks/after.json
#   ./tools/Verify-SimulationTicks.ps1 -SimulationSource <copy of Assets/Game/Simulation> -OutputDirectory TestResults/SimulationTicks/Before --scenario battle
[CmdletBinding(PositionalBinding = $false)]
param([string]$UnityPath = 'D:\Unity\Editors\6000.3.23f1\Editor\Unity.exe', [string]$OutputDirectory = 'TestResults\SimulationTicks\Harness',
    [string]$SimulationSource = 'Assets\Game\Simulation', [switch]$CompileOnly,
    [Parameter(ValueFromRemainingArguments = $true)][object[]]$HarnessArgs)
$ErrorActionPreference = 'Stop'
$HarnessArgs = @($HarnessArgs | ForEach-Object { if ($_ -is [array]) { $_ -join ',' } else { [string]$_ } })
$project = Split-Path -Parent $PSScriptRoot
$editorData = Join-Path (Split-Path -Parent $UnityPath) 'Data'
$compiler = Join-Path $editorData 'DotNetSdkRoslyn\csc.dll'
$compilerHost = Join-Path $editorData 'NetCoreRuntime\dotnet.exe'
$runtimeLine = @(& dotnet --list-runtimes | Where-Object { $_ -match '^Microsoft.NETCore.App 8\.' })[-1]
if ($runtimeLine -notmatch '^Microsoft.NETCore.App ([\d.]+) \[(.+)\]') { throw '.NET 8 runtime is required.' }
$runtime = Join-Path $Matches[2] $Matches[1]
$binary = if ([IO.Path]::IsPathRooted($OutputDirectory)) { $OutputDirectory } else { Join-Path $project $OutputDirectory }
$source = if ([IO.Path]::IsPathRooted($SimulationSource)) { $SimulationSource } else { Join-Path $project $SimulationSource }
New-Item -ItemType Directory -Path $binary -Force | Out-Null
$response = @('/nologo', '/target:exe', '/langversion:9', '/optimize+', '/warn:4', '/nowarn:0649', '/nostdlib+', ('/out:"' + (Join-Path $binary 'SimulationTicks.dll') + '"'))
$response += Get-ChildItem -LiteralPath $runtime -Filter '*.dll' | ForEach-Object {
    try { [Reflection.AssemblyName]::GetAssemblyName($_.FullName) | Out-Null; '/reference:"' + $_.FullName + '"' } catch [BadImageFormatException] { }
}
$response += Get-ChildItem -LiteralPath $source -Filter '*.cs' | ForEach-Object { '"' + $_.FullName + '"' }
$response += '"' + (Join-Path $project 'tools\SimulationTicks.cs') + '"'
[IO.File]::WriteAllLines((Join-Path $binary 'compile.rsp'), $response, [Text.UTF8Encoding]::new($false))
& $compilerHost $compiler ('@' + (Join-Path $binary 'compile.rsp'))
if ($LASTEXITCODE -ne 0) { throw 'Simulation tick harness compilation failed.' }
# Fully optimised code from the first call keeps the timings comparable between builds.
[IO.File]::WriteAllText((Join-Path $binary 'SimulationTicks.runtimeconfig.json'), '{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"8.0.0"},"rollForward":"LatestPatch","configProperties":{"System.Runtime.TieredCompilation":false,"System.GC.Concurrent":false}}}')
if ($CompileOnly) { return }
Push-Location $project
try { & dotnet (Join-Path $binary 'SimulationTicks.dll') @HarnessArgs; if ($LASTEXITCODE -ne 0) { throw 'Simulation tick harness failed.' } } finally { Pop-Location }
