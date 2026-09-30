[CmdletBinding()]
param(
    [ValidateRange(640,8192)][int]$Width=1920,
    [ValidateRange(480,8192)][int]$Height=1080,
    [ValidateRange(15,900)][int]$TimeoutSeconds=180,
    [string]$Output='Artifacts/ArtReview/kingdom-premium/player',
    [string]$ExpectedBuildGuid=''
)
$ErrorActionPreference='Stop'
$premiumProject=(Resolve-Path -LiteralPath (Split-Path -Parent $PSScriptRoot)).Path
$premiumPlayer=Join-Path $premiumProject 'Builds/KingdomPremium/KingdomPremium.exe'
if(-not(Test-Path -LiteralPath $premiumPlayer -PathType Leaf)){throw 'Build the KingdomPremium player before running this review.'}
$premiumOutput=if([IO.Path]::IsPathRooted($Output)){[IO.Path]::GetFullPath($Output)}else{[IO.Path]::GetFullPath((Join-Path $premiumProject $Output))}
$premiumPrefix=$premiumProject.TrimEnd('\')+'\'
if(-not $premiumOutput.StartsWith($premiumPrefix,[StringComparison]::OrdinalIgnoreCase)){throw 'Review output must be a directory within this project.'}
[void](New-Item -ItemType Directory -Path $premiumOutput -Force)
$premiumLogs=Join-Path $premiumProject 'TestResults'
[void](New-Item -ItemType Directory -Path $premiumLogs -Force)
$premiumLog=Join-Path $premiumLogs ('kingdom-premium-player-'+$Width+'x'+$Height+'.log')
$premiumReportPath=Join-Path $premiumOutput 'review.json'
$premiumStarted=[DateTimeOffset]::UtcNow
$premiumArgs=@('-screen-fullscreen','0','-screen-width',"$Width",'-screen-height',"$Height",'-kingdomPremiumReview',('"'+$premiumOutput+'"'),'-logFile',('"'+$premiumLog+'"'))
$premiumProcess=Start-Process -FilePath $premiumPlayer -ArgumentList $premiumArgs -WorkingDirectory $premiumProject -WindowStyle Hidden -PassThru
try{
    $premiumDeadline=[DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while(-not $premiumProcess.WaitForExit(1000)){
        if([DateTime]::UtcNow -ge $premiumDeadline){
            $premiumProcess.Kill()
            throw "Kingdom review timed out; only this script's process was stopped. Inspect $premiumLog"
        }
    }
    $premiumProcess.WaitForExit()
    $premiumProcess.Refresh()
    if($premiumProcess.ExitCode -ne 0){throw "Kingdom review exited with code $($premiumProcess.ExitCode). Inspect $premiumLog and $premiumReportPath"}
    if(-not(Test-Path -LiteralPath $premiumReportPath -PathType Leaf)){throw 'The player did not write review.json.'}
    $premiumReport=Get-Content -Raw -Encoding UTF8 -LiteralPath $premiumReportPath | ConvertFrom-Json
    if([DateTimeOffset]::Parse($premiumReport.generatedUtc) -lt $premiumStarted.AddSeconds(-1)){throw 'review.json is stale; it predates this launch.'}
    if(-not $premiumReport.passed -or $premiumReport.status -ne 'completed'){throw "Native review failed: $($premiumReport.problems -join '; ')"}
    if($premiumReport.width -ne $Width -or $premiumReport.height -ne $Height){throw 'The reviewed resolution does not match the requested resolution.'}
    if($ExpectedBuildGuid -and $premiumReport.buildGuid -ne $ExpectedBuildGuid){throw 'The player build GUID does not match -ExpectedBuildGuid.'}
    if($premiumReport.units.Count -ne 3){throw 'The report does not contain exactly three characters.'}
    $premiumExpected=@('close','medium','rts','worker','warrior','hero','controls')
    if($premiumReport.views.Count -ne $premiumExpected.Count){throw 'The report does not contain exactly seven captured views.'}
    foreach($premiumViewId in $premiumExpected){
        $premiumView=@($premiumReport.views | Where-Object {$_.id -eq $premiumViewId})
        if($premiumView.Count -ne 1 -or -not $premiumView[0].hasPixels){throw "Missing or empty capture: $premiumViewId"}
        $premiumImage=Join-Path $premiumOutput ($premiumViewId+'.png')
        if(-not(Test-Path -LiteralPath $premiumImage -PathType Leaf)){throw "Missing PNG: $premiumImage"}
        $premiumBytes=[IO.File]::ReadAllBytes($premiumImage)
        if($premiumBytes.Length -lt 24 -or $premiumBytes[0] -ne 137 -or $premiumBytes[1] -ne 80 -or $premiumBytes[2] -ne 78 -or $premiumBytes[3] -ne 71){throw "Invalid PNG: $premiumImage"}
        $premiumPngWidth=([uint32]$premiumBytes[16]*16777216)+([uint32]$premiumBytes[17]*65536)+([uint32]$premiumBytes[18]*256)+[uint32]$premiumBytes[19]
        $premiumPngHeight=([uint32]$premiumBytes[20]*16777216)+([uint32]$premiumBytes[21]*65536)+([uint32]$premiumBytes[22]*256)+[uint32]$premiumBytes[23]
        if($premiumPngWidth -ne $Width -or $premiumPngHeight -ne $Height){throw "PNG resolution mismatch: $premiumImage"}
        if((Get-Item -LiteralPath $premiumImage).LastWriteTimeUtc -lt $premiumStarted.UtcDateTime.AddSeconds(-1)){throw "Stale PNG: $premiumImage"}
    }
    $premiumRts=$premiumReport.views | Where-Object {$_.id -eq 'rts'}
    $premiumHud=$premiumReport.views | Where-Object {$_.id -eq 'controls'}
    if($premiumRts.unitsOnScreen -ne 3 -or $premiumRts.completeUnits -ne 3){throw 'RTS does not frame all three characters.'}
    if(-not $premiumHud.hudFramingApplied -or $premiumHud.boundsInFreeArea -ne 3 -or -not $premiumReport.controlsFit -or -not $premiumReport.controlsBlockWorldInput){throw 'The controls overlap characters or do not fit/intercept input.'}
    Write-Host ('Kingdom review passed: '+$Width+'x'+$Height+', 3 characters, 7 native PNGs, build '+$premiumReport.buildGuid)
    Write-Host ('Report: '+$premiumReportPath)
}finally{
    $premiumProcess.Dispose()
}
