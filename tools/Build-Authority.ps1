[CmdletBinding()]
param([string]$UnityPath = 'D:\Unity\Editors\6000.3.23f1\Editor\Unity.exe')
$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
$editorData = Join-Path (Split-Path -Parent $UnityPath) 'Data'
$compiler = Join-Path $editorData 'DotNetSdkRoslyn\csc.dll'
$compilerHost = Join-Path $editorData 'NetCoreRuntime\dotnet.exe'
$runtimeHost = (Get-Command dotnet -ErrorAction Stop).Source
$runtimeLine = @(& $runtimeHost --list-runtimes | Where-Object { $_ -match '^Microsoft.NETCore.App 8\.' })[-1]
if (!$runtimeLine -or $runtimeLine -notmatch '^Microsoft.NETCore.App ([\d.]+) \[(.+)\]') { throw '.NET 8 runtime is required. No installation was attempted.' }
$runtimeVersion = $Matches[1]
$runtime = Join-Path $Matches[2] $runtimeVersion
$output = Join-Path $project 'Server\AuthorityWorker\out'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$response = @('/nologo', '/target:exe', '/langversion:9', '/optimize+', '/warn:4', '/nowarn:0649', '/nostdlib+', ('/out:"' + (Join-Path $output 'Emberfield.Authority.dll') + '"'))
$response += Get-ChildItem -LiteralPath $runtime -Filter '*.dll' | ForEach-Object {
    try { [Reflection.AssemblyName]::GetAssemblyName($_.FullName) | Out-Null; '/reference:"' + $_.FullName + '"' }
    catch [BadImageFormatException] { } # Runtime folders also contain native runtime DLLs.
}
$response += @('Assets\Game\Simulation', 'Assets\Game\Networking', 'Server\AuthorityWorker') | ForEach-Object {
    Get-ChildItem -LiteralPath (Join-Path $project $_) -Filter '*.cs' | ForEach-Object { '"' + $_.FullName + '"' }
}
$responsePath = Join-Path $output 'compile.rsp'
[IO.File]::WriteAllLines($responsePath, $response, [Text.UTF8Encoding]::new($false))
& $compilerHost $compiler ('@' + $responsePath)
if ($LASTEXITCODE -ne 0) { throw 'Authority compilation failed.' }
$config = @{ runtimeOptions = @{ tfm = 'net8.0'; framework = @{ name = 'Microsoft.NETCore.App'; version = '8.0.0' }; rollForward = 'LatestPatch' } } | ConvertTo-Json -Depth 4
[IO.File]::WriteAllText((Join-Path $output 'Emberfield.Authority.runtimeconfig.json'), $config, [Text.UTF8Encoding]::new($false))
Write-Host ('Authority built: ' + (Join-Path $output 'Emberfield.Authority.dll'))
