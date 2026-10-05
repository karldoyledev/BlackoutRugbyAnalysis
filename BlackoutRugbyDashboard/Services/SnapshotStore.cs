using System.Text.Json;
using BlackoutRugbyDashboard.Models;

namespace BlackoutRugbyDashboard.Services;

public class SnapshotStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _snapshotRoot;

    public SnapshotStore(IWebHostEnvironment environment)
    {
        _snapshotRoot = Path.Combine(environment.ContentRootPath, "Data", "Snapshots");
        Directory.CreateDirectory(_snapshotRoot);
    }

    /// <summary>
    /// Saves a Squad Snapshot (the Capture squad action's point in time) and
    /// returns what was written, so the caller can report when the capture
    /// happened without re-reading the folder.
    /// </summary>
    public async Task<TeamSnapshot> SaveSnapshotAsync(TeamDashboardViewModel dashboard)
    {
        var snapshot = new TeamSnapshot
        {
            TeamId = dashboard.TeamId,
            TeamName = dashboard.TeamName,
            CapturedAtUtc = DateTime.UtcNow,
            Players = dashboard.Players.Select(player => new PlayerSnapshotRecord
            {
                Id = player.Id,
                Name = player.Name,
                Csr = player.Csr,
                Salary = player.Salary,
                Form = player.Form,
                Energy = player.Energy,
                Age = player.Age,
                TotalPoints = player.TotalPoints,
                Tries = player.Tries,
                Tackles = player.Tackles,
                MetresGained = player.MetresGained,
                TotalCaps = player.TotalCaps
            }).ToList()
        };

        var folder = GetTeamFolder(dashboard.TeamId);
        Directory.CreateDirectory(folder);
        var filePath = Path.Combine(folder, $"{snapshot.CapturedAtUtc:yyyyMMdd-HHmmss}.json");
        var json = JsonSerializer.Serialize(snapshot, JsonOptions);
        await File.WriteAllTextAsync(filePath, json).ConfigureAwait(false);
        return snapshot;
    }

    /// <summary>The most recent snapshot for a team, or null when none exists. The
    /// degraded roster replay (D-Squad) sources the dashboard table from here.</summary>
    public async Task<TeamSnapshot?> GetLatestAsync(int teamId)
    {
        var snapshotFiles = Directory.Exists(GetTeamFolder(teamId))
            ? Directory.GetFiles(GetTeamFolder(teamId), "*.json").OrderByDescending(path => path).ToList()
            : new List<string>();

        return snapshotFiles.Count == 0
            ? null
            : await LoadSnapshotAsync(snapshotFiles[0]).ConfigureAwait(false);
    }

    private static async Task<TeamSnapshot> LoadSnapshotAsync(string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath).ConfigureAwait(false);
        return JsonSerializer.Deserialize<TeamSnapshot>(json, JsonOptions) ?? new TeamSnapshot();
    }

    private string GetTeamFolder(int teamId)
    {
        return Path.Combine(_snapshotRoot, teamId.ToString());
    }
}
