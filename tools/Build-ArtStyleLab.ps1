[CmdletBinding()]
param([string]$UnityPath='D:\Unity\Editors\6000.3.23f1\Editor\Unity.exe', [switch]$Player, [int]$TimeoutSeconds=1200)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Unity-Batch.ps1')
$artProject=(Resolve-Path -LiteralPath (Split-Path -Parent $PSScriptRoot)).Path
$artPaths=Resolve-UnityBatchPaths -UnityPath $UnityPath -ProjectPath $artProject
$artArguments=@('-quit','-executeMethod','Emberfield.Editor.ArtStyleLabBaker.Run')
if($Player){$artArguments+='-artStylePlayer'}
Invoke-UnityBatch -UnityPath $artPaths.Editor -ProjectPath $artPaths.Project -LogPath (Join-Path $artPaths.Results 'art-style-lab.log') -TimeoutSeconds $TimeoutSeconds -Arguments $artArguments
if(-not(Select-String -LiteralPath (Join-Path $artPaths.Results 'art-style-lab.log') -SimpleMatch 'ART_STYLE_LAB_OK')){throw 'Art style laboratory export did not complete.'}
Write-Host ('Review: '+(Join-Path $artProject 'Artifacts/ArtReview/index.html'))
