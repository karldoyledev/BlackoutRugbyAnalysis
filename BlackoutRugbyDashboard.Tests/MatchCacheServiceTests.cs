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
        public int FixtureCalls, SummaryCalls, TeamCalls, FsCalls, LuCalls, MemberCalls;

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

        public Task<string> GetPlayersAsync(int? playerId = null, string? playerIds = null, int? teamId = null, string? teamIds = null, bool youth = false, bool nat = false, bool u20 = false) => throw new NotSupportedException("the roster read stays on the page (D-Squad); ps enters with the Squad slice");

        public int PsCalls;
        public string PlayerStatsXml { get; set; } = string.Empty;

        public Task<string> GetPlayerStatisticsAsync(int playerId) => GetPlayerStatisticsAsync(playerId, null);

        public Task<string> GetPlayerStatisticsAsync(int playerId, int? season)
        {
            PsCalls++;
            return Task.FromResult(PlayerStatsXml);
        }

        public Task<string> GetMemberAsync(int memberId)
        {
            MemberCalls++;
            return Task.FromResult(TestXml.Load("r2-m-memberid.xml"));
        }

        public Task<string> GetLineupsAsync(int teamId, int? fixtureId = null, string? fixtureIds = null, bool youth = false, bool nat = false, bool u20 = false)
        {
            LuCalls++;
            return Task.FromResult(TestXml.Load("r1-lineups-lu-fixture-21416928.xml"));
        }

        public Task<string> GetStandingsAsync(int? leagueId = null, bool youth = false, bool nat = false, bool u20 = false, int? season = null) => throw new NotSupportedException();
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

    [Fact]
    public async Task ResetTable_Fixtures_WipesDerivedRowsAndRawArchive()
    {
        var (service, _, db, rawRoot) = BuildService();
        await service.FillFixtureAsync(900001, 45047);

        var deleted = await service.ResetTableAsync(CacheTable.Fixtures);

        Assert.True(deleted > 0);
        Assert.Empty(await db.Fixtures.ToListAsync());
        Assert.Empty(await db.PlayerFixtures.ToListAsync());
        Assert.Empty(await db.TeamFixtureStats.ToListAsync());
        Assert.Empty(await db.MatchSummaries.ToListAsync());
        Assert.Empty(await db.MatchSummaryScorers.ToListAsync());
        Assert.Empty(Directory.GetFiles(Path.Combine(rawRoot, "raw")));
    }

    [Fact]
    public async Task ResetTable_Fixtures_ThenRefill_AppendOnlyRulesStillHold()
    {
        var (service, api, db, _) = BuildService();
        await service.FillHomeWindowAsync(45047);
        await service.ResetTableAsync(CacheTable.Fixtures);

        await service.FillHomeWindowAsync(45047);

        Assert.Equal(1, await db.Fixtures.CountAsync());
        Assert.Equal(1, await db.MatchSummaries.CountAsync());
        Assert.Equal(2, api.FixtureCalls);
        Assert.Equal(2, api.SummaryCalls);
    }

    [Fact]
    public async Task ResetTable_MatchSummaries_TakesScorers_LeavesFixtures()
    {
        var (service, _, db, _) = BuildService();
        await service.FillFixtureAsync(900001, 45047);

        await service.ResetTableAsync(CacheTable.MatchSummaries);

        Assert.Equal(1, await db.Fixtures.CountAsync());
        Assert.Equal(23, await db.PlayerFixtures.CountAsync());
        Assert.Empty(await db.MatchSummaries.ToListAsync());
        Assert.Empty(await db.MatchSummaryScorers.ToListAsync());
    }

    [Fact]
    public async Task ResetTable_TeamFacts_ClearsOnlyThatTable_AndIsRecapturedOnTheNextRefresh()
    {
        var (service, api, db, _) = BuildService();
        await service.FillHomeWindowAsync(45047);
        Assert.Equal(1, await db.TeamFacts.CountAsync());

        await service.ResetTableAsync(CacheTable.TeamFacts);

        Assert.Empty(await db.TeamFacts.ToListAsync());
        Assert.Equal(1, await db.Fixtures.CountAsync());

        await service.FillHomeWindowAsync(45047);

        Assert.Equal(1, await db.TeamFacts.CountAsync());
        Assert.Equal(2, api.TeamCalls);
    }

    [Fact]
    public async Task FillFixtureAnalysis_ColdFixture_FillsTheFullEntryScope()
    {
        var (service, api, _, _) = BuildService();

        var calls = await service.FillFixtureAnalysisAsync(900001, 45047);

        Assert.Equal(5, calls);
        Assert.Equal(5, api.FixtureCalls + api.SummaryCalls + api.FsCalls + api.LuCalls);
    }

    [Fact]
    public async Task FillFixtureAnalysis_WarmFixture_FillsOnlyTheMissingAnalysisScope()
    {
        var (service, api, _, _) = BuildService();
        await service.FillHomeWindowAsync(45047); // f + ms + t — the Home window fill
        var fsBefore = api.FsCalls;
        var luBefore = api.LuCalls;

        var calls = await service.FillFixtureAnalysisAsync(900001, 45047);

        // The analysis adds the squad fs, the bare fs and the lu — nothing else.
        Assert.Equal(3, calls);
        Assert.Equal(2, api.FsCalls - fsBefore);
        Assert.Equal(1, api.LuCalls - luBefore);

        // The little the analysis never re-reads: the archived `lu` (and the fixture /
        // summary Home already held) are untouched by a second fill. The `fs` rows the
        // FakeApi returns carry the artifact's own fixture id, so the fs archive itself
        // is out of scope here — its append-only rule is FillFixture_NeverReFetches'.
        await service.FillFixtureAnalysisAsync(900001, 45047);
        Assert.Equal(1, api.LuCalls - luBefore);
        Assert.Equal(1, api.FixtureCalls);
        Assert.Equal(1, api.SummaryCalls);
    }

    [Fact]
    public async Task GetCachedLineup_ParsesTheArchivedLu_WithNoApiCall()
    {
        var (service, _, _, _) = BuildService();
        await service.FillFixtureAnalysisAsync(900001, 45047);

        var lineup = service.GetCachedLineup(900001, 45047);

        Assert.NotNull(lineup);
        Assert.Equal(45047, lineup!.TeamId);
        Assert.Equal(15, lineup.Xv.Count);
        Assert.Equal(8, lineup.Bench.Count);
        Assert.Equal(16398979, lineup.CaptainId);
    }

    [Fact]
    public void GetCachedLineup_WithNoArchive_ReturnsNull()
    {
        var (service, _, _, _) = BuildService();

        Assert.Null(service.GetCachedLineup(900001, 45047));
    }

    [Fact]
    public async Task GetPlayerFixtureHistory_JoinsFixturesAndSummaries_NewestFirst()
    {
        var (service, _, db, _) = BuildService();
        db.Fixtures.Add(new FixtureRow
        {
            FixtureId = 1, HomeTeamId = 45047, GuestTeamId = 45037, Competition = "League", Round = 1,
            Season = 62, MatchStartUnix = 100, MatchFinishUnix = 200
        });
        db.Fixtures.Add(new FixtureRow
        {
            FixtureId = 2, HomeTeamId = 45037, GuestTeamId = 45047, Competition = "League", Round = 2,
            Season = 62, MatchStartUnix = 300, MatchFinishUnix = 400
        });
        db.PlayerFixtures.Add(new PlayerFixtureRow { FixtureId = 1, TeamId = 45047, PlayerId = 99, Slot = 1 });
        db.PlayerFixtures.Add(new PlayerFixtureRow { FixtureId = 2, TeamId = 45047, PlayerId = 99, Slot = 1 });
        db.MatchSummaries.Add(new MatchSummaryRow { FixtureId = 1, HomePoints = 20, GuestPoints = 10 });
        db.MatchSummaries.Add(new MatchSummaryRow { FixtureId = 2, HomePoints = 5, GuestPoints = 15 });
        await db.SaveChangesAsync();

        var history = await service.GetPlayerFixtureHistoryAsync(99);

        Assert.Equal(2, history.Count);
        Assert.Equal(2, history[0].Fixture.FixtureId); // newest first
        Assert.False(history[0].IsHome);
        Assert.Equal(15, history[0].Score);
        Assert.Equal(5, history[0].OppositionScore);
        Assert.True(history[1].IsHome);
        Assert.Equal(20, history[1].Score);
    }

    [Fact]
    public async Task GetPlayerFixtureHistory_UnknownPlayer_IsEmpty()
    {
        var (service, _, _, _) = BuildService();

        Assert.Empty(await service.GetPlayerFixtureHistoryAsync(123456));
    }

    [Fact]
    public async Task GetLatestCachedFixture_ReturnsSeasonAndFinish_OrNullWhenEmpty()
    {
        var (service, _, db, _) = BuildService();

        Assert.Null(await service.GetLatestCachedFixtureAsync(45047));

        db.Fixtures.Add(new FixtureRow
        {
            FixtureId = 1, HomeTeamId = 45047, GuestTeamId = 45037, Season = 61,
            MatchStartUnix = 100, MatchFinishUnix = 200
        });
        db.Fixtures.Add(new FixtureRow
        {
            FixtureId = 2, HomeTeamId = 45037, GuestTeamId = 45047, Season = 62,
            MatchStartUnix = 300, MatchFinishUnix = 400
        });
        await db.SaveChangesAsync();

        var latest = await service.GetLatestCachedFixtureAsync(45047);

        Assert.NotNull(latest);
        Assert.Equal(62, latest!.Value.Season);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(400).UtcDateTime, latest.Value.FinishUtc);
    }
}
