[CmdletBinding()]
param([int]$Width=1920,[int]$Height=1080,[string]$Output='Artifacts/ArtReview',[int]$TimeoutSeconds=120)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Unity-Batch.ps1')
$artProject=(Resolve-Path -LiteralPath (Split-Path -Parent $PSScriptRoot)).Path
$artExecutable=Join-Path $artProject 'Builds/ArtStyleLab/ArtStyleLab.exe'
if(-not(Test-Path -LiteralPath $artExecutable)){throw 'Build the laboratory first: tools/Build-ArtStyleLab.ps1 -Player'}
$artOutput=[IO.Path]::GetFullPath((Join-Path $artProject $Output))
[void](New-Item -ItemType Directory -Path $artOutput -Force)
$artLog=Join-Path $artProject ('TestResults/art-style-player-'+$Width+'x'+$Height+'.log')
$artArgs=@('-screen-fullscreen','0','-screen-width',"$Width",'-screen-height',"$Height",'-artStyleReview',$artOutput,'-logFile',$artLog)
$artLine=($artArgs | ForEach-Object { ConvertTo-UnityProcessArgument $_ }) -join ' '
$artProcess=Start-Process -FilePath $artExecutable -ArgumentList $artLine -WorkingDirectory $artProject -WindowStyle Hidden -PassThru
try {
    $artDeadline=[DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while(-not $artProcess.WaitForExit(1000)) {
        if([DateTime]::UtcNow -gt $artDeadline){$artProcess.Kill();throw "ArtStyleLab review timed out. See $artLog"}
    }
    if($artProcess.ExitCode -ne 0){throw "ArtStyleLab review failed ($($artProcess.ExitCode)). See $artLog and $artOutput/player-review.json"}
    $artReport=Get-Content -LiteralPath (Join-Path $artOutput 'player-review.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    if(-not $artReport.passed -or $artReport.status -ne 'completed'){throw 'Native player did not pass the review checks.'}
    if($artReport.width -ne $Width -or $artReport.height -ne $Height){throw 'Native player did not use the requested review resolution.'}
    Write-Host ("PASS: {0}x{1}; {2} animation states; {3} team samples; {4} controls; {5} screenshots. {6}" -f $Width,$Height,$artReport.animations.Count,$artReport.teams.Count,$artReport.visibleControls,$artReport.screenshots.Count,$artOutput)
}
finally {$artProcess.Dispose()}
