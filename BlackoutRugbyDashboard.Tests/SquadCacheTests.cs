using System.Net.Http;
using BlackoutRugbyDashboard.Data;
using BlackoutRugbyDashboard.Models;
using BlackoutRugbyDashboard.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Xunit;

namespace BlackoutRugbyDashboard.Tests;

/// <summary>
/// Squad cache-first tests (D-Squad): the warm-reload call budget (Q7a), the ps
/// event-driven staleness rule (Q4b), the degrade-to-cache reads (Q9), and the
/// cached-row ↔ live-parse parity contract on the archived XML.
/// </summary>
public class SquadCacheTests
{
    // Window fixture 21416928 is completed; 21499999 is unplayed. The ids match
    // the archived fs XML so the filled rows join back to the window.
    private const string WindowXml = """
        <blackoutrugby_api_response season="62" round="3" day="2">
          <fixture id="21416928"><id>21416928</id><season>62</season><leagueid>349097</leagueid><round>2</round><hometeamid>45047</hometeamid><guestteamid>45037</guestteamid><competition>League</competition><botmatch>0</botmatch><weather>2</weather><data_removed>0</data_removed><stadium>0</stadium><country_iso>IE</country_iso><matchstart>1789220100</matchstart><matchfinish>1789226106</matchfinish></fixture>
          <fixture id="21499999"><id>21499999</id><season>62</season><leagueid>349097</leagueid><round>3</round><hometeamid>45047</hometeamid><guestteamid>45037</guestteamid><competition>League</competition><botmatch>0</botmatch><weather>1</weather><data_removed>0</data_removed><stadium>0</stadium><country_iso>IE</country_iso><matchstart>1789824900</matchstart></fixture>
        </blackoutrugby_api_response>
        """;

    private const string SingleFixtureXml = """
        <blackoutrugby_api_response season="62" round="3" day="2">
          <fixture id="21416928"><id>21416928</id><season>62</season><leagueid>349097</leagueid><round>2</round><hometeamid>45047</hometeamid><guestteamid>45037</guestteamid><competition>League</competition><botmatch>0</botmatch><weather>2</weather><data_removed>0</data_removed><stadium>0</stadium><country_iso>IE</country_iso><matchstart>1789220100</matchstart><matchfinish>1789226106</matchfinish></fixture>
        </blackoutrugby_api_response>
        """;

    private const string SummaryXml = """
        <blackoutrugby_api_response>
          <match_summary fixtureid="21416928"><home><points>31</points></home><guest><points>17</points></guest></match_summary>
        </blackoutrugby_api_response>
        """;

    private sealed class FakeApi : IBlackoutRugbyApiClient
    {
        public int FixtureCalls, SummaryCalls, TeamCalls, FsCalls, LuCalls, PsCalls;
        public string PlayerStatsXml { get; set; } = string.Empty;
        public bool ThrowOnPlayerStats;

        public Task<string> GetFixturesAsync(int? fixtureId = null, string? fixtureIds = null, int? teamId = null, int? last = null, int? future = null, int? past = null, int? latest = null, int? leagueId = null, int? season = null, int? round = null, bool roundRobin = false, int? friendlyCompId = null, bool youth = false, bool nat = false, bool u20 = false)
        {
            FixtureCalls++;
            return Task.FromResult(fixtureId.HasValue ? SingleFixtureXml : WindowXml);
        }

        public Task<string> GetMatchSummaryAsync(int? fixtureId = null, string? fixtureIds = null, bool youth = false, bool nat = false, bool u20 = false)
        {
            SummaryCalls++;
            return Task.FromResult(SummaryXml);
        }

        public Task<string> GetTeamsAsync(int? teamId = null, string? teamIds = null, int? regionId = null, int? leagueId = null, bool nat = false, bool u20 = false, string? country = null)
        {
            TeamCalls++;
            return Task.FromResult(TestXml.Load("r5-t-single-45037-fixed.xml"));
        }

        public Task<string> GetFixtureStatisticsAsync(int fixtureId, int? playerStats = null, int? teamPlayersStats = null)
        {
            FsCalls++;
            return Task.FromResult(teamPlayersStats.HasValue
                ? TestXml.Load("r3-fs-teamplayers-21416928.xml")
                : TestXml.Load("r3-fs-bare-21416928.xml"));
        }

        public Task<string> GetPlayerStatisticsAsync(int playerId) => GetPlayerStatisticsAsync(playerId, null);

        public Task<string> GetPlayerStatisticsAsync(int playerId, int? season)
        {
            PsCalls++;
            if (ThrowOnPlayerStats)
            {
                throw new HttpRequestException("connection reset");
            }

            return Task.FromResult(PlayerStatsXml);
        }

        public Task<string> GetPlayersAsync(int? playerId = null, string? playerIds = null, int? teamId = null, string? teamIds = null, bool youth = false, bool nat = false, bool u20 = false) => throw new NotSupportedException();

        public Task<string> GetMemberAsync(int memberId) => throw new NotSupportedException();

        public Task<string> GetLineupsAsync(int teamId, int? fixtureId = null, string? fixtureIds = null, bool youth = false, bool nat = false, bool u20 = false)
        {
            LuCalls++;
            return Task.FromResult("<blackoutrugby_api_response />");
        }
    }

    private static (MatchCacheService Service, FakeApi Api, DashboardDbContext Db, string RawRoot) BuildService()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<DashboardDbContext>().UseSqlite(connection).Options;
        var db = new DashboardDbContext(options);
        db.Database.EnsureCreated();
        var api = new FakeApi();
        var rawRoot = Path.Combine(Path.GetTempPath(), "squadtest-" + Guid.NewGuid().ToString("N"));
        var service = new MatchCacheService(api, new BlackoutRugbyResponseAdapter(), db, new MatchCacheRawStore(rawRoot));
        return (service, api, db, rawRoot);
    }
    [Fact]
    public async Task SquadWindow_ColdFillThenWarmReloadCostsOnlyTheDiscoveryRead()
    {
        var (service, api, _, _) = BuildService();

        var cold = await service.FillSquadWindowAsync(45047, 62, 20, "http://x");
        Assert.Equal(1, cold.Completed);
        Assert.Equal(1, cold.NewlyCached);
        Assert.Equal(0, cold.AlreadyCached);
        Assert.Equal(1, cold.Unplayed);
        Assert.Equal(6, cold.ApiCalls); // 1 discovery + 5 entry-scope fill calls
        Assert.Equal(6, api.FixtureCalls + api.SummaryCalls + api.FsCalls + api.LuCalls);

        var warm = await service.FillSquadWindowAsync(45047, 62, 20, "http://x");
        Assert.Equal(1, warm.Completed);
        Assert.Equal(0, warm.NewlyCached);
        Assert.Equal(1, warm.AlreadyCached);
        Assert.Equal(1, warm.ApiCalls); // the warm budget: discovery only — zero fs/ms/lu
        Assert.Equal(7, api.FixtureCalls + api.SummaryCalls + api.FsCalls + api.LuCalls);
    }

    [Fact]
    public async Task SquadWindowRows_ReturnOurPlayersTeamStatsAndSummary()
    {
        var (service, _, _, _) = BuildService();
        await service.FillSquadWindowAsync(45047, 62, 20, "http://x");

        var rows = await service.GetSquadWindowRowsAsync(new[] { 21416928 }, 45047);

        var entry = Assert.Single(rows.Values);
        Assert.Equal(21416928, entry.Fixture.FixtureId);
        Assert.Equal(23, entry.Players.Count);
        Assert.NotNull(entry.TeamStats);
        Assert.NotNull(entry.Summary);
        Assert.Equal(31, entry.Summary!.HomePoints);
        Assert.Equal(17, entry.Summary.GuestPoints);
    }

    [Fact]
    public async Task CachedWindow_DegradeReadFiltersCompletedTeamFixtures()
    {
        var (service, _, _, _) = BuildService();
        await service.FillSquadWindowAsync(45047, 62, 20, "http://x");

        var season62 = await service.GetCachedWindowAsync(45047, 62, 20);
        Assert.Equal(21416928, Assert.Single(season62).FixtureId);

        Assert.Empty(await service.GetCachedWindowAsync(45047, 99, 20));
        Assert.Single(await service.GetCachedWindowAsync(45047, 0, 20));
    }

    [Fact]
    public async Task PlayerSeasons_RefreshOnlyWhenNewerCompletedFixtureExists()
    {
        var (service, api, _, _) = BuildService();
        api.PlayerStatsXml = TestXml.Load("dashboard-player-statistics.xml");
        var players = new[] { 16396178, 16728687 };

        var firstFixtureFinish = DateTime.UtcNow.AddHours(-2);
        var cold = await service.GetOrRefreshPlayerSeasonsAsync(players, 80, firstFixtureFinish);
        Assert.Equal(0, cold.Served);
        Assert.Equal(2, cold.Refreshed);
        Assert.Equal(2, api.PsCalls);
        Assert.Equal(1234, cold.Stats[16396178].Tackles); // grouped "1,234" parses cleanly

        var warm = await service.GetOrRefreshPlayerSeasonsAsync(players, 80, firstFixtureFinish);
        Assert.Equal(2, warm.Served);
        Assert.Equal(0, warm.Refreshed);
        Assert.Equal(2, api.PsCalls); // the warm budget: zero ps calls

        var nextFixtureFinish = DateTime.UtcNow.AddHours(1);
        var afterNewFixture = await service.GetOrRefreshPlayerSeasonsAsync(players, 80, nextFixtureFinish);
        Assert.Equal(0, afterNewFixture.Served);
        Assert.Equal(2, afterNewFixture.Refreshed);
        Assert.Equal(4, api.PsCalls); // the event-driven refresh fired for the new fixture
    }

    [Fact]
    public async Task PlayerSeasons_FailedRefreshKeepsCachedValuesAndCountsFailures()
    {
        var (service, api, _, _) = BuildService();
        api.PlayerStatsXml = TestXml.Load("dashboard-player-statistics.xml");
        var players = new[] { 16396178 };

        await service.GetOrRefreshPlayerSeasonsAsync(players, 80, DateTime.UtcNow.AddHours(-2));

        api.ThrowOnPlayerStats = true;
        var degraded = await service.GetOrRefreshPlayerSeasonsAsync(players, 80, DateTime.UtcNow.AddHours(1));

        Assert.Equal(1, degraded.Failed);
        Assert.Equal(0, degraded.Served);
        Assert.Equal(1234, degraded.Stats[16396178].Tackles); // the stale cached row survived
    }
    [Fact]
    public void SquadFixtureStats_CacheRowsMatchLiveParse_PerPlayerFields()
    {
        var adapter = TestXml.CreateAdapter();
        var squadFsXml = TestXml.Load("r3-fs-teamplayers-21416928.xml");
        var bareFsXml = TestXml.Load("r3-fs-bare-21416928.xml");

        var live = adapter.ParseFixturePlayerStatistics(squadFsXml, 45047);
        var playerRows = adapter.ParsePlayerFixtureStats(squadFsXml).Where(row => row.TeamId == 45047).ToList();
        var teamRow = adapter.ParseTeamFixtureStats(bareFsXml).Single(row => row.TeamId == 45047 && row.Half == "full");
        var cached = adapter.ToFixturePlayerStatistics(playerRows, teamRow);

        Assert.NotEmpty(live);
        Assert.Equal(live.Count, cached.Count);
        var liveById = live.ToDictionary(item => item.PlayerId);
        foreach (var cachedItem in cached)
        {
            var liveItem = liveById[cachedItem.PlayerId];
            Assert.Equal(liveItem.Tackles, cachedItem.Tackles);
            Assert.Equal(liveItem.MetresGained, cachedItem.MetresGained);
            Assert.Equal(liveItem.Tries, cachedItem.Tries);
            Assert.Equal(liveItem.Conversions, cachedItem.Conversions);
            Assert.Equal(liveItem.DropGoals, cachedItem.DropGoals);
            Assert.Equal(liveItem.Penalties, cachedItem.Penalties);
            Assert.Equal(liveItem.TotalPoints, cachedItem.TotalPoints);
            Assert.Equal(liveItem.YellowCards, cachedItem.YellowCards);
            Assert.Equal(liveItem.RedCards, cachedItem.RedCards);
            Assert.Equal(liveItem.Linebreaks, cachedItem.Linebreaks);
            Assert.Equal(liveItem.Intercepts, cachedItem.Intercepts);
            Assert.Equal(liveItem.Kicks, cachedItem.Kicks);
            Assert.Equal(liveItem.KnockOns, cachedItem.KnockOns);
            Assert.Equal(liveItem.ForwardPasses, cachedItem.ForwardPasses);
            Assert.Equal(liveItem.TryAssists, cachedItem.TryAssists);
            Assert.Equal(liveItem.BeatenDefenders, cachedItem.BeatenDefenders);
            Assert.Equal(liveItem.Injuries, cachedItem.Injuries);
            Assert.Equal(liveItem.HandlingErrors, cachedItem.HandlingErrors);
            Assert.Equal(liveItem.MissedTackles, cachedItem.MissedTackles);
            Assert.Equal(liveItem.Fights, cachedItem.Fights);
            Assert.Equal(liveItem.KickingMetres, cachedItem.KickingMetres);
            Assert.Equal(liveItem.PenaltiesConceded, cachedItem.PenaltiesConceded);
            Assert.Equal(liveItem.KicksOutOnTheFull, cachedItem.KicksOutOnTheFull);

            // The four lineout/scrum fields are not per-player fs fields (verified
            // against this archived XML): the live per-player parse reads them as
            // 0, while the cache sources the real values from the bare-fs team row.
            Assert.Equal(0, liveItem.LineoutsWon);
            Assert.Equal(teamRow.LineoutsWon, cachedItem.LineoutsWon);
            Assert.Equal(0, liveItem.LineoutsLost);
            Assert.Equal(teamRow.LineoutsLost, cachedItem.LineoutsLost);
            Assert.Equal(0, liveItem.ScrumWins);
            Assert.Equal(teamRow.ScrumsWon, cachedItem.ScrumWins);
            Assert.Equal(0, liveItem.ScrumLosses);
            Assert.Equal(teamRow.ScrumsLost, cachedItem.ScrumLosses);
        }
    }
}

/// <summary>The SnapshotStore's revived comparison (Q5/Q10): save → latest → delta.</summary>
public class SnapshotStoreTests
{
    private sealed class FakeEnvironment(string contentRoot) : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ApplicationName { get; set; } = "tests";
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = contentRoot;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static TeamDashboardViewModel BuildDashboard(int tackles) => new()
    {
        TeamId = 7,
        TeamName = "Test Team",
        Players = new[]
        {
            new PlayerDashboardItem
            {
                Id = 1,
                Name = "Player 1",
                Csr = 50,
                Salary = 100,
                Form = 7,
                Energy = 90,
                TotalPoints = 20,
                Tries = 2,
                Tackles = tackles,
                MetresGained = 30,
                TotalCaps = 5
            }
        }
    };

    [Fact]
    public async Task Snapshots_SaveLatestAndCompare()
    {
        var root = Path.Combine(Path.GetTempPath(), "snaptest-" + Guid.NewGuid().ToString("N"));
        var store = new SnapshotStore(new FakeEnvironment(root));

        await store.SaveSnapshotAsync(BuildDashboard(tackles: 10));
        Assert.Null(await store.GetLatestComparisonAsync(7)); // one snapshot, nothing to compare
        var latest = await store.GetLatestAsync(7);
        Assert.NotNull(latest);
        Assert.Equal(10, latest!.Players.Single().Tackles);

        await Task.Delay(1100); // snapshot filenames carry one-second resolution
        await store.SaveSnapshotAsync(BuildDashboard(tackles: 14));

        var comparison = await store.GetLatestComparisonAsync(7);
        Assert.NotNull(comparison);
        Assert.Equal(4, comparison!.PlayerChanges.Single().DeltaTackles);
    }
}

/// <summary>Cache-surfacing (Q6): cache entries are a distinct, honest log type.</summary>
public class ApiLoggerCacheTests
{
    [Fact]
    public void LogCache_TypedEntriesAppearInLogsAndJson()
    {
        var logger = new ApiLogger();
        logger.LogCache("match-cache/fixturestats?fixtureId=1", "23 rows");

        var entry = Assert.Single(logger.Logs);
        Assert.Equal("cache", entry.Type);
        Assert.Equal("Cache", entry.Method);
        Assert.Contains("\"type\":\"cache\"", logger.GetLogsJson());
    }
}