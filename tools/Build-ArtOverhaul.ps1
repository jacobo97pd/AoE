[CmdletBinding()]
param([string]$UnityPath='D:\Unity\Editors\6000.3.23f1\Editor\Unity.exe', [switch]$Player, [switch]$PlayerOnly, [switch]$KingdomOnly, [int]$TimeoutSeconds=1800)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Unity-Batch.ps1')
$overhaulProject=(Resolve-Path -LiteralPath (Split-Path -Parent $PSScriptRoot)).Path
$overhaulPaths=Resolve-UnityBatchPaths -UnityPath $UnityPath -ProjectPath $overhaulProject
$overhaulArguments=@('-quit','-executeMethod','Emberfield.Editor.ArtOverhaulBaker.Run')
if($Player -or $PlayerOnly){$overhaulArguments+='-overhaulPlayer'}
if($PlayerOnly){$overhaulArguments+='-overhaulPlayerOnly'}
if($KingdomOnly){$overhaulArguments+='-overhaulKingdomOnly'}
Invoke-UnityBatch -UnityPath $overhaulPaths.Editor -ProjectPath $overhaulPaths.Project -LogPath (Join-Path $overhaulPaths.Results 'art-overhaul.log') -TimeoutSeconds $TimeoutSeconds -Arguments $overhaulArguments
if(-not(Select-String -LiteralPath (Join-Path $overhaulPaths.Results 'art-overhaul.log') -SimpleMatch 'ART_OVERHAUL_OK')){throw 'Art overhaul export did not complete.'}
Write-Host ('Review: '+(Join-Path $overhaulProject 'Artifacts/ArtReview/overhaul'))
