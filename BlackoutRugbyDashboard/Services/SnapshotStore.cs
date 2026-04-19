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

    public async Task SaveSnapshotAsync(TeamDashboardViewModel dashboard)
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
    }

    public async Task<TeamSnapshotComparison?> GetLatestComparisonAsync(int teamId)
    {
        var snapshotFiles = Directory.Exists(GetTeamFolder(teamId))
            ? Directory.GetFiles(GetTeamFolder(teamId), "*.json").OrderByDescending(path => path).ToList()
            : new List<string>();

        if (snapshotFiles.Count < 2)
        {
            return null;
        }

        var current = await LoadSnapshotAsync(snapshotFiles[0]).ConfigureAwait(false);
        var previous = await LoadSnapshotAsync(snapshotFiles[1]).ConfigureAwait(false);
        return BuildComparison(previous, current);
    }

    private static async Task<TeamSnapshot> LoadSnapshotAsync(string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath).ConfigureAwait(false);
        return JsonSerializer.Deserialize<TeamSnapshot>(json, JsonOptions) ?? new TeamSnapshot();
    }

    private static TeamSnapshotComparison BuildComparison(TeamSnapshot previous, TeamSnapshot current)
    {
        var previousPlayers = previous.Players.ToDictionary(player => player.Id);
        var currentPlayers = current.Players.ToDictionary(player => player.Id);
        var allIds = previousPlayers.Keys.Union(currentPlayers.Keys).OrderBy(id => id);
        var changes = new List<PlayerChangeSummary>();

        foreach (var playerId in allIds)
        {
            previousPlayers.TryGetValue(playerId, out var older);
            currentPlayers.TryGetValue(playerId, out var newer);

            if (older is null && newer is not null)
            {
                changes.Add(new PlayerChangeSummary
                {
                    Id = newer.Id,
                    Name = newer.Name,
                    IsNewPlayer = true,
                    DeltaCsr = newer.Csr,
                    DeltaSalary = newer.Salary,
                    DeltaForm = newer.Form,
                    DeltaEnergy = newer.Energy,
                    DeltaTotalPoints = newer.TotalPoints,
                    DeltaTries = newer.Tries,
                    DeltaTackles = newer.Tackles,
                    DeltaMetresGained = newer.MetresGained,
                    DeltaTotalCaps = newer.TotalCaps
                });

                continue;
            }

            if (older is not null && newer is null)
            {
                changes.Add(new PlayerChangeSummary
                {
                    Id = older.Id,
                    Name = older.Name,
                    IsRemovedPlayer = true,
                    DeltaCsr = -older.Csr,
                    DeltaSalary = -older.Salary,
                    DeltaForm = -older.Form,
                    DeltaEnergy = -older.Energy,
                    DeltaTotalPoints = -older.TotalPoints,
                    DeltaTries = -older.Tries,
                    DeltaTackles = -older.Tackles,
                    DeltaMetresGained = -older.MetresGained,
                    DeltaTotalCaps = -older.TotalCaps
                });

                continue;
            }

            if (older is null || newer is null)
            {
                continue;
            }

            changes.Add(new PlayerChangeSummary
            {
                Id = newer.Id,
                Name = newer.Name,
                DeltaCsr = newer.Csr - older.Csr,
                DeltaSalary = newer.Salary - older.Salary,
                DeltaForm = newer.Form - older.Form,
                DeltaEnergy = newer.Energy - older.Energy,
                DeltaTotalPoints = newer.TotalPoints - older.TotalPoints,
                DeltaTries = newer.Tries - older.Tries,
                DeltaTackles = newer.Tackles - older.Tackles,
                DeltaMetresGained = newer.MetresGained - older.MetresGained,
                DeltaTotalCaps = newer.TotalCaps - older.TotalCaps
            });
        }

        return new TeamSnapshotComparison
        {
            PreviousCapturedAtUtc = previous.CapturedAtUtc,
            CurrentCapturedAtUtc = current.CapturedAtUtc,
            PlayerChanges = changes
                .Where(change => change.IsNewPlayer
                    || change.IsRemovedPlayer
                    || change.DeltaCsr != 0
                    || change.DeltaEnergy != 0
                    || change.DeltaTotalPoints != 0
                    || change.DeltaTries != 0
                    || change.DeltaTackles != 0
                    || change.DeltaMetresGained != 0
                    || change.DeltaTotalCaps != 0)
                .ToList()
        };
    }

    private string GetTeamFolder(int teamId)
    {
        return Path.Combine(_snapshotRoot, teamId.ToString());
    }
}
