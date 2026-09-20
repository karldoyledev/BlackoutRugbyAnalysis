# Dashboard launcher: start / stop / restart / status in one command.
# Runbook: docs/agents/run.md
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File scripts/run-dashboard.ps1            # start, or report status
#   powershell -NoProfile -ExecutionPolicy Bypass -File scripts/run-dashboard.ps1 -Restart   # kill + relaunch
#   powershell -NoProfile -ExecutionPolicy Bypass -File scripts/run-dashboard.ps1 -Stop      # kill only
#   ... -Open                                                                                # + open the browser
param(
    [switch]$Stop,
    [switch]$Restart,
    [switch]$Open,
    [int]$Port = 5272
)

$ErrorActionPreference = 'Stop'

$RepoRoot    = Split-Path -Parent $PSScriptRoot
$ProjectPath = Join-Path $RepoRoot 'BlackoutRugbyDashboard\BlackoutRugbyDashboard.csproj'
$LogDir      = Join-Path $RepoRoot '.scratch'
$Url         = "http://localhost:$Port/"

# $null = nothing listening; -1 = a foreign process holds the port; >0 = our dashboard PIyes t
function Get-ListenerId {
    $conn = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $conn) { return $null }
    $proc = Get-CimInstance Win32_Process -Filter "ProcessId = $($conn.OwningProcess)"
    if ($proc -and $proc.CommandLine -match 'BlackoutRugbyDashboard') { return [int]$conn.OwningProcess }
    return -1
}

# The app requires auth everywhere, so the healthy anonymous response is a 302 to /Account/Login.
function Test-Healthy {
    $code = (& curl.exe -s -o NUL -w '%{http_code}' --noproxy '*' --max-time 5 $Url 2>$null) -join ''
    return $code -match '^(200|302)$'
}

function Stop-Dashboard {
    $id = Get-ListenerId
    if ($null -eq $id) { Write-Output "Not running (nothing listens on port $Port)."; return }
    if ($id -eq -1) { throw "Port $Port is held by a foreign process; refusing to stop it." }
    Stop-Process -Id $id -Force
    for ($i = 0; $i -lt 20; $i++) {
        if ($null -eq (Get-ListenerId)) { break }
        Start-Sleep -Milliseconds 500
    }
    Write-Output "Stopped dashboard (was PID $id)."
}

function Start-Dashboard {
    if (-not (Test-Path $LogDir)) { New-Item -ItemType Directory -Force -Path $LogDir | Out-Null }
    $outLog = Join-Path $LogDir 'server.log'
    $errLog = Join-Path $LogDir 'server.err.log'

    Write-Output "Building & launching (dotnet run, launch profile 'http')..."
    $runner = Start-Process -FilePath 'dotnet' `
        -ArgumentList ('run --project "{0}" --launch-profile http' -f $ProjectPath) `
        -WorkingDirectory (Split-Path -Parent $ProjectPath) `
        -RedirectStandardOutput $outLog -RedirectStandardError $errLog `
        -WindowStyle Hidden -PassThru

    $deadline = (Get-Date).AddSeconds(90)
    while ((Get-Date) -lt $deadline) {
        if ($runner.HasExited) {
            Write-Output "Launcher exited before the app became healthy. Log tails:"
            Get-Content $errLog -Tail 25 -ErrorAction SilentlyContinue
            Get-Content $outLog -Tail 25 -ErrorAction SilentlyContinue
            exit 1
        }
        if (Test-Healthy) {
            $id = Get-ListenerId
            Write-Output "Dashboard is up."
            Write-Output "  URL:  $Url"
            Write-Output "  PID:  $(if ($id -and $id -gt 0) { $id } else { $runner.Id })"
            Write-Output "  Logs: $outLog | $errLog"
            if ($Open) { Start-Process $Url }
            return
        }
        Start-Sleep -Seconds 2
    }
    Write-Output "Timed out waiting for $Url. Log tails:"
    Get-Content $errLog -Tail 25 -ErrorAction SilentlyContinue
    Get-Content $outLog -Tail 25 -ErrorAction SilentlyContinue
    exit 1
}

if ($Stop)    { Stop-Dashboard; exit 0 }
if ($Restart) { Stop-Dashboard; Start-Dashboard; exit 0 }

$id = Get-ListenerId
if ($id -eq -1) { throw "Port $Port is held by a foreign process; cannot start." }
if ($null -ne $id) {
    Write-Output "Already running: PID $id at $Url"
    if ($Open) { Start-Process $Url }
    exit 0
}
Start-Dashboard