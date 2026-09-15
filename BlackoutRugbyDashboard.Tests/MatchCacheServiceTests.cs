using BlackoutRugbyDashboard.Data;
using BlackoutRugbyDashboard.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BlackoutRugbyDashboard.Tests;

/// <summary>
/// Match Cache fill-service tests at the cache seam (spec Testing Decisions §3):
/// real SQLite (in-memory) + a fake API client + a temp raw directory. The
/// append-only rules (D1 §4) and the Home call budget (D5) are the contract.
/// </summary>
public class MatchCacheServiceTests
{
    private const string WindowXml = """
        <blackoutrugby_api_response season="62" round="3" day="2">
          <fixture id="900001"><id>900001</id><season>62</season><leagueid>349097</leagueid><round>2</round><hometeamid>45047</hometeamid><guestteamid>45037</guestteamid><competition>League</competition><botmatch>0</botmatch><weather>2</weather><data_removed>0</data_removed><stadium>0</stadium><country_iso>IE</country_iso><matchstart>1789220100</matchstart><matchfinish>1789226106</matchfinish></fixture>
          <fixture id="900002"><id>900002</id><season>62</season><leagueid>349097</leagueid><round>3</round><hometeamid>45037</hometeamid><guestteamid>45047</guestteamid><competition>League</competition><botmatch>0</botmatch><weather>1</weather><data_removed>0</data_removed><stadium>0</stadium><country_iso>IE</country_iso><matchstart>1789824900</matchstart></fixture>
        </blackoutrugby_api_response>
        """;

    private const string SingleFixtureXml = """
        <blackoutrugby_api_response season="62" round="3" day="2">
          <fixture id="900001"><id>900001</id><season>62</season><leagueid>349097</leagueid><round>2</round><hometeamid>45047</hometeamid><guestteamid>45037</guestteamid><competition>League</competition><botmatch>0</botmatch><weather>2</weather><data_removed>0</data_removed><stadium>0</stadium><country_iso>IE</country_iso><matchstart>1789220100</matchstart><matchfinish>1789226106</matchfinish></fixture>
        </blackoutrugby_api_response>
        """;

    private const string SummaryXml = """
        <blackoutrugby_api_response>
          <match_summary fixtureid="900001"><home><points>31</points><tries><player id="16728687"><id>16728687</id><number>3</number></player></tries><intensity>2</intensity></home><guest><points>17</points></guest><attendance><standing>100</standing><uncovered>50</uncovered></attendance><weather><id>1</id><night>1</night></weather></match_summary>
        </blackoutrugby_api_response>
        """;

    private sealed class FakeApi : IBlackoutRugbyApiClient
    {
        public int FixtureCalls, SummaryCalls, TeamCalls, FsCalls, LuCalls;

        public string TeamXml { get; set; } = TestXml.Load("r5-t-single-45037-fixed.xml");

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
            return Task.FromResult(TeamXml);
        }

        public Task<string> GetFixtureStatisticsAsync(int fixtureId, int? playerStats = null, int? teamPlayersStats = null)
        {
            FsCalls++;
            return Task.FromResult(teamPlayersStats.HasValue
                ? TestXml.Load("r3-fs-teamplayers-21416928.xml")
                : TestXml.Load("r3-fs-bare-21416928.xml"));
        }

        public Task<string> GetPlayerStatisticsAsync(int playerId) => throw new NotSupportedException("ps enters with the Player History slice (D7)");

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
        var rawRoot = Path.Combine(Path.GetTempPath(), "mctest-" + Guid.NewGuid().ToString("N"));
        var service = new MatchCacheService(api, new BlackoutRugbyResponseAdapter(), db, new MatchCacheRawStore(rawRoot));
        return (service, api, db, rawRoot);
    }

    [Fact]
    public async Task FillHomeWindow_CachesCompletedFixturesOnly_SkipsUnplayed()
    {
        var (service, api, db, _) = BuildService();

        var result = await service.FillHomeWindowAsync(45047);

        Assert.Equal(1, result.Completed);
        Assert.Equal(1, result.NewlyCached);
        Assert.Equal(3, result.ApiCalls);
        Assert.Equal(3, api.FixtureCalls + api.SummaryCalls + api.TeamCalls);
        Assert.NotNull(await db.Fixtures.SingleAsync(row => row.FixtureId == 900001));
        Assert.Empty(await db.Fixtures.Where(row => row.FixtureId == 900002).ToListAsync());
    }

    [Fact]
    public async Task FillHomeWindow_SummaryAndScorersAndTeamFactsAreCached()
    {
        var (service, _, db, _) = BuildService();

        await service.FillHomeWindowAsync(45047);

        var summary = await db.MatchSummaries.SingleAsync(row => row.FixtureId == 900001);
        Assert.Equal(31, summary.HomePoints);
        Assert.Equal(17, summary.GuestPoints);
        Assert.True(summary.WeatherNight);
        Assert.Equal(100, summary.Standing);
        var scorer = await db.MatchSummaryScorers.SingleAsync(row => row.FixtureId == 900001);
        Assert.Equal(3, scorer.Count);

        var teamFact = await db.TeamFacts.SingleAsync(row => row.TeamId == 45037);
        Assert.Equal("Pok78", teamFact.Name);
        Assert.False(teamFact.Bot);
        Assert.True(teamFact.AverageTop15Csr > 0);
    }

    [Fact]
    public async Task FillHomeWindow_SecondRefresh_ReFetchesCompletedFixturesNever()
    {
        var (service, api, db, _) = BuildService();

        await service.FillHomeWindowAsync(45047);
        await service.FillHomeWindowAsync(45047);

        Assert.Equal(2, api.FixtureCalls);
        Assert.Equal(1, api.SummaryCalls);
        Assert.Equal(1, await db.Fixtures.CountAsync());
        Assert.Equal(2, await db.TeamFacts.CountAsync());
    }

    [Fact]
    public async Task FillFixture_FullEntryScope_CachesAllReadsAndRawFiles()
    {
        var (service, api, db, rawRoot) = BuildService();

        var cached = await service.FillFixtureAsync(900001, 45047);

        Assert.True(cached);
        Assert.Equal(5, api.FixtureCalls + api.SummaryCalls + api.FsCalls + api.LuCalls);
        Assert.Equal(1, api.FixtureCalls);
        Assert.Equal(1, api.SummaryCalls);
        Assert.Equal(2, api.FsCalls);
        Assert.Equal(1, api.LuCalls);
        Assert.Equal(23, await db.PlayerFixtures.CountAsync());
        Assert.Equal(4, await db.TeamFixtureStats.CountAsync());
        Assert.Equal(1, await db.MatchSummaries.CountAsync());
        var rawFiles = Directory.GetFiles(Path.Combine(rawRoot, "raw"));
        Assert.Equal(5, rawFiles.Length);
        Assert.Contains(rawFiles, path => Path.GetFileName(path).StartsWith("fs-teamplayers-900001"));
    }

    [Fact]
    public async Task FillFixture_NeverReFetches_AndUnplayedNeverCached()
    {
        var (service, api, _, _) = BuildService();

        Assert.True(await service.FillFixtureAsync(900001, 45047));
        Assert.False(await service.FillFixtureAsync(900001, 45047));
        Assert.Equal(1, api.FixtureCalls);
        Assert.Equal(5, api.FixtureCalls + api.SummaryCalls + api.FsCalls + api.LuCalls);

        Assert.False(await service.FillFixtureAsync(900002, 45047));
        Assert.Equal(2, api.FixtureCalls);
    }
}
