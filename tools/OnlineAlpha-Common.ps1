# Shared ownership checks for the explicitly started alpha service and tunnel.
Set-StrictMode -Version Latest

function ConvertTo-OnlineArgument([string]$Value) {
    if ($Value.Contains('"')) { throw 'Process arguments cannot contain embedded quotes.' }
    return '"' + [regex]::Replace($Value, '(\\+)$', '$1$1') + '"'
}
function Get-OnlineIdentity($Process, [string]$Purpose, [string]$Marker) {
    $Process.Refresh()
    $expectedProcessId = $Process.Id
    $expectedStartTicks = $Process.StartTime.ToUniversalTime().Ticks.ToString()
    $identityWait = [Diagnostics.Stopwatch]::StartNew()
    do {
        # A just-started Process can temporarily report no Path. Reacquire it,
        # but never adopt another lifetime if Windows has already reused its PID.
        $candidate = Get-Process -Id $expectedProcessId -ErrorAction SilentlyContinue
        if ($null -eq $candidate) { throw ($Purpose + ' process exited before its identity could be captured.') }
        $candidate.Refresh()
        if ($candidate.Id -ne $expectedProcessId -or $candidate.StartTime.ToUniversalTime().Ticks.ToString() -ne $expectedStartTicks) {
            throw ($Purpose + ' process identity changed before capture completed.')
        }
        $executable = $null
        try { $executable = $candidate.Path } catch { } # A transient path query must never produce an incomplete identity.
        if (-not [string]::IsNullOrWhiteSpace($executable)) {
            return [pscustomobject]@{ Pid = $expectedProcessId; StartedUtcTicks = $expectedStartTicks;
                Executable = $executable; Purpose = $Purpose; Marker = $Marker }
        }
        $remaining = 2000 - $identityWait.ElapsedMilliseconds
        if ($remaining -gt 0) { Start-Sleep -Milliseconds ([int][Math]::Min(100, $remaining)) }
    } while ($identityWait.ElapsedMilliseconds -lt 2000)
    throw ($Purpose + ' process executable path remained unavailable after the identity capture timeout.')
}
function Find-OnlineOwnedProcess($Identity) {
    if ($null -eq $Identity) { return $null }
    $candidate = Get-Process -Id $Identity.Pid -ErrorAction SilentlyContinue
    if ($null -eq $candidate) { return $null }
    try {
        if ($candidate.StartTime.ToUniversalTime().Ticks.ToString() -ne $Identity.StartedUtcTicks -or
            -not [string]::Equals($candidate.Path, $Identity.Executable, [StringComparison]::OrdinalIgnoreCase)) { return $null }
        $details = Get-CimInstance Win32_Process -Filter ('ProcessId = ' + $Identity.Pid) -ErrorAction Stop
        if ($null -eq $details.CommandLine -or $details.CommandLine.IndexOf($Identity.Marker, [StringComparison]::OrdinalIgnoreCase) -lt 0) { return $null }
        return $candidate
    }
    catch { return $null }
}
function Get-OnlineWorkers($ServiceIdentity, [string]$AuthorityPath) {
    if ($null -eq (Find-OnlineOwnedProcess $ServiceIdentity)) { return }
    foreach ($child in @(Get-CimInstance Win32_Process -Filter ('ParentProcessId = ' + $ServiceIdentity.Pid))) {
        if ($child.Name -ne 'dotnet.exe' -or $null -eq $child.CommandLine -or $child.CommandLine.IndexOf($AuthorityPath, [StringComparison]::OrdinalIgnoreCase) -lt 0) { continue }
        $process = Get-Process -Id $child.ProcessId -ErrorAction SilentlyContinue
        if ($null -ne $process) { Get-OnlineIdentity $process 'authority' $AuthorityPath }
    }
}
function Stop-OnlineOwnedProcess($Identity) {
    if ($null -eq $Identity) { return }
    $process = Find-OnlineOwnedProcess $Identity
    if ($null -eq $process) { return [pscustomobject]@{ Purpose = $Identity.Purpose; Pid = $Identity.Pid; Status = 'Absent or identity differs; untouched' } }
    # A Process instance plus start time/executable/command marker prevents a reused PID from targeting another program.
    Stop-Process -InputObject $process -Force -ErrorAction Stop
    [void]$process.WaitForExit(5000)
    return [pscustomobject]@{ Purpose = $Identity.Purpose; Pid = $Identity.Pid; Status = 'Stopped owned process' }
}
function Write-OnlineState([string]$Path, $State) {
    [IO.File]::WriteAllText($Path, ($State | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
}
function Read-OnlineHealth([string]$Address) {
    try { return Invoke-RestMethod -Uri ($Address + '/health') -Method Get -TimeoutSec 4 -MaximumRedirection 0 -ErrorAction Stop }
    catch { return $null }
}
