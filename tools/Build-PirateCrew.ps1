[CmdletBinding()]
param([string]$UnityPath='D:\Unity\Editors\6000.3.23f1\Editor\Unity.exe',[switch]$Player,[switch]$PlayerOnly,[switch]$SceneOnly,[switch]$CaptureOnly,[int]$TimeoutSeconds=1800,[string]$ScratchDirectory='D:\EmberfieldWorkingCache\AoE')
$ErrorActionPreference='Stop'
if($CaptureOnly -and ($Player -or $PlayerOnly -or $SceneOnly)){throw '-CaptureOnly cannot be combined with player or scene rebuild switches.'}
. (Join-Path $PSScriptRoot 'Unity-Batch.ps1')
$premiumProject=(Resolve-Path -LiteralPath (Split-Path -Parent $PSScriptRoot)).Path
Assert-UnityProjectAvailable -ProjectPath $premiumProject
# Unity recreates Project/Temp at startup. Use a native D: project shell so large
# sharedassets staging files never fall back to the nearly full system volume.
# Assets and the existing import cache are shared only with this same Unity version,
# sequentially. ProjectSettings are copied so the review cannot replace game settings.
$premiumExecutionProject=$premiumProject
if($ScratchDirectory){
    $premiumExecutionProject=[IO.Path]::GetFullPath((Join-Path $ScratchDirectory 'PirateCrewBuildProject'))
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
$premiumMethod=if($CaptureOnly){'Emberfield.Editor.PirateCrewBaker.CaptureOnly'}else{'Emberfield.Editor.PirateCrewBaker.Run'}
$premiumMarker=if($CaptureOnly){'PIRATE_CREW_CAPTURE_OK'}else{'PIRATE_CREW_OK'}
$premiumArguments=@('-quit','-executeMethod',$premiumMethod)
if($Player -or $PlayerOnly){$premiumArguments+='-pirateCrewPlayer'}
if($PlayerOnly){$premiumArguments+='-pirateCrewPlayerOnly'}
if($SceneOnly){$premiumArguments+='-pirateCrewSceneOnly'}
try{Invoke-UnityBatch -UnityPath $premiumPaths.Editor -ProjectPath $premiumPaths.Project -LogPath (Join-Path $premiumPaths.Results 'pirate-crew.log') -TimeoutSeconds $TimeoutSeconds -Arguments $premiumArguments}
finally{
    # Leave the workspace with the game's settings again, so later test runs in it see the same
    # render pipeline as the project.
    if($ScratchDirectory){Get-ChildItem -LiteralPath (Join-Path $premiumProject 'ProjectSettings') -Force | Copy-Item -Destination $premiumSettings -Recurse -Force}
    # The shared Library must not keep the scratch project's editor type database.
    if($ScratchDirectory){Remove-ForeignEditorTypeDb -ProjectPath $premiumProject -ExecutionProjectPath $premiumExecutionProject}
}
if(-not(Select-String -LiteralPath (Join-Path $premiumPaths.Results 'pirate-crew.log') -SimpleMatch $premiumMarker)){throw 'Pirate crew export did not complete.'}
Write-Host ('Review: '+(Join-Path $premiumProject 'Artifacts/ArtReview/pirate-crew'))
