[CmdletBinding()]
param(
    [string]$StateDirectory = 'D:\CodexTooling\online-validation\alpha',
    [string]$PlayerPath = ''
)
$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($PlayerPath)) { $PlayerPath = Join-Path $project 'Builds/Windows/Emberfield.exe' }
$stateFile = Join-Path $StateDirectory 'state.json'
if (-not (Test-Path -LiteralPath $stateFile)) { throw 'Start the alpha first: ./tools/Start-OnlineAlpha.ps1 -Public' }
$state = Get-Content -LiteralPath $stateFile -Raw | ConvertFrom-Json
if (-not $state.Ready -or $state.StoppedUtc) { throw 'The alpha is stopped. Run ./tools/Start-OnlineAlpha.ps1 -Public first.' }
$address = [Uri]$state.Address
if (-not $address.IsAbsoluteUri -or $address.AbsolutePath -ne '/' -or $address.UserInfo -or $address.Query -or $address.Fragment -or ($address.Scheme -ne 'https' -and -not ($address.Scheme -eq 'http' -and $address.IsLoopback))) {
    throw 'Invalid server address in alpha state.'
}
$health = Invoke-RestMethod -Uri ($state.Address.TrimEnd('/') + '/health') -TimeoutSec 15 -MaximumRedirection 0
if ($health.status -ne 'ready' -or $health.protocolVersion -ne $state.ProtocolVersion -or $health.contentVersion -ne $state.ContentVersion) {
    throw 'The configured alpha endpoint is not ready with the expected version.'
}
$player = (Resolve-Path -LiteralPath $PlayerPath).Path
. (Join-Path $PSScriptRoot 'Unity-Batch.ps1')
$arguments = @('-emberfieldServer',$state.Address,'-screen-fullscreen','0','-screen-width','1600','-screen-height','900')
$line = ($arguments | ForEach-Object { ConvertTo-UnityProcessArgument $_ }) -join ' '
Start-Process -FilePath $player -ArgumentList $line -WorkingDirectory (Split-Path -Parent $player) -WindowStyle Normal
Write-Host "Game opened with server $($state.Address). Open MULTIPLAYER to sign in or create an account."
