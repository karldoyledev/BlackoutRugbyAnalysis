# Running the app

One command for start / stop / restart / status: `scripts/run-dashboard.ps1`. It builds, launches detached (`dotnet run`, launch profile `http`), waits for health, and prints URL / PID / log paths.

## Commands

- **Start or status**: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/run-dashboard.ps1` — exits 0 with `Already running: PID <n>` when up; otherwise builds, starts, and polls health.
- **Restart** (pick up latest code): append `-Restart`.
- **Stop**: append `-Stop`.
- **Open browser**: append `-Open`.
- **Port override**: `-Port 5272` (default).

## Facts

- **App**: BlackoutRugbyDashboard — ASP.NET Core net8.0 Razor Pages + SQLite. EF migrations apply automatically at startup.
- **URL**: http://localhost:5272/ (`Properties/launchSettings.json`, profile `http`, `ASPNETCORE_ENVIRONMENT=Development`).
- **Health**: anonymous root returns `302 → /Account/Login?ReturnUrl=%2F`. That **is** the healthy response — every page requires auth; Login/SignUp are the only anonymous pages.
- **Logs**: `.scratch/server.log` (stdout) and `.scratch/server.err.log` (overwritten per launch).
- **Roll-forward**: both csproj files set `<RollForward>LatestMajor</RollForward>` — this machine has only .NET 10 runtimes while the projects target net8.0. Removing it makes `dotnet run` fail with "You must install or update .NET to run this application".
- **Data**: SQLite at `BlackoutRugbyDashboard/Data/dashboard.db`; raw match-cache XML under `BlackoutRugbyDashboard/Data/MatchCache`.
- **Tests**: `dotnet test BlackoutRugbyDashboard.Tests/BlackoutRugbyDashboard.Tests.csproj`.