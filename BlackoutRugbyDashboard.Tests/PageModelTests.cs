using System.Security.Claims;
using BlackoutRugbyDashboard.Data;
using BlackoutRugbyDashboard.Models;
using BlackoutRugbyDashboard.Pages;
using BlackoutRugbyDashboard.Pages.Players;
using BlackoutRugbyDashboard.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlackoutRugbyDashboard.Tests;

/// <summary>
/// The page-model seam (spec Testing Decisions §4): the real Match Analysis and Player
/// History page models, driven against a seeded in-memory cache and a temp snapshot /
/// raw folder, over a fake API client. They assert the rendered state (team sheet,
/// recommendations, trends, season block) and the call budget — never Razor markup.
/// </summary>
public class PageModelTests
{
    private const int TeamId = 45047;
    private const int OpponentId = 45037;

    private static readonly string LineupXml = TestXml.Load("r1-lineups-lu-fixture-21416928.xml");

    private sealed class CountingApi : IBlackoutRugbyApiClient
    {
        public int Calls;

        public Task<string> GetFixturesAsync(int? fixtureId = null, string? fixtureIds = null, int? teamId = null, int? last = null, int? future = null, int? past = null, int? latest = null, int? leagueId = null, int? season = null, int? round = null, bool roundRobin = false, int? friendlyCompId = null, bool youth = false, bool nat = false, bool u20 = false)
            => Count();

        public Task<string> GetMatchSummaryAsync(int? fixtureId = null, string? fixtureIds = null, bool youth = false, bool nat = false, bool u20 = false)
            => Count();

        public Task<string> GetTeamsAsync(int? teamId = null, string? teamIds = null, int? regionId = null, int? leagueId = null, bool nat = false, bool u20 = false, string? country = null)
            => Count();

        public Task<string> GetPlayersAsync(int? playerId = null, string? playerIds = null, int? teamId = null, string? teamIds = null, bool youth = false, bool nat = false, bool u20 = false)
            => Count();

        public Task<string> GetFixtureStatisticsAsync(int fixtureId, int? playerStats = null, int? teamPlayersStats = null)
            => Count();

        public Task<string> GetPlayerStatisticsAsync(int playerId) => Count();

        public Task<string> GetPlayerStatisticsAsync(int playerId, int? season) => Count();

        public Task<string> GetMemberAsync(int memberId) => Count();

        public Task<string> GetLineupsAsync(int teamId, int? fixtureId = null, string? fixtureIds = null, bool youth = false, bool nat = false, bool u20 = false)
            => Count();

        public Task<string> GetStandingsAsync(int? leagueId = null, bool youth = false, bool nat = false, bool u20 = false, int? season = null)
            => Count();

        private Task<string> Count()
        {
            Calls++;
            return Task.FromResult("<blackoutrugby_api_response />");
        }
    }

    private sealed class FakeFactory : IBlackoutRugbyApiClientFactory
    {
        public IBlackoutRugbyApiClient CreateDeveloperOnly() => throw new NotSupportedException();

        public IBlackoutRugbyApiClient CreateForMember(int memberId, string memberKey) => throw new NotSupportedException();

        public BlackoutRugby.Api.BlackoutRugbyApiClient CreateFullSurface(int? memberId = null, string? memberKey = null) => throw new NotSupportedException();
    }

    private sealed class FakeProtector : IMemberKeyProtector
    {
        public string Protect(string plaintext) => "protected";

        public string? Unprotect(string? ciphertext) => null;
    }

    private sealed record Harness(
        DashboardDbContext Db,
        ClubLinkService Links,
        MatchCacheService Cache,
        SnapshotStore Snapshots,
        CountingApi Api,
        string ContentRoot);

    private static Harness BuildHarness()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var db = new DashboardDbContext(new DbContextOptionsBuilder<DashboardDbContext>().UseSqlite(connection).Options);
        db.Database.EnsureCreated();
        db.Users.Add(new DashboardUser { Id = "u1", UserName = "u@example.com", MemberId = 1000, TeamId = TeamId });
        db.SaveChanges();

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, "u1") }, "test"))
        };
        var accessor = new HttpContextAccessor { HttpContext = httpContext };

        var contentRoot = Path.Combine(Path.GetTempPath(), "pmpage-" + Guid.NewGuid().ToString("N"));
        var environment = new TestEnvironment(contentRoot);
        var api = new CountingApi();
        var adapter = new BlackoutRugbyResponseAdapter();

        var links = new ClubLinkService(db, new FakeFactory(), new FakeProtector(), accessor, Options.Create(new DeveloperOptions()), adapter);
        var snapshots = new SnapshotStore(environment);
        var cache = new MatchCacheService(api, adapter, db, new MatchCacheRawStore(Path.Combine(contentRoot, "MatchCache")));
        return new Harness(db, links, cache, snapshots, api, contentRoot);
    }

    private static async Task WriteSnapshotAsync(Harness harness)
    {
        var folder = Path.Combine(harness.ContentRoot, "Data", "Snapshots", TeamId.ToString());
        Directory.CreateDirectory(folder);
        const string json = """
            {"TeamId":45047,"TeamName":"Testers","CapturedAtUtc":"2026-01-01T00:00:00Z","Players":[
              {"Id":16728687,"Name":"M. Ratu","Csr":321},
              {"Id":16398979,"Name":"Cap Stern","Csr":200},
              {"Id":16396178,"Name":"Kick Boot","Csr":180}
            ]}
            """;
        await File.WriteAllTextAsync(Path.Combine(folder, "20260101-000000.json"), json);
    }

    private static void WriteLineupArchive(Harness harness, int fixtureId)
    {
        var rawDirectory = Path.Combine(harness.ContentRoot, "MatchCache", "raw");
        Directory.CreateDirectory(rawDirectory);
        File.WriteAllText(Path.Combine(rawDirectory, $"lu-{fixtureId}-20260101000000000.xml"), LineupXml);
    }

    [Fact]
    public async Task FixtureModel_RendersTheTeamSheetAndRecommendations_FromTheCache()
    {
        var harness = BuildHarness();
        harness.Db.Fixtures.Add(new FixtureRow
        {
            FixtureId = 1, Season = 62, LeagueId = 349097, Round = 2, Competition = "League",
            HomeTeamId = TeamId, GuestTeamId = OpponentId,
            MatchStartUnix = 1789220100, MatchFinishUnix = 1789226106
        });
        harness.Db.TeamFixtureStats.Add(new TeamFixtureStatRow
        {
            FixtureId = 1, TeamId = TeamId, Side = "home", Half = "full",
            Possession = 40, Territory = 45, LineoutsWon = 8, LineoutsLost = 14, PenaltiesConceded = 1
        });
        harness.Db.TeamFixtureStats.Add(new TeamFixtureStatRow
        {
            FixtureId = 1, TeamId = OpponentId, Side = "guest", Half = "full",
            Possession = 60, Territory = 55, Tries = 1
        });
        harness.Db.PlayerFixtures.Add(new PlayerFixtureRow
        {
            FixtureId = 1, TeamId = TeamId, PlayerId = 16728687, Slot = 1, MinutesPlayed = 80
        });
        harness.Db.MatchSummaries.Add(new MatchSummaryRow
        {
            FixtureId = 1, HomePoints = 31, GuestPoints = 17, HomeIntensity = 2
        });
        await harness.Db.SaveChangesAsync();
        await WriteSnapshotAsync(harness);
        WriteLineupArchive(harness, 1);

        var model = new FixtureModel(harness.Links, harness.Cache, harness.Snapshots, NullLogger<FixtureModel>.Instance);
        await model.OnGetAsync(1, recs: "1");

        Assert.False(model.NotFound);
        Assert.True(model.IsOurFixture);
        Assert.Equal(23, model.TeamSheet.Count);
        Assert.Equal("M. Ratu", model.TeamSheet[0].Name);
        Assert.True(model.TeamSheet.Single(slot => slot.PlayerId == 16398979).IsCaptain);
        Assert.Equal("Intensity: 2", model.IntensityChip);
        Assert.Equal(12, model.Compare.Count);
        Assert.True(model.CanRecommend);
        Assert.True(model.ShowRecommendations);
        Assert.Equal(2, model.Recommendations.Count); // possession Watch + lineout Act
        Assert.Equal(RecommendationSeverity.Act, model.Recommendations[0].Severity);
        Assert.Equal(0, harness.Api.Calls); // every read served from the cache
    }

    [Fact]
    public async Task PlayerDetailModel_RendersTrendsAndSeason_WithNoApiCall()
    {
        var harness = BuildHarness();
        harness.Db.Fixtures.Add(new FixtureRow
        {
            FixtureId = 1, Season = 61, Competition = "League", Round = 1,
            HomeTeamId = TeamId, GuestTeamId = OpponentId, MatchStartUnix = 100, MatchFinishUnix = 200
        });
        harness.Db.Fixtures.Add(new FixtureRow
        {
            FixtureId = 2, Season = 62, Competition = "League", Round = 2,
            HomeTeamId = OpponentId, GuestTeamId = TeamId, MatchStartUnix = 300, MatchFinishUnix = 400
        });
        harness.Db.PlayerFixtures.Add(new PlayerFixtureRow
        {
            FixtureId = 1, TeamId = TeamId, PlayerId = 16728687, Slot = 1, MinutesPlayed = 80, Tries = 1
        });
        harness.Db.PlayerFixtures.Add(new PlayerFixtureRow
        {
            FixtureId = 2, TeamId = TeamId, PlayerId = 16728687, Slot = 1, MinutesPlayed = 75, Tries = 2
        });
        harness.Db.MatchSummaries.Add(new MatchSummaryRow { FixtureId = 1, HomePoints = 20, GuestPoints = 10 });
        harness.Db.MatchSummaries.Add(new MatchSummaryRow { FixtureId = 2, HomePoints = 5, GuestPoints = 15 });
        harness.Db.PlayerSeasons.Add(new PlayerSeasonRow
        {
            PlayerId = 16728687, Season = 62, Tries = 7, FetchedAt = DateTime.UtcNow.AddYears(1)
        });
        await harness.Db.SaveChangesAsync();
        await WriteSnapshotAsync(harness);

        var model = new PlayerDetailModel(harness.Links, harness.Cache, harness.Snapshots, NullLogger<PlayerDetailModel>.Instance);
        await model.OnGetAsync(16728687);

        Assert.Equal("M. Ratu", model.PlayerName);
        Assert.Equal(321, model.PlayerCsr);
        Assert.True(model.IsKnown);
        Assert.False(model.EmptyTrends);
        Assert.Equal(2, model.Rows.Count);
        Assert.Equal(2, model.Rows[0].FixtureId); // newest first by default
        Assert.Equal(62, model.Season);           // the newest cached season
        Assert.Equal(new[] { 62, 61 }, model.SeasonOptions);
        Assert.NotNull(model.SeasonTotals);
        Assert.Equal("Tries", model.SeasonTotals!.Groups.Single(group => group.Group == StatGroup.Attack).Fields[0].Label);
        Assert.Equal(0, harness.Api.Calls);
    }

    [Fact]
    public async Task PlayerDetailModel_UnknownPlayer_IsAnEmptyTrendsState_WithNoApiCall()
    {
        var harness = BuildHarness();
        await WriteSnapshotAsync(harness);

        var model = new PlayerDetailModel(harness.Links, harness.Cache, harness.Snapshots, NullLogger<PlayerDetailModel>.Instance);
        await model.OnGetAsync(999999);

        Assert.Equal("Player 999999", model.PlayerName);
        Assert.False(model.IsKnown);
        Assert.True(model.EmptyTrends);
        Assert.False(model.ShowSeasonCard);
        Assert.Null(model.SeasonTotals);
        Assert.Empty(model.SeasonOptions);
        Assert.Equal(0, harness.Api.Calls);
    }

    [Fact]
    public async Task PlayerDetailModel_TransferredOutPlayer_StillRendersTheSeasonCard()
    {
        var harness = BuildHarness();
        // Cached while ours, but absent from the latest snapshot: a transferred-out Player.
        harness.Db.Fixtures.Add(new FixtureRow
        {
            FixtureId = 1, Season = 62, Competition = "League", Round = 1,
            HomeTeamId = TeamId, GuestTeamId = OpponentId, MatchStartUnix = 100, MatchFinishUnix = 200
        });
        harness.Db.PlayerFixtures.Add(new PlayerFixtureRow
        {
            FixtureId = 1, TeamId = TeamId, PlayerId = 777, Slot = 1, MinutesPlayed = 80, Tries = 2
        });
        harness.Db.MatchSummaries.Add(new MatchSummaryRow { FixtureId = 1, HomePoints = 20, GuestPoints = 10 });
        harness.Db.PlayerSeasons.Add(new PlayerSeasonRow
        {
            PlayerId = 777, Season = 62, LeagueCaps = 4, FetchedAt = DateTime.UtcNow.AddYears(1)
        });
        await harness.Db.SaveChangesAsync();
        await WriteSnapshotAsync(harness); // the snapshot carries no Player 777

        var model = new PlayerDetailModel(harness.Links, harness.Cache, harness.Snapshots, NullLogger<PlayerDetailModel>.Instance);
        await model.OnGetAsync(777);

        Assert.False(model.IsKnown);
        Assert.Null(model.PlayerCsr);       // the chip blanks once absent from the snapshot
        Assert.True(model.ShowSeasonCard);  // but the card still renders (D7 §5)
        Assert.NotNull(model.SeasonTotals);
        Assert.False(model.EmptyTrends);
        Assert.Equal(0, harness.Api.Calls);
    }
}



