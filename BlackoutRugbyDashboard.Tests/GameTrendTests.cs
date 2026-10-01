using BlackoutRugbyDashboard.Data;
using BlackoutRugbyDashboard.Models;
using BlackoutRugbyDashboard.Services;
using Xunit;

namespace BlackoutRugbyDashboard.Tests;

/// <summary>
/// The Game review card's trend mode (S3, build slice #37): the picker spanning
/// every charted field plus the three output columns, the per-player series across
/// the cached window (oldest to newest, gaps for games a Player missed), the latest
/// reading, its movement and the window total. The builder is pure — it takes the
/// page's cached window and returns rows — so "cache-only" is structural, not
/// asserted.
/// </summary>
public class GameTrendTests
{
    private const int TeamId = 45047;
    private const int OpponentId = 45037;

    private static readonly DateTime Day0 = new(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc);

    private static long Unix(int dayOffset) => new DateTimeOffset(Day0.AddDays(dayOffset)).ToUnixTimeSeconds();

    private static PlayerFixtureRow Player(int playerId, int slot, params (string Field, int Value)[] values)
    {
        var row = new PlayerFixtureRow
        {
            PlayerId = playerId,
            TeamId = TeamId,
            Side = "home",
            Jersey = slot,
            Slot = slot,
            MinutesPlayed = 80,
            TotalPoints = 5,
            EnergyAfter = 62
        };

        foreach (var (field, value) in values)
        {
            typeof(PlayerFixtureRow).GetProperty(field)!.SetValue(row, value);
        }

        return row;
    }

    private static SquadFixtureRows Fixture(int fixtureId, int dayOffset, params PlayerFixtureRow[] players) =>
        new(
            new FixtureRow
            {
                FixtureId = fixtureId,
                Season = 62,
                LeagueId = 349097,
                Round = 1,
                Competition = "League",
                HomeTeamId = TeamId,
                GuestTeamId = OpponentId,
                MatchStartUnix = Unix(dayOffset),
                MatchFinishUnix = Unix(dayOffset) + 5400
            },
            new MatchSummaryRow { FixtureId = fixtureId, HomePoints = 31, GuestPoints = 17 },
            players,
            null);

    private static TeamSnapshot Capture(params (int Id, string Name)[] players) => new()
    {
        TeamId = TeamId,
        TeamName = "Doylester",
        CapturedAtUtc = new DateTime(2026, 9, 21, 19, 25, 0, DateTimeKind.Utc),
        Players = players
            .Select(player => new PlayerSnapshotRecord { Id = player.Id, Name = player.Name })
            .ToList()
    };

    private static SquadPageData Page(params SquadFixtureRows[] window) => new(
        TeamId,
        "Ashgrove RFC",
        "IE",
        false,
        71,
        62,
        window,
        new Dictionary<int, string>(),
        Capture((101, "Seán O'Brien"), (102, "Tane Williams")),
        new Dictionary<int, PlayerStatistics>());

    [Fact]
    public void Stats_CoverEveryChartedFieldPlusTheThreeOutputs()
    {
        // 37 D4 fields + the three always-visible outputs (S3).
        Assert.Equal(40, GameTrend.Stats.Count);

        Assert.Equal(
            new[] { "minutes_played", "total_points", "energy_after" },
            GameTrend.Stats.Take(3).Select(stat => stat.Key));
        Assert.All(GameTrend.Stats.Take(3), stat => Assert.Equal(GameTrend.OutputGroup, stat.Group));

        var charted = GameReviewMatrix.Groups
            .SelectMany(GameReviewMatrix.FieldsFor)
            .Select(field => field.Key)
            .ToList();
        Assert.Equal(charted, GameTrend.Stats.Skip(3).Select(stat => stat.Key));

        Assert.Equal(
            new[] { "Output", "Attack", "Defence", "Kicking", "Handling", "Discipline", "Lineout", "Other" },
            GameTrend.Stats.Select(stat => stat.Group).Distinct());
    }

    [Fact]
    public void Resolve_UnknownKeyFallsBackToTheDefault()
    {
        Assert.Equal("minutes_played", GameTrend.DefaultStat.Key);
        Assert.Equal("tries", GameTrend.Resolve("tries").Key);
        Assert.Equal("minutes_played", GameTrend.Resolve(null).Key);
        Assert.Equal("minutes_played", GameTrend.Resolve("  ").Key);
        Assert.Equal("minutes_played", GameTrend.Resolve("not-a-field").Key);
    }

    /// <summary>The window arrives newest-first (SquadPageReader); the series must
    /// still read oldest to newest, with the latest value, its movement and the
    /// window total.</summary>
    [Fact]
    public void Build_RunsOldestToNewest_WithLatestDeltaAndSeason()
    {
        var page = Page(
            Fixture(1002, dayOffset: 7, Player(101, 1, ("Tries", 3))),
            Fixture(1001, dayOffset: 0, Player(101, 1, ("Tries", 1))));

        var row = Assert.Single(GameTrend.Build(page, "tries"));

        Assert.Equal(new double?[] { 1, 3 }, row.Series);
        Assert.Equal(3, row.Latest);
        Assert.Equal(2, row.Delta);
        Assert.Equal(4, row.Season);
        Assert.Equal("Seán O'Brien", row.Name);
    }

    /// <summary>A Fixture a Player missed is a gap in their series, so their latest
    /// reading and movement read between the games they actually featured in.</summary>
    [Fact]
    public void Build_LeavesAGapForAFixtureThePlayerMissed()
    {
        var page = Page(
            Fixture(1003, dayOffset: 14, Player(101, 1, ("Tries", 5))),
            Fixture(1002, dayOffset: 7, Player(102, 2, ("Tries", 3))),
            Fixture(1001, dayOffset: 0, Player(101, 1, ("Tries", 2))));

        var rows = GameTrend.Build(page, "tries");
        var missed = rows.Single(row => row.PlayerId == 101);

        Assert.Equal(new double?[] { 2, null, 5 }, missed.Series);
        Assert.Equal(5, missed.Latest);
        Assert.Equal(3, missed.Delta); // 5 - 2, the two games this player featured in
        Assert.Equal(7, missed.Season);

        // A Player in one Fixture only has a latest and a total, but nothing to move against.
        var single = rows.Single(row => row.PlayerId == 102);
        Assert.Equal(3, single.Latest);
        Assert.Null(single.Delta);
        Assert.Equal(3, single.Season);
    }

    [Fact]
    public void Build_ReadsTheThreeOutputColumns()
    {
        var page = Page(Fixture(1001, dayOffset: 0, Player(101, 1)));

        Assert.Equal(80, Assert.Single(GameTrend.Build(page, "minutes_played")).Latest);
        Assert.Equal(5, Assert.Single(GameTrend.Build(page, "total_points")).Latest);
        Assert.Equal(62, Assert.Single(GameTrend.Build(page, "energy_after")).Latest);
    }

    /// <summary>Rows read in squad order: the slot the Player last appeared in, then
    /// id — so the XV surfaces above the bench however the window is ordered.</summary>
    [Fact]
    public void Build_OrdersByTheMostRecentSlot()
    {
        var page = Page(
            Fixture(1002, dayOffset: 7, Player(201, 1), Player(202, 3)),
            Fixture(1001, dayOffset: 0, Player(201, 5), Player(203, 2)));

        var rows = GameTrend.Build(page, "minutes_played");

        Assert.Equal(new[] { 201, 203, 202 }, rows.Select(row => row.PlayerId));
    }

    /// <summary>An unknown Player falls back to their id, never a blank cell.</summary>
    [Fact]
    public void Build_NamesAnUnknownPlayerById()
    {
        var page = Page(Fixture(1001, dayOffset: 0, Player(999, 4)));

        Assert.Equal("Player 999", Assert.Single(GameTrend.Build(page, "minutes_played")).Name);
    }

    [Fact]
    public void Build_EmptyWindow_IsNoRows()
    {
        Assert.Empty(GameTrend.Build(Page(), "tries"));
    }
}

