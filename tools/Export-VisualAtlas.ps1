[CmdletBinding()]
param([string]$UnityPath='D:\Unity\Editors\6000.3.23f1\Editor\Unity.exe', [int]$TimeoutSeconds=1200)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Unity-Batch.ps1')
$atlasProject=(Resolve-Path -LiteralPath (Split-Path -Parent $PSScriptRoot)).Path
$atlasPaths=Resolve-UnityBatchPaths -UnityPath $UnityPath -ProjectPath $atlasProject
Invoke-UnityBatch -UnityPath $atlasPaths.Editor -ProjectPath $atlasPaths.Project -LogPath (Join-Path $atlasPaths.Results 'visual-atlas.log') -TimeoutSeconds $TimeoutSeconds -Arguments @('-quit','-executeMethod','Emberfield.Editor.VisualAssetAtlas.Run','-atlasOutput',(Join-Path $atlasProject 'docs/art/visual-atlas'))
if (-not (Select-String -LiteralPath (Join-Path $atlasPaths.Results 'visual-atlas.log') -SimpleMatch 'EMBERFIELD_VISUAL_ATLAS_OK')) { throw 'Visual atlas export did not complete.' }
& python (Join-Path $PSScriptRoot 'build-visual-atlas.py') --strict
if ($LASTEXITCODE -ne 0) { throw 'Visual atlas gallery validation failed.' }
Write-Host ('Visual atlas: ' + (Join-Path $atlasProject 'docs/art/visual-atlas/index.html'))
