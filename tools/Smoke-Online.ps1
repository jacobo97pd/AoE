[CmdletBinding()]
param(
    [ValidateRange(640, 3840)][int]$Width = 1280,
    [ValidateRange(480, 2160)][int]$Height = 720,
    [ValidateRange(60, 360)][int]$TimeoutSeconds = 240,
    [switch]$NavalSlice,
    [ValidateSet('historical', 'fantasy', 'naval')][string]$Realm = 'historical',
    [ValidateSet('', 'amber_crossing', 'sapphire_coast', 'sunscar_basin', 'legend_lands')][string]$MapId = '',
    [string]$ServerUrl = '',
    [string]$PlayerPath = '',
    [string]$OutputDirectory = ''
)
$ErrorActionPreference = 'Stop'
try {
    $node = (Get-Command node -ErrorAction Stop).Source
    if ($Realm -eq 'naval' -and $MapId -and $MapId -ne 'sapphire_coast') { throw 'Naval PvP is available only on sapphire_coast.' }
    $arguments = @((Join-Path $PSScriptRoot 'smoke-online.mjs'), '--width', [string]$Width, '--height', [string]$Height, '--timeout', [string]$TimeoutSeconds, '--realm', $Realm)
    if ($NavalSlice) { $arguments += '--naval-slice' }
    # The shared server catalog chooses omitted maps, including fantasy's legend_lands.
    if (-not [string]::IsNullOrWhiteSpace($MapId)) { $arguments += @('--map', $MapId) }
    if (-not [string]::IsNullOrWhiteSpace($OutputDirectory)) { $arguments += @('--output', $OutputDirectory) }
    if (-not [string]::IsNullOrWhiteSpace($ServerUrl)) { $arguments += @('--server', $ServerUrl) }
    if (-not [string]::IsNullOrWhiteSpace($PlayerPath)) { $arguments += @('--player', $PlayerPath) }
    & $node @arguments
    if ($LASTEXITCODE -ne 0) { throw 'The two-client online smoke failed. Inspect the summary and local player logs.' }
    exit 0
}
catch { Write-Error -Message $_.Exception.Message -ErrorAction Continue; exit 1 }
