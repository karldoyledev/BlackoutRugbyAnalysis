using BlackoutRugbyDashboard.Data;
using BlackoutRugbyDashboard.Models;
using BlackoutRugbyDashboard.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlackoutRugbyDashboard.Tests;

/// <summary>
/// The Capture squad action (S2 load model, build slice #34): one explicit live
/// read of the club roster (teams + players), the Squad Snapshot it saves, and
/// the club TeamFact that finally gives the club strip's Team CSR its writer.
/// Failures follow D3: a rejection is the hard error with the Settings deep link;
/// a transport failure replays the last capture instead of losing the page. The
/// API is a stub — the live game API is never called from tests.
/// </summary>
public class SquadCaptureTests
{
    /// <summary>The club is 45037 — the team the live-verified `t` fixture (R5)
    /// describes, so the capture's fact lands on the linked club's id.</summary>
    private const int TeamId = 45037;
    private const int OpponentId = 45047;

    /// <summary>A stub API that records which reads the capture made, so "the
    /// roster read is 2 calls" is asserted rather than assumed.</summary>
    private sealed class StubApi : IBlackoutRugbyApiClient
    {
        public List<string> Calls { get; } = new();

        public Func<string> Teams { get; set; } = () => throw new NotSupportedException();
        public Func<string> Players { get; set; } = () => throw new NotSupportedException();

        public Task<string> GetTeamsAsync(int? teamId = null, string? teamIds = null, int? regionId = null, int? leagueId = null, bool nat = false, bool u20 = false, string? country = null)
        {
            Calls.Add($"t:teamId={teamId}");
            return Task.FromResult(Teams());
        }

        public Task<string> GetPlayersAsync(int? playerId = null, string? playerIds = null, int? teamId = null, string? teamIds = null, bool youth = false, bool nat = false, bool u20 = false)
        {
            Calls.Add($"p:teamId={teamId}");
            return Task.FromResult(Players());
        }

        public Task<string> GetFixturesAsync(int? fixtureId = null, string? fixtureIds = null, int? teamId = null, int? last = null, int? future = null, int? past = null, int? latest = null, int? leagueId = null, int? season = null, int? round = null, bool roundRobin = false, int? friendlyCompId = null, bool youth = false, bool nat = false, bool u20 = false) => throw new NotSupportedException();

        public Task<string> GetMatchSummaryAsync(int? fixtureId = null, string? fixtureIds = null, bool youth = false, bool nat = false, bool u20 = false) => throw new NotSupportedException();

        public Task<string> GetFixtureStatisticsAsync(int fixtureId, int? playerStats = null, int? teamPlayersStats = null) => throw new NotSupportedException();

        public Task<string> GetPlayerStatisticsAsync(int playerId) => throw new NotSupportedException();

        public Task<string> GetPlayerStatisticsAsync(int playerId, int? season) => throw new NotSupportedException();

        public Task<string> GetMemberAsync(int memberId) => throw new NotSupportedException();

        public Task<string> GetLineupsAsync(int teamId, int? fixtureId = null, string? fixtureIds = null, bool youth = false, bool nat = false, bool u20 = false) => throw new NotSupportedException();

        public Task<string> GetStandingsAsync(int? leagueId = null, bool youth = false, bool nat = false, bool u20 = false, int? season = null) => throw new NotSupportedException();
    }

    private sealed record CaptureHost(
        SquadCaptureService Capture,
        StubApi Api,
        DashboardDbContext Db,
        SnapshotStore Snapshots,
        ApiLogger Logger,
        SquadPageReader Reader,
        string SnapshotRoot);

    private static CaptureHost BuildHost()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var db = new DashboardDbContext(new DbContextOptionsBuilder<DashboardDbContext>().UseSqlite(connection).Options);
        db.Database.EnsureCreated();

        var api = new StubApi();
        var adapter = new BlackoutRugbyResponseAdapter();
        var rawRoot = Path.Combine(Path.GetTempPath(), "squadcapture-raw-" + Guid.NewGuid().ToString("N"));
        var contentRoot = Path.Combine(Path.GetTempPath(), "squadcapture-snap-" + Guid.NewGuid().ToString("N"));
        var cache = new MatchCacheService(api, adapter, db, new MatchCacheRawStore(rawRoot));
        var snapshots = new SnapshotStore(new TestEnvironment(contentRoot));
        var logger = new ApiLogger();

        var capture = new SquadCaptureService(
            api,
            adapter,
            cache,
            snapshots,
            logger,
            Options.Create(new DashboardDefaultsOptions { BaseEndpoint = "http://classic-api.blackoutrugby.com" }));

        return new CaptureHost(
            capture, api, db, snapshots, logger, new SquadPageReader(cache, snapshots), contentRoot);
    }

    /// <summary>The live-verified single-team shape (R5): CSR, bot flag and ranks.</summary>
    private static string ClubTeamXml() => TestXml.Load("r5-t-single-45037-fixed.xml");

    private static string RosterXml() => TestXml.Load("dashboard-players.xml");

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

    /// <summary>Writes a Squad Snapshot straight into the snapshot folder (the
    /// store's file order is the capture history order).</summary>
    private static void WriteCapture(
        string contentRoot,
        DateTime capturedAtUtc,
        params (int Id, string Name, int Csr, int Tackles, int TotalPoints, int TotalCaps)[] players)
    {
        var snapshot = new TeamSnapshot
        {
            TeamId = TeamId,
            TeamName = "Pok78",
            CapturedAtUtc = capturedAtUtc,
            Players = players
                .Select(player => new PlayerSnapshotRecord
                {
                    Id = player.Id,
                    Name = player.Name,
                    Csr = player.Csr,
                    Tackles = player.Tackles,
                    TotalPoints = player.TotalPoints,
                    TotalCaps = player.TotalCaps
                })
                .ToList()
        };

        var folder = Path.Combine(contentRoot, "Data", "Snapshots", TeamId.ToString());
        Directory.CreateDirectory(folder);
        File.WriteAllText(
            Path.Combine(folder, $"{capturedAtUtc:yyyyMMdd-HHmmss}.json"),
            System.Text.Json.JsonSerializer.Serialize(snapshot, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }

    [Fact]
    public async Task Capture_ReadsTheRosterAndSavesTheSnapshotAndTheClubFact()
    {
        var host = BuildHost();
        host.Api.Teams = ClubTeamXml;
        host.Api.Players = RosterXml;

        var result = await host.Capture.CaptureAsync(TeamId);

        Assert.Equal(SquadCaptureStatus.Captured, result.Status);
        Assert.True(result.SavedSnapshot);
        Assert.False(result.IsHardError);
        Assert.Equal(2, result.PlayerCount);
        Assert.Equal(new[] { $"t:teamId={TeamId}", $"p:teamId={TeamId}" }, host.Api.Calls);
        Assert.Equal(2, host.Logger.Logs.Count(log => log.Type == "request"));

        var snapshot = await host.Snapshots.GetLatestAsync(TeamId);
        Assert.NotNull(snapshot);
        Assert.Equal(result.CapturedAtUtc, snapshot!.CapturedAtUtc);
        Assert.Equal("Pok78", snapshot.TeamName);
        Assert.Equal(2, snapshot.Players.Count);

        var captured = snapshot.Players[0];
        Assert.Equal(101, captured.Id);
        Assert.Equal("Seán O'Brien", captured.Name);
        Assert.Equal(24, captured.Age); // captured since #34 — the comparison card's Age reads it
        Assert.Equal(145, captured.Csr);
        Assert.Equal(1250000, captured.Salary);
        Assert.Equal(7, captured.Form);
        Assert.Equal(92, captured.Energy);
        Assert.Equal(29, snapshot.Players[1].Age);

        // The club's own TeamFact: the writer the strip's Team CSR chip was missing.
        var clubFact = await host.Db.TeamFacts.AsNoTracking().SingleAsync(fact => fact.TeamId == TeamId);
        Assert.Equal("Pok78", clubFact.Name);
        Assert.Equal("IE", clubFact.CountryIso);
        Assert.Equal(49826, clubFact.AverageTop15Csr);
        Assert.Equal(349097, clubFact.LeagueId);
        Assert.False(clubFact.Bot);
    }

    [Fact]
    public async Task Capture_ColdStart_BootstrapsThePageWithNothingElseSetUp()
    {
        var host = BuildHost();
        host.Api.Teams = ClubTeamXml;
        host.Api.Players = RosterXml;

        var coldStart = await host.Reader.ReadAsync(TeamId);
        Assert.True(coldStart.IsColdStart);

        var result = await host.Capture.CaptureAsync(TeamId);

        Assert.Equal(SquadCaptureStatus.Captured, result.Status);

        // No other setup step: a plain read of the same cache now has a page to render.
        var page = await host.Reader.ReadAsync(TeamId);
        Assert.False(page.IsColdStart);
        Assert.Equal("Pok78", page.ClubName);      // the capture's club fact names the club
        Assert.Equal(49826, page.TeamCsr);         // and feeds the Team CSR chip
        Assert.Equal(0, page.Season);              // nothing cached yet: no derived season
        Assert.Empty(page.SeasonStats);            // no ps reads cached, so the captured values stand in
        Assert.Equal(2, page.Capture!.Players.Count);
        Assert.Equal(24, page.Capture.Players[0].Age);
    }

    [Fact]
    public async Task Capture_FoldsInTheCachedSeasonTotalsForTheDerivedSeason()
    {
        var host = BuildHost();
        host.Api.Teams = ClubTeamXml;
        host.Api.Players = RosterXml;
        host.Db.Fixtures.Add(CachedFixture(1003, season: 62, round: 2, startUnix: 1_789_824_900, finishUnix: 1_789_830_000));
        host.Db.PlayerSeasons.Add(new PlayerSeasonRow
        {
            PlayerId = 101,
            Season = 62,
            FetchedAt = new DateTime(2026, 9, 20, 8, 0, 0, DateTimeKind.Utc),
            Tackles = 44,
            TotalPoints = 210,
            LeagueCaps = 14
        });
        await host.Db.SaveChangesAsync();

        await host.Capture.CaptureAsync(TeamId);

        var snapshot = await host.Snapshots.GetLatestAsync(TeamId);
        var captured = snapshot!.Players.Single(player => player.Id == 101);
        Assert.Equal(44, captured.Tackles);
        Assert.Equal(210, captured.TotalPoints);
        Assert.Equal(14, captured.TotalCaps);
        // No season read and no previous capture for this player: nothing to carry.
        Assert.Equal(0, snapshot.Players.Single(player => player.Id == 102).Tackles);
    }

    [Fact]
    public async Task Capture_CarriesThePreviousCapturesTotalsOver_WhenNoSeasonReadIsCached()
    {
        var host = BuildHost();
        host.Api.Teams = ClubTeamXml;
        host.Api.Players = RosterXml;
        host.Db.Fixtures.Add(CachedFixture(1003, season: 62, round: 2, startUnix: 1_789_824_900, finishUnix: 1_789_830_000));
        await host.Db.SaveChangesAsync();
        WriteCapture(
            host.SnapshotRoot,
            new DateTime(2026, 9, 20, 8, 0, 0, DateTimeKind.Utc),
            (101, "Seán O'Brien", 145, 44, 210, 14));

        await host.Capture.CaptureAsync(TeamId);

        // The roster read knows nothing about season-to-date totals: the previous
        // capture's values survive rather than being zeroed under the page's feet.
        var snapshot = await host.Snapshots.GetLatestAsync(TeamId);
        var captured = snapshot!.Players.Single(player => player.Id == 101);
        Assert.Equal(44, captured.Tackles);
        Assert.Equal(210, captured.TotalPoints);
        Assert.Equal(14, captured.TotalCaps);
    }

    [Fact]
    public async Task Capture_RejectedCredentials_IsTheHardErrorAndKeepsTheCachedData()
    {
        var host = BuildHost();
        host.Api.Teams = () => "<teams><error>The member key is not valid</error></teams>";
        host.Api.Players = RosterXml;
        var capturedAt = new DateTime(2026, 9, 20, 8, 0, 0, DateTimeKind.Utc);
        WriteCapture(host.SnapshotRoot, capturedAt, (101, "Seán O'Brien", 145, 44, 210, 14));

        var result = await host.Capture.CaptureAsync(TeamId);

        Assert.Equal(SquadCaptureStatus.ApiRejected, result.Status);
        Assert.True(result.IsHardError);
        Assert.Contains("Member Key", result.Message);
        Assert.Contains("Settings", result.Message);
        Assert.False(result.SavedSnapshot);
        // The rejected first read stops the pair: credentials that fail the team read
        // fail the roster read too, and the API's calls are rate-limited.
        Assert.Equal(new[] { $"t:teamId={TeamId}" }, host.Api.Calls);

        // Nothing was overwritten, so the page still renders the last capture.
        var snapshot = await host.Snapshots.GetLatestAsync(TeamId);
        Assert.Equal(capturedAt, snapshot!.CapturedAtUtc);
        Assert.Empty(await host.Db.TeamFacts.ToListAsync()); // no half-capture: the club fact waits for a real read
    }

    [Fact]
    public async Task Capture_ErrorOnTheRosterRead_IsTheHardError()
    {
        var host = BuildHost();
        host.Api.Teams = ClubTeamXml;
        host.Api.Players = () => "<players><error>No data requested</error></players>";

        var result = await host.Capture.CaptureAsync(TeamId);

        Assert.Equal(SquadCaptureStatus.ApiRejected, result.Status);
        Assert.True(result.IsHardError);
        Assert.Contains("No data requested", result.Message);
        Assert.Null(await host.Snapshots.GetLatestAsync(TeamId));
    }

    [Fact]
    public async Task Capture_TransportFailure_ReplaysTheLastCaptureWithALoudWarning()
    {
        var host = BuildHost();
        host.Api.Teams = () => throw new HttpRequestException("the API is unreachable");
        var capturedAt = new DateTime(2026, 9, 20, 8, 0, 0, DateTimeKind.Utc);
        WriteCapture(host.SnapshotRoot, capturedAt, (101, "Seán O'Brien", 145, 44, 210, 14));

        var result = await host.Capture.CaptureAsync(TeamId);

        Assert.Equal(SquadCaptureStatus.ReplayedLastSnapshot, result.Status);
        Assert.True(result.IsDegraded);
        Assert.False(result.IsHardError);
        Assert.Contains("as captured", result.Message);
        Assert.Contains("1 player(s)", result.Message);
        Assert.Equal(capturedAt, result.CapturedAtUtc);
        Assert.Equal(1, result.PlayerCount);

        // The replay reads the existing snapshot — nothing is rewritten.
        var snapshot = await host.Snapshots.GetLatestAsync(TeamId);
        Assert.Equal(capturedAt, snapshot!.CapturedAtUtc);
    }

    [Fact]
    public async Task Capture_TransportFailureWithNothingCaptured_IsTheHardError()
    {
        var host = BuildHost();
        host.Api.Teams = () => throw new HttpRequestException("the API is unreachable");

        var result = await host.Capture.CaptureAsync(TeamId);

        Assert.Equal(SquadCaptureStatus.NoSnapshotToReplay, result.Status);
        Assert.True(result.IsHardError);
        Assert.Contains("no Squad Snapshot exists", result.Message);
    }

    [Fact]
    public async Task Capture_EmptyRoster_SavesNothingRatherThanAnEmptyCapture()
    {
        var host = BuildHost();
        host.Api.Teams = ClubTeamXml;
        host.Api.Players = () => "<players></players>";

        var result = await host.Capture.CaptureAsync(TeamId);

        Assert.Equal(SquadCaptureStatus.EmptyRoster, result.Status);
        Assert.False(result.SavedSnapshot);
        Assert.False(result.IsHardError);
        Assert.Null(await host.Snapshots.GetLatestAsync(TeamId));
    }

    [Fact]
    public async Task Capture_SaveFailure_IsTheHardErrorRatherThanAThrowingSeam()
    {
        var host = BuildHost();
        host.Api.Teams = ClubTeamXml;
        host.Api.Players = RosterXml;
        // A file where the team's snapshot folder belongs: the save cannot proceed.
        File.WriteAllText(
            Path.Combine(host.SnapshotRoot, "Data", "Snapshots", TeamId.ToString()), "not a folder");

        var result = await host.Capture.CaptureAsync(TeamId);

        Assert.Equal(SquadCaptureStatus.CaptureFailed, result.Status);
        Assert.True(result.IsHardError);
        Assert.Contains("saving the capture failed", result.Message);
    }

    [Fact]
    public async Task Capture_WithoutALinkedClub_IsTheHardErrorWithNoApiCall()
    {
        var host = BuildHost();

        var result = await host.Capture.CaptureAsync(teamId: 0);

        Assert.Equal(SquadCaptureStatus.NoLinkedClub, result.Status);
        Assert.True(result.IsHardError);
        Assert.Empty(host.Api.Calls);
    }
}
