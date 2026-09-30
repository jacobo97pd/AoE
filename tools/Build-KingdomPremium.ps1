[CmdletBinding()]
param([string]$UnityPath='D:\Unity\Editors\6000.3.23f1\Editor\Unity.exe',[switch]$Player,[switch]$PlayerOnly,[switch]$SceneOnly,[int]$TimeoutSeconds=1800,[string]$ScratchDirectory='D:\EmberfieldWorkingCache\AoE')
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Unity-Batch.ps1')
$premiumProject=(Resolve-Path -LiteralPath (Split-Path -Parent $PSScriptRoot)).Path
Assert-UnityProjectAvailable -ProjectPath $premiumProject
# Unity recreates Project/Temp at startup. Use a native D: project shell so large
# sharedassets staging files never fall back to the nearly full system volume.
# Assets and the existing import cache are shared only with this same Unity version,
# sequentially. ProjectSettings are copied so the review cannot replace game settings.
$premiumExecutionProject=$premiumProject
if($ScratchDirectory){
    $premiumExecutionProject=[IO.Path]::GetFullPath((Join-Path $ScratchDirectory 'KingdomBuildProject'))
    Assert-UnityProjectAvailable -ProjectPath $premiumExecutionProject
    [void](New-Item -ItemType Directory -Path $premiumExecutionProject -Force)
    foreach($premiumFolder in @('Assets','Packages','Library','Artifacts','Builds','TestResults')){
        $premiumLink=Join-Path $premiumExecutionProject $premiumFolder
        $premiumExpectedTarget=[IO.Path]::GetFullPath((Join-Path $premiumProject $premiumFolder))
        if(Test-Path -LiteralPath $premiumLink){
            $premiumExisting=Get-Item -LiteralPath $premiumLink -Force
            if($premiumExisting.LinkType -ne 'Junction' -or @($premiumExisting.Target).Count -ne 1 -or
                -not [IO.Path]::GetFullPath($premiumExisting.Target[0]).Equals($premiumExpectedTarget,[StringComparison]::OrdinalIgnoreCase)){
                throw "The build workspace belongs to another checkout: $premiumLink. Use a different -ScratchDirectory."
            }
        }else{
            [void](New-Item -ItemType Junction -Path $premiumLink -Target $premiumExpectedTarget)
        }
    }
    $premiumSettings=Join-Path $premiumExecutionProject 'ProjectSettings'
    [void](New-Item -ItemType Directory -Path $premiumSettings -Force)
    Get-ChildItem -LiteralPath (Join-Path $premiumProject 'ProjectSettings') -Force | Copy-Item -Destination $premiumSettings -Recurse -Force
}
$premiumPaths=Resolve-UnityBatchPaths -UnityPath $UnityPath -ProjectPath $premiumExecutionProject
$premiumArguments=@('-quit','-executeMethod','Emberfield.Editor.KingdomPremiumBaker.Run')
if($Player -or $PlayerOnly){$premiumArguments+='-premiumPlayer'}
if($PlayerOnly){$premiumArguments+='-premiumPlayerOnly'}
if($SceneOnly){$premiumArguments+='-premiumSceneOnly'}
try{Invoke-UnityBatch -UnityPath $premiumPaths.Editor -ProjectPath $premiumPaths.Project -LogPath (Join-Path $premiumPaths.Results 'kingdom-premium.log') -TimeoutSeconds $TimeoutSeconds -Arguments $premiumArguments}
# The shared Library must not keep the scratch project's editor type database.
finally{if($ScratchDirectory){Remove-ForeignEditorTypeDb -ProjectPath $premiumProject -ExecutionProjectPath $premiumExecutionProject}}
if(-not(Select-String -LiteralPath (Join-Path $premiumPaths.Results 'kingdom-premium.log') -SimpleMatch 'KINGDOM_PREMIUM_OK')){throw 'Kingdom premium export did not complete.'}
Write-Host ('Review: '+(Join-Path $premiumProject 'Artifacts/ArtReview/kingdom-premium'))
