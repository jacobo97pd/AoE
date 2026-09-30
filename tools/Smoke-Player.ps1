[CmdletBinding()]
param(
    [string]$ProjectPath = '',
    [int]$Width = 1440,
    [int]$Height = 1080,
    [ValidatePattern('^[A-Za-z0-9_-]{0,40}$')][string]$EvidenceLabel = '',
    [ValidateSet('Economy', 'Combat', 'Technology', 'Faction', 'Offline', 'ArtReview')][string]$Scenario = 'Economy',
    [ValidateSet('aven', 'serevin')][string]$Faction = 'aven',
    [ValidateSet('Conquest', 'Dominion')][string]$Mode = 'Conquest',
    [ValidateRange(15, 1800)][int]$TimeoutSeconds = 90
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($ProjectPath)) { $ProjectPath = Split-Path -Parent $PSScriptRoot }
. (Join-Path $PSScriptRoot 'Unity-Batch.ps1')
try {
    $project = (Resolve-Path -LiteralPath $ProjectPath).Path
    $player = Join-Path $project 'Builds\Windows\Emberfield.exe'
    if (-not (Test-Path -LiteralPath $player)) { throw 'Build the Windows development player first.' }
    $smokeName = if ($Scenario -eq 'Offline') { "OfflineSmoke-$Faction-$Mode" } elseif ($Scenario -eq 'Faction') { "FactionSmoke-$Faction" } elseif ($Scenario -eq 'Combat') { 'CombatSmoke' } elseif ($Scenario -eq 'Technology') { 'TechnologySmoke' } else { 'PlayerSmoke' }
    $smokeFlag = if ($Scenario -eq 'Offline') { '-emberfieldOfflineSmoke' } elseif ($Scenario -eq 'Faction') { '-emberfieldFactionSmoke' } elseif ($Scenario -eq 'Combat') { '-emberfieldCombatSmoke' } elseif ($Scenario -eq 'Technology') { '-emberfieldTechnologySmoke' } else { '-emberfieldSmoke' }
    if ($Scenario -eq 'ArtReview') { $smokeName = 'ArtReview'; $smokeFlag = '-emberfieldArtReview' }
    $evidencePrefix = if ([string]::IsNullOrEmpty($EvidenceLabel)) { '' } else { $EvidenceLabel + '-' }
    $output = Join-Path $project "TestResults\${evidencePrefix}${smokeName}-${Width}x${Height}"
    [void](New-Item -ItemType Directory -Path $output -Force)
    foreach ($name in @('smoke.txt', 'greybox.png', 'match-menu.png', 'match-start.png', 'match-economy.png', 'match-battle.png', 'match-objective.png', 'combat-finish.png', 'research-kingdom.png', 'archive-research.png', 'research-empire-top.png', 'empire-units.png', 'faction-chooser.png', 'charter-changing.png', 'unique-action.png', 'faction-mechanic.png', 'outpost-packing.png', 'outpost-deployed.png', 'faction-research.png', 'player.log')) {
        $stale = Join-Path $output $name
        if (Test-Path -LiteralPath $stale) { Remove-Item -LiteralPath $stale -Force }
    }
    $arguments = @('-batchmode', '-force-d3d11', '-screen-fullscreen', '0', '-screen-width', [string]$Width, '-screen-height', [string]$Height, '-logFile', (Join-Path $output 'player.log'), $smokeFlag, $output)
    if ($Scenario -eq 'Faction') { $arguments += @('-emberfieldFaction', $Faction) }
    if ($Scenario -eq 'Offline') { $arguments += @('-emberfieldFaction', $Faction, '-emberfieldOffline', $Mode) }
    $argumentLine = ($arguments | ForEach-Object { ConvertTo-UnityProcessArgument $_ }) -join ' '
    $process = Start-Process -FilePath $player -ArgumentList $argumentLine -WorkingDirectory $project -WindowStyle Hidden -PassThru
    try {
        $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
        while (-not $process.WaitForExit(1000)) {
            if ([DateTime]::UtcNow -ge $deadline) {
                $process.Kill()
                throw "Player smoke timed out. Inspect $output\player.log."
            }
        }
        if ($process.ExitCode -ne 0) { throw "Player exited $($process.ExitCode). Inspect $output\player.log." }
    }
    finally { $process.Dispose() }
    $reportPath = Join-Path $output 'smoke.txt'
    if (-not (Test-Path -LiteralPath $reportPath)) { throw 'Player produced no smoke report.' }
    if ((Get-Content -LiteralPath $reportPath -Raw) -notmatch 'Passed: True') { throw 'Player smoke failed.' }
    if (-not (Test-Path -LiteralPath (Join-Path $output 'greybox.png'))) { throw 'Player produced no screenshot.' }
    $failures = Select-String -LiteralPath (Join-Path $output 'player.log') -Pattern 'Exception:|Error:|Shader error'
    if ($failures) { throw "Player log contains runtime errors. Inspect $output\player.log." }
    Get-Content -LiteralPath $reportPath
    Write-Host "Screenshot: $output\greybox.png"
    exit 0
}
catch {
    Write-Error -Message $_.Exception.Message -ErrorAction Continue
    exit 1
}
