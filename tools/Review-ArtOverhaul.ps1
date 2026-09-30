[CmdletBinding()]
param([ValidateSet('Kingdom','Caribbean','Desert','Fantasy')][string[]]$Biomes=@('Kingdom','Caribbean','Desert','Fantasy'),[int]$Width=1920,[int]$Height=1080,[int]$TimeoutSeconds=180,[string]$Output='Artifacts/ArtReview/overhaul/player')
$ErrorActionPreference='Stop'
$overhaulProject=(Resolve-Path -LiteralPath (Split-Path -Parent $PSScriptRoot)).Path
$overhaulPlayer=Join-Path $overhaulProject 'Builds/ArtOverhaul/ArtOverhaul.exe'
if(-not(Test-Path -LiteralPath $overhaulPlayer -PathType Leaf)){throw 'Build the separate art player first with tools/Build-ArtOverhaul.ps1 -Player.'}
$overhaulOutput=[IO.Path]::GetFullPath((Join-Path $overhaulProject $Output))
$overhaulRoot=$overhaulProject.TrimEnd('\')+'\'
if(-not $overhaulOutput.StartsWith($overhaulRoot,[StringComparison]::OrdinalIgnoreCase)){throw 'Review output must stay within this project.'}
[void](New-Item -ItemType Directory -Path $overhaulOutput -Force)
foreach($overhaulBiome in $Biomes){
    $overhaulLog=Join-Path $overhaulProject ('TestResults/overhaul-player-'+$overhaulBiome.ToLowerInvariant()+'-'+$Width+'x'+$Height+'.log')
    $overhaulArgs=@('-screen-fullscreen','0','-screen-width',"$Width",'-screen-height',"$Height",'-overhaulReview',('"'+$overhaulOutput+'"'),'-artBiome',$overhaulBiome,'-logFile',('"'+$overhaulLog+'"'))
    $overhaulProcess=Start-Process -FilePath $overhaulPlayer -ArgumentList $overhaulArgs -WorkingDirectory $overhaulProject -WindowStyle Hidden -PassThru
    try{
        $overhaulDeadline=[DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
        while(-not $overhaulProcess.WaitForExit(1000)){
            if([DateTime]::UtcNow -ge $overhaulDeadline){$overhaulProcess.Kill();throw "Review timed out for $overhaulBiome; only this script's process was stopped."}
        }
        if($overhaulProcess.ExitCode -ne 0){throw "Review failed for $overhaulBiome. Inspect $overhaulLog"}
        $overhaulReport=Get-Content -LiteralPath (Join-Path $overhaulOutput ($overhaulBiome.ToLowerInvariant()+'-review.json')) -Raw | ConvertFrom-Json
        if(-not $overhaulReport.passed){throw "Review report failed for $overhaulBiome."}
        Write-Host ($overhaulBiome+' review passed at '+$Width+'x'+$Height)
    }finally{$overhaulProcess.Dispose()}
}
