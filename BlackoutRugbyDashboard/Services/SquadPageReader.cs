using BlackoutRugbyDashboard.Data;
using BlackoutRugbyDashboard.Models;

namespace BlackoutRugbyDashboard.Services;

/// <summary>
/// Everything a plain visit to /Squad renders: the club strip's identity, the
/// cached window of completed Fixtures, and the latest Squad Snapshot (S2 load
/// model, build slice #33). The snapshot-comparison read retired with the card it
/// fed (C3, #35 — the Game review matrix replaced it).
/// </summary>
public sealed record SquadPageData(
    int TeamId,
    string ClubName,
    string CountryIso,
    bool ClubIsBot,
    int? TeamCsr,
    int Season,
    IReadOnlyList<SquadFixtureRows> Window,
    IReadOnlyDictionary<int, string> TeamNames,
    TeamSnapshot? Capture,
    IReadOnlyDictionary<int, PlayerStatistics> SeasonStats)
{
    /// <summary>Nothing cached at all — no completed Fixture and no Squad
    /// Snapshot: the page's bootstrap empty state, never a silent live read.</summary>
    public bool IsColdStart => Window.Count == 0 && Capture is null;
}

/// <summary>
/// The Squad page's cache read (D-Squad, S2): the window, the club identity and
/// the latest Squad Snapshot, all from the Match Cache and the snapshot folder.
/// It holds no API client — a plain GET cannot read the game API even by accident.
/// The page's only live read is the explicit Capture squad action.
/// </summary>
public class SquadPageReader(MatchCacheService cache, SnapshotStore snapshots)
{
    /// <summary>The squad window: the last 20 completed Fixtures (D-Squad).</summary>
    public const int SquadWindowLast = 20;

    public async Task<SquadPageData> ReadAsync(
        int teamId, int windowLast = SquadWindowLast, CancellationToken cancellationToken = default)
    {
        var window = await cache.GetCachedWindowAsync(teamId, season: 0, windowLast, cancellationToken);
        var season = window.Count == 0 ? 0 : window[0].Season;

        var rows = await cache.GetSquadWindowRowsAsync(
            window.Select(row => row.FixtureId).ToList(), teamId, cancellationToken);
        var orderedRows = rows.Values
            .OrderByDescending(entry => entry.Fixture.MatchStartUnix)
            .ToList();

        var teamNames = await cache.GetTeamNamesAsync(
            window.SelectMany(row => new[] { row.HomeTeamId, row.GuestTeamId }), cancellationToken);
        var teamFact = await cache.GetLatestTeamFactAsync(teamId, cancellationToken);
        var capture = await snapshots.GetLatestAsync(teamId);

        var seasonStats = capture is null || capture.Players.Count == 0
            ? (IReadOnlyDictionary<int, PlayerStatistics>)new Dictionary<int, PlayerStatistics>()
            : await cache.GetCachedPlayerSeasonsAsync(
                capture.Players.Select(player => player.Id).ToList(), season, cancellationToken);

        return new SquadPageData(
            teamId,
            ResolveClubName(teamId, teamFact, capture),
            teamFact?.CountryIso ?? string.Empty,
            teamFact?.Bot ?? false,
            teamFact?.AverageTop15Csr,
            season,
            orderedRows,
            teamNames,
            capture,
            seasonStats);
    }

    /// <summary>The clue we hold about whose page this is: a captured team read,
    /// else the name on the latest Squad Snapshot, else the linked Team id.</summary>
    private static string ResolveClubName(int teamId, TeamFactRow? teamFact, TeamSnapshot? capture)
    {
        if (!string.IsNullOrWhiteSpace(teamFact?.Name))
        {
            return teamFact!.Name;
        }

        return !string.IsNullOrWhiteSpace(capture?.TeamName)
            ? capture!.TeamName
            : $"Team {teamId}";
    }
}
