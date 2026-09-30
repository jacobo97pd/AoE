[CmdletBinding()]
param(
    [ValidateRange(2, 128)][int]$Matches = 32,
    [ValidateRange(30, 600)][int]$Seconds = 75,
    [string]$OutputDirectory,
    [switch]$BuildAuthority
)
$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
if ($BuildAuthority) { & (Join-Path $PSScriptRoot 'Build-Authority.ps1') }
$arguments = @('--experimental-sqlite', (Join-Path $PSScriptRoot 'online-load.mjs'), '--matches', [string]$Matches, '--seconds', [string]$Seconds)
if ($OutputDirectory) { $arguments += @('--output', $OutputDirectory) }
Push-Location $project
try { & node @arguments; if ($LASTEXITCODE -ne 0) { throw 'Online load failed. Inspect its report.json for the actual failure.' } }
finally { Pop-Location }
