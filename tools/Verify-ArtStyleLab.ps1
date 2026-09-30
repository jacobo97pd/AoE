[CmdletBinding()]
param([string]$UnityPath='D:\Unity\Editors\6000.3.23f1\Editor\Unity.exe', [int]$TimeoutSeconds=1200)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Unity-Batch.ps1')
$artProject=(Resolve-Path -LiteralPath (Split-Path -Parent $PSScriptRoot)).Path
$artPaths=Resolve-UnityBatchPaths -UnityPath $UnityPath -ProjectPath $artProject
$artResult=Join-Path $artPaths.Results 'art-style-tests.xml'
if(Test-Path -LiteralPath $artResult){Remove-Item -LiteralPath $artResult}
Invoke-UnityBatch -UnityPath $artPaths.Editor -ProjectPath $artPaths.Project -LogPath (Join-Path $artPaths.Results 'art-style-tests.log') -TimeoutSeconds $TimeoutSeconds -Arguments @('-runTests','-testPlatform','EditMode','-testFilter','Emberfield.Tests.ArtStyleLab','-testResults',$artResult)
Assert-UnityTestResult -ResultPath $artResult
