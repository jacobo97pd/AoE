[CmdletBinding()]
param(
    [switch]$Public,
    [ValidateRange(1024, 65535)][int]$Port = 8787,
    [ValidateRange(1, 16)][int]$Workers = 8,
    [ValidateRange(1, 16)][int]$MatchesPerWorker = 8,
    [string]$StateDirectory = 'D:/CodexTooling/online-validation/alpha',
    [string]$DatabasePath = 'D:/EmberfieldOnline/data/alpha.sqlite',
    [string]$CloudflaredPath = 'D:/CodexTooling/online-validation/cloudflared/cloudflared.exe'
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'OnlineAlpha-Common.ps1')
$project = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$stateRoot = [IO.Path]::GetFullPath($StateDirectory)
$database = [IO.Path]::GetFullPath($DatabasePath)
[void][IO.Directory]::CreateDirectory($stateRoot)
[void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($database))
$statePath = Join-Path $stateRoot 'state.json'
$controlLock = [IO.File]::Open((Join-Path $stateRoot 'control.lock'), [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
$state = $null
$originalTls = [Net.ServicePointManager]::SecurityProtocol
try {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    if (Test-Path -LiteralPath $statePath) {
        $previous = Get-Content -LiteralPath $statePath -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($null -ne (Find-OnlineOwnedProcess $previous.Service) -or $null -ne (Find-OnlineOwnedProcess $previous.Tunnel)) {
            throw 'An owned alpha process is already running. Use Stop-OnlineAlpha.ps1 before starting another configuration; the existing processes were not changed.'
        }
    }
    $node = (Get-Command node -ErrorAction Stop).Source
    $dotnet = (Get-Command dotnet -ErrorAction Stop).Source
    $serverScript = Join-Path $project 'Server/server.mjs'
    $authority = Join-Path $project 'Server/AuthorityWorker/out/Emberfield.Authority.dll'
    if (-not (Test-Path -LiteralPath $authority -PathType Leaf)) { throw 'Build the C# authority with tools/Build-Authority.ps1 first.' }
    if (@(Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue).Count -gt 0) { throw 'The requested port already has a listener. No existing process was changed.' }
    if ($Public -and -not (Test-Path -LiteralPath $CloudflaredPath -PathType Leaf)) { throw 'The verified cloudflared executable is missing. No download or installation was attempted.' }
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
    $local = 'http://127.0.0.1:' + $Port
    $state = [pscustomobject]@{ Schema = 1; StartedUtc = [DateTime]::UtcNow.ToString('o'); StoppedUtc = ''; Ready = $false;
        Project = $project; DatabasePath = $database; AuthorityPath = $authority; AuthoritySha256 = (Get-FileHash -LiteralPath $authority -Algorithm SHA256).Hash;
        LocalAddress = $local; Address = $local; Public = [bool]$Public; TemporaryPublicTunnel = [bool]$Public; Workers = $Workers; MatchesPerWorker = $MatchesPerWorker;
        ProtocolVersion = 0; ContentVersion = ''; Service = $null; Tunnel = $null; Authorities = @(); StopChecks = @(); Failure = '';
        ServiceStdout = (Join-Path $stateRoot ($stamp + '-service.stdout.log')); ServiceStderr = (Join-Path $stateRoot ($stamp + '-service.stderr.log'));
        TunnelStdout = (Join-Path $stateRoot ($stamp + '-tunnel.stdout.log')); TunnelStderr = (Join-Path $stateRoot ($stamp + '-tunnel.stderr.log')) }
    $environment = @{ EMBERFIELD_HOST = '127.0.0.1'; EMBERFIELD_PORT = [string]$Port; EMBERFIELD_DATABASE = $database;
        EMBERFIELD_AUTHORITY_WORKERS = [string]$Workers; EMBERFIELD_MATCHES_PER_WORKER = [string]$MatchesPerWorker;
        EMBERFIELD_TLS_CERT = ''; EMBERFIELD_TLS_KEY = ''; EMBERFIELD_COSMETIC_SANDBOX = ''; EMBERFIELD_TRUST_CLOUDFLARE_LOOPBACK = $(if ($Public) { '1' } else { '' });
        AUTHORITY_COMMAND = $dotnet; AUTHORITY_ARGS_JSON = (ConvertTo-Json -InputObject @($authority) -Compress) }
    $savedEnvironment = @{}
    try {
        foreach ($name in $environment.Keys) { $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process'); [Environment]::SetEnvironmentVariable($name, $environment[$name], 'Process') }
        $process = Start-Process -FilePath $node -ArgumentList @('--experimental-sqlite', (ConvertTo-OnlineArgument $serverScript)) -WorkingDirectory $project -WindowStyle Hidden -PassThru -RedirectStandardOutput $state.ServiceStdout -RedirectStandardError $state.ServiceStderr
        $state.Service = Get-OnlineIdentity $process 'service' $serverScript
    }
    finally { foreach ($name in $savedEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], 'Process') } }
    Write-OnlineState $statePath $state
    $health = $null; $until = [DateTime]::UtcNow.AddSeconds(40)
    do {
        if ($null -eq (Find-OnlineOwnedProcess $state.Service)) { throw 'The alpha service exited before becoming ready. Inspect the recorded service logs.' }
        $health = Read-OnlineHealth $local
        if ($null -ne $health -and $health.ok -and $health.status -eq 'ready') { break }
        Start-Sleep -Milliseconds 300
    } while ([DateTime]::UtcNow -lt $until)
    if ($null -eq $health -or -not $health.ok -or $health.status -ne 'ready') { throw 'The local authority did not become ready.' }
    if ($null -eq (Find-OnlineOwnedProcess $state.Service) -or
        @((Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue) | Where-Object { $_.OwningProcess -eq $state.Service.Pid }).Count -ne 1) {
        throw 'The ready endpoint is not owned by the service process started by this script.'
    }
    $state.Authorities = @(Get-OnlineWorkers $state.Service $authority)
    $state.ProtocolVersion = $health.protocolVersion; $state.ContentVersion = $health.contentVersion
    if ($Public) {
        $tunnelArguments = @('tunnel', '--no-autoupdate', '--loglevel', 'info', '--metrics', '127.0.0.1:0', '--url', $local)
        $tunnel = Start-Process -FilePath ([IO.Path]::GetFullPath($CloudflaredPath)) -ArgumentList $tunnelArguments -WorkingDirectory $stateRoot -WindowStyle Hidden -PassThru -RedirectStandardOutput $state.TunnelStdout -RedirectStandardError $state.TunnelStderr
        $state.Tunnel = Get-OnlineIdentity $tunnel 'tunnel' $local
        Write-OnlineState $statePath $state
        $until = [DateTime]::UtcNow.AddSeconds(90); $publicReady = $false
        do {
            if ($null -eq (Find-OnlineOwnedProcess $state.Service) -or $null -eq (Find-OnlineOwnedProcess $state.Tunnel)) { throw 'The service or tunnel exited during HTTPS startup. Inspect the recorded logs.' }
            $logs = ''
            foreach ($log in @($state.TunnelStdout, $state.TunnelStderr)) { if (Test-Path -LiteralPath $log) { $logs += Get-Content -LiteralPath $log -Raw -Encoding UTF8 } }
            $endpoint = [regex]::Match($logs, 'https://[a-z0-9-]+\.trycloudflare\.com')
            if ($endpoint.Success) {
                $publicHealth = Read-OnlineHealth $endpoint.Value
                if ($null -ne $publicHealth -and $publicHealth.ok -and $publicHealth.status -eq 'ready' -and
                    $publicHealth.protocolVersion -eq $state.ProtocolVersion -and $publicHealth.contentVersion -eq $state.ContentVersion) {
                    $state.Address = $endpoint.Value; $publicReady = $true; break
                }
            }
            Start-Sleep -Milliseconds 400
        } while ([DateTime]::UtcNow -lt $until)
        if (-not $publicReady) { throw 'The temporary HTTPS endpoint did not become ready with the same content version. No certificate validation was bypassed.' }
    }
    $state.Ready = $true; Write-OnlineState $statePath $state
    Write-Host ('Online alpha ready: ' + $state.Address)
    Write-Host ('State and owned process identities: ' + $statePath)
    if ($Public) { Write-Host 'This public HTTPS URL is temporary and remains available only while this computer, service and tunnel are running.' }
    $state
}
catch {
    if ($null -ne $state) {
        $state.Failure = $_.Exception.Message
        $workersToStop = @($state.Authorities) + @(Get-OnlineWorkers $state.Service $state.AuthorityPath)
        $state.StopChecks = @(Stop-OnlineOwnedProcess $state.Tunnel; Stop-OnlineOwnedProcess $state.Service; foreach ($worker in $workersToStop) { Stop-OnlineOwnedProcess $worker })
        $state.Ready = $false; $state.StoppedUtc = [DateTime]::UtcNow.ToString('o'); Write-OnlineState $statePath $state
    }
    throw
}
finally { [Net.ServicePointManager]::SecurityProtocol = $originalTls; $controlLock.Dispose() }
