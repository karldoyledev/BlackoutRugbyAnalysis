using System.Text.Json;
using BlackoutRugbyDashboard.Data;
using BlackoutRugbyDashboard.Models;
using BlackoutRugbyDashboard.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BlackoutRugbyDashboard.Tests;

/// <summary>
/// The Squad page's cache read (S2 load model, build slice #33): a plain visit
/// renders from the Match Cache and the latest Squad Snapshot only — the club
/// strip, the derived season, the window, and the capture that feeds the
/// comparison card. The reader is built with an API client that throws on every
/// call, so "zero API calls on GET" is asserted here rather than assumed.
/// </summary>
public class SquadPageReaderTests
{
    private const int TeamId = 45047;
    private const int OpponentId = 45037;

    private static readonly JsonSerializerOptions SnapshotJson = new() { WriteIndented = true };

    /// <summary>The load model's contract, enforced: a plain GET makes no API read.</summary>
    private sealed class NeverCalledApi : IBlackoutRugbyApiClient
    {
        public Task<string> GetFixturesAsync(int? fixtureId = null, string? fixtureIds = null, int? teamId = null, int? last = null, int? future = null, int? past = null, int? latest = null, int? leagueId = null, int? season = null, int? round = null, bool roundRobin = false, int? friendlyCompId = null, bool youth = false, bool nat = false, bool u20 = false) => throw Offender();

        public Task<string> GetMatchSummaryAsync(int? fixtureId = null, string? fixtureIds = null, bool youth = false, bool nat = false, bool u20 = false) => throw Offender();

        public Task<string> GetTeamsAsync(int? teamId = null, string? teamIds = null, int? regionId = null, int? leagueId = null, bool nat = false, bool u20 = false, string? country = null) => throw Offender();

        public Task<string> GetPlayersAsync(int? playerId = null, string? playerIds = null, int? teamId = null, string? teamIds = null, bool youth = false, bool nat = false, bool u20 = false) => throw Offender();

        public Task<string> GetFixtureStatisticsAsync(int fixtureId, int? playerStats = null, int? teamPlayersStats = null) => throw Offender();

        public Task<string> GetPlayerStatisticsAsync(int playerId) => throw Offender();

        public Task<string> GetPlayerStatisticsAsync(int playerId, int? season) => throw Offender();

        public Task<string> GetMemberAsync(int memberId) => throw Offender();

        public Task<string> GetLineupsAsync(int teamId, int? fixtureId = null, string? fixtureIds = null, bool youth = false, bool nat = false, bool u20 = false) => throw Offender();

        private static InvalidOperationException Offender(
            [System.Runtime.CompilerServices.CallerMemberName] string read = "") =>
            new($"the Squad page's cache read called {read} — a plain GET must not read the API");
    }

    private static (SquadPageReader Reader, DashboardDbContext Db, string SnapshotRoot) BuildReader()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var db = new DashboardDbContext(new DbContextOptionsBuilder<DashboardDbContext>().UseSqlite(connection).Options);
        db.Database.EnsureCreated();

        var rawRoot = Path.Combine(Path.GetTempPath(), "squadreader-raw-" + Guid.NewGuid().ToString("N"));
        var snapshotRoot = Path.Combine(Path.GetTempPath(), "squadreader-snap-" + Guid.NewGuid().ToString("N"));
        var cache = new MatchCacheService(
            new NeverCalledApi(),
            new BlackoutRugbyResponseAdapter(),
            db,
            new MatchCacheRawStore(rawRoot));

        return (new SquadPageReader(cache, new SnapshotStore(new TestEnvironment(snapshotRoot))), db, snapshotRoot);
    }

    private static FixtureRow CachedFixture(int fixtureId, int season, int round, long startUnix, long finishUnix) => new()
    {
        FixtureId = fixtureId,
        Season = season,
        LeagueId = 349097,
        Round = round,
        Competition = "League",
        HomeTeamId = TeamId,
        GuestTeamId = OpponentId,
        WeatherId = 1,
        BotMatch = 0,
        DataRemoved = 0,
        Stadium = "0",
        CountryIso = "IE",
        MatchStartUnix = startUnix,
        MatchFinishUnix = finishUnix,
        FetchedAt = new DateTime(2026, 9, 20, 8, 0, 0, DateTimeKind.Utc)
    };

    private static TeamFactRow CapturedTeamFact(string name, int csr, DateTime capturedAt) => new()
    {
        TeamId = TeamId,
        CapturedAt = capturedAt,
        Name = name,
        CountryIso = "IE",
        Bot = false,
        AverageTop15Csr = csr,
        RankingPoints = null,
        LeagueId = 349097,
        RegionalRank = 0,
        NationalRank = 0,
        WorldRank = 0
    };

    /// <summary>Writes a Squad Snapshot straight into the snapshot folder, so a
    /// multi-capture history is cheap to set up (the store's file order is the
    /// history order).</summary>
    private static void WriteCapture(string snapshotRoot, DateTime capturedAtUtc, params (int Id, string Name, int Csr)[] players)
    {
        var snapshot = new TeamSnapshot
        {
            TeamId = TeamId,
            TeamName = "Ashgrove RFC",
            CapturedAtUtc = capturedAtUtc,
            Players = players
                .Select(player => new PlayerSnapshotRecord { Id = player.Id, Name = player.Name, Csr = player.Csr })
                .ToList()
        };

        var folder = Path.Combine(snapshotRoot, "Data", "Snapshots", TeamId.ToString());
        Directory.CreateDirectory(folder);
        File.WriteAllText(
            Path.Combine(folder, $"{capturedAtUtc:yyyyMMdd-HHmmss}.json"),
            JsonSerializer.Serialize(snapshot, SnapshotJson));
    }

    [Fact]
    public async Task ReadAsync_EmptyCache_IsTheColdStart()
    {
        var (reader, _, _) = BuildReader();

        var page = await reader.ReadAsync(TeamId);

        Assert.True(page.IsColdStart);
        Assert.Equal("Team 45047", page.ClubName);
        Assert.Null(page.TeamCsr);
        Assert.Equal(0, page.Season);
        Assert.Empty(page.Window);
        Assert.Null(page.Capture);
        Assert.Null(page.Comparison);
    }

    [Fact]
    public async Task ReadAsync_CachedWindow_DerivesTheSeasonAndOrdersNewestFirst()
    {
        var (reader, db, _) = BuildReader();
        db.Fixtures.AddRange(
            CachedFixture(1001, season: 61, round: 9, startUnix: 1_780_000_000, finishUnix: 1_780_006_000),
            CachedFixture(1002, season: 62, round: 1, startUnix: 1_789_220_100, finishUnix: 1_789_226_106),
            CachedFixture(1003, season: 62, round: 2, startUnix: 1_789_824_900, finishUnix: 1_789_830_000));
        await db.SaveChangesAsync();

        var page = await reader.ReadAsync(TeamId);

        Assert.False(page.IsColdStart);
        Assert.Equal(62, page.Season);
        Assert.Equal(new[] { 1003, 1002, 1001 }, page.Window.Select(row => row.Fixture.FixtureId));
    }

    [Fact]
    public async Task ReadAsync_CachedWindow_TakesOnlyTheNewestCompletedFixtures()
    {
        var (reader, db, _) = BuildReader();
        db.Fixtures.AddRange(
            CachedFixture(1001, season: 61, round: 9, startUnix: 1_780_000_000, finishUnix: 1_780_006_000),
            CachedFixture(1002, season: 62, round: 1, startUnix: 1_789_220_100, finishUnix: 1_789_226_106),
            CachedFixture(1003, season: 62, round: 2, startUnix: 1_789_824_900, finishUnix: 1_789_830_000));
        await db.SaveChangesAsync();

        var page = await reader.ReadAsync(TeamId, windowLast: 2);

        Assert.Equal(new[] { 1003, 1002 }, page.Window.Select(row => row.Fixture.FixtureId));
    }

    [Fact]
    public async Task ReadAsync_ClubStrip_TakesTheNewestTeamFact()
    {
        var (reader, db, _) = BuildReader();
        db.TeamFacts.AddRange(
            CapturedTeamFact("Ashgrove RFC", csr: 60, capturedAt: new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc)),
            CapturedTeamFact("Ashgrove RFC", csr: 71, capturedAt: new DateTime(2026, 9, 20, 8, 0, 0, DateTimeKind.Utc)));
        await db.SaveChangesAsync();

        var page = await reader.ReadAsync(TeamId);

        Assert.Equal("Ashgrove RFC", page.ClubName);
        Assert.Equal(71, page.TeamCsr);
        Assert.False(page.ClubIsBot);
    }

    [Fact]
    public async Task ReadAsync_Capture_IsTheLatestSnapshotAndFeedsTheComparisonCard()
    {
        var (reader, db, snapshotRoot) = BuildReader();
        db.Fixtures.Add(CachedFixture(1003, season: 62, round: 2, startUnix: 1_789_824_900, finishUnix: 1_789_830_000));
        db.PlayerSeasons.Add(new PlayerSeasonRow
        {
            PlayerId = 7,
            Season = 62,
            FetchedAt = new DateTime(2026, 9, 20, 8, 0, 0, DateTimeKind.Utc),
            Tackles = 12
        });
        await db.SaveChangesAsync();
        WriteCapture(snapshotRoot, new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc), (7, "Faulkner", 50));
        WriteCapture(snapshotRoot, new DateTime(2026, 9, 20, 8, 0, 0, DateTimeKind.Utc), (7, "Faulkner", 55));

        var page = await reader.ReadAsync(TeamId);

        Assert.Equal("Ashgrove RFC", page.ClubName); // the capture names the club while no TeamFact is cached
        Assert.Equal(55, Assert.Single(page.Capture!.Players).Csr);
        Assert.Equal(5, Assert.Single(page.Comparison!.PlayerChanges).DeltaCsr);
        Assert.Equal(12, page.SeasonStats[7].Tackles);
        Assert.False(page.IsColdStart);
    }

    [Fact]
    public async Task ReadAsync_OneCapture_HasNoComparisonYet()
    {
        var (reader, _, snapshotRoot) = BuildReader();
        WriteCapture(snapshotRoot, new DateTime(2026, 9, 20, 8, 0, 0, DateTimeKind.Utc), (7, "Faulkner", 55));

        var page = await reader.ReadAsync(TeamId);

        Assert.NotNull(page.Capture);
        Assert.Null(page.Comparison);
        Assert.False(page.IsColdStart); // a capture is something to render, even with an empty window
        Assert.Equal(0, page.Season);
        Assert.Empty(page.SeasonStats);
    }
}
