using System.Security.Claims;
using BlackoutRugbyDashboard.Data;
using BlackoutRugbyDashboard.Models;
using BlackoutRugbyDashboard.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlackoutRugbyDashboard.Tests;

/// <summary>
/// Club Link tests at the account seam (D3): the link state machine and the one
/// r=m validation probe, against a real SQLite (in-memory) store, an ephemeral
/// Data Protection provider, and a fake API client — no live API, no machine keys.
/// </summary>
public class ClubLinkServiceTests
{
    private const string MemberKey = "test-member-key-0123456789abcdef";

    private sealed class FakeApi : IBlackoutRugbyApiClient
    {
        public int? ProbedMemberId;
        public string MemberXml { get; set; } = TestXml.Load("r2-m-memberid.xml");
        public Exception? Throw { get; set; }

        public Task<string> GetMemberAsync(int memberId)
        {
            ProbedMemberId = memberId;
            if (Throw is not null)
            {
                throw Throw;
            }

            return Task.FromResult(MemberXml);
        }

        public Task<string> GetFixturesAsync(int? fixtureId = null, string? fixtureIds = null, int? teamId = null, int? last = null, int? future = null, int? past = null, int? latest = null, int? leagueId = null, int? season = null, int? round = null, bool roundRobin = false, int? friendlyCompId = null, bool youth = false, bool nat = false, bool u20 = false) => throw new NotSupportedException();

        public Task<string> GetMatchSummaryAsync(int? fixtureId = null, string? fixtureIds = null, bool youth = false, bool nat = false, bool u20 = false) => throw new NotSupportedException();

        public Task<string> GetTeamsAsync(int? teamId = null, string? teamIds = null, int? regionId = null, int? leagueId = null, bool nat = false, bool u20 = false, string? country = null) => throw new NotSupportedException();

        public Task<string> GetFixtureStatisticsAsync(int fixtureId, int? playerStats = null, int? teamPlayersStats = null) => throw new NotSupportedException();

        public Task<string> GetPlayerStatisticsAsync(int playerId) => throw new NotSupportedException();

        public Task<string> GetLineupsAsync(int teamId, int? fixtureId = null, string? fixtureIds = null, bool youth = false, bool nat = false, bool u20 = false) => throw new NotSupportedException();
    }

    private sealed class FakeFactory : IBlackoutRugbyApiClientFactory
    {
        private readonly Func<FakeApi> _apiFactory;

        public FakeFactory(Func<FakeApi> apiFactory) => _apiFactory = apiFactory;

        public FakeApi Last { get; private set; } = new();

        public IBlackoutRugbyApiClient CreateDeveloperOnly() => Build();

        public IBlackoutRugbyApiClient CreateForMember(int memberId, string memberKey) => Build();

        public BlackoutRugby.Api.BlackoutRugbyApiClient CreateFullSurface(int? memberId = null, string? memberKey = null) => throw new NotSupportedException();

        private IBlackoutRugbyApiClient Build()
        {
            Last = _apiFactory();
            return Last;
        }
    }

    private sealed class Harness
    {
        public DashboardDbContext Db { get; }
        public FakeApi Api { get; }
        public FakeFactory Factory { get; }
        public ClubLinkService Service { get; }
        public MemberKeyProtector Protector { get; }

        public Harness(
            bool signedIn = true,
            bool memberTakenByOther = false,
            int developerId = 1333,
            string developerKey = "dev-key",
            string developerIv = "dev-iv")
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
            var options = new DbContextOptionsBuilder<DashboardDbContext>().UseSqlite(connection).Options;
            Db = new DashboardDbContext(options);
            Db.Database.EnsureCreated();

            Db.Users.Add(new DashboardUser { Id = "user-1", UserName = "one@test.local", Email = "one@test.local", LockoutEnabled = true });
            if (memberTakenByOther)
            {
                Db.Users.Add(new DashboardUser { Id = "user-2", UserName = "two@test.local", Email = "two@test.local", MemberId = 202665, TeamId = 45047, EncryptedMemberKey = "taken", LinkedAt = DateTime.UtcNow });
            }

            Db.SaveChanges();

            var identity = signedIn
                ? new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "user-1") }, "TestAuth")
                : new ClaimsIdentity();
            var http = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
            var accessor = new HttpContextAccessor { HttpContext = http };

            Protector = new MemberKeyProtector(new EphemeralDataProtectionProvider());
            Api = new FakeApi();
            Factory = new FakeFactory(() => Api);
            Service = new ClubLinkService(
                Db,
                Factory,
                Protector,
                accessor,
                Options.Create(new DeveloperOptions { DeveloperId = developerId, DeveloperKey = developerKey, DeveloperIV = developerIv }),
                new BlackoutRugbyResponseAdapter());
        }
    }

    [Fact]
    public async Task LinkAsync_WithValidMember_BindsLinkAndEncryptsKey()
    {
        var harness = new Harness();

        var result = await harness.Service.LinkAsync(202665, MemberKey);

        Assert.True(result.Success);
        Assert.Equal(ClubLinkStatus.Linked, result.Status);
        Assert.Equal(45047, result.TeamId);
        Assert.Equal("Doyleski", result.Username);
        Assert.Equal(202665, harness.Api.ProbedMemberId);

        var user = await harness.Db.Users.SingleAsync(row => row.Id == "user-1");
        Assert.Equal(202665, user.MemberId);
        Assert.Equal(45047, user.TeamId);
        Assert.NotNull(user.LinkedAt);
        Assert.NotNull(user.EncryptedMemberKey);
        Assert.NotEqual(MemberKey, user.EncryptedMemberKey);
        Assert.Equal(MemberKey, harness.Protector.Unprotect(user.EncryptedMemberKey));
    }

    [Fact]
    public async Task LinkAsync_RejectedKey_ClassifiesWrongMemberKeyAndPersistsNothing()
    {
        var harness = new Harness();
        harness.Api.MemberXml = "<blackoutrugby_api_response><error>Invalid member key</error></blackoutrugby_api_response>";

        var result = await harness.Service.LinkAsync(202665, "wrong-key");

        Assert.False(result.Success);
        Assert.Equal(ClubLinkStatus.WrongMemberKey, result.Status);
        Assert.Equal(202665, harness.Api.ProbedMemberId);
        var user = await harness.Db.Users.SingleAsync(row => row.Id == "user-1");
        Assert.Null(user.MemberId);
        Assert.Null(user.EncryptedMemberKey);
    }

    [Fact]
    public async Task LinkAsync_UnknownMemberId_ClassifiesUnknownMemberId()
    {
        var harness = new Harness();
        harness.Api.MemberXml = "<blackoutrugby_api_response><error>No member found</error></blackoutrugby_api_response>";

        var result = await harness.Service.LinkAsync(999999, MemberKey);

        Assert.Equal(ClubLinkStatus.UnknownMemberId, result.Status);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task LinkAsync_UnrecognizedApiError_CarriesTheRawText()
    {
        var harness = new Harness();
        harness.Api.MemberXml = "<blackoutrugby_api_response><error>Something unexpected</error></blackoutrugby_api_response>";

        var result = await harness.Service.LinkAsync(202665, MemberKey);

        Assert.Equal(ClubLinkStatus.ApiRejected, result.Status);
        Assert.Contains("Something unexpected", result.Message);
    }

    [Fact]
    public async Task LinkAsync_TransientNoDataRequested_MapsToTheRetryableUnreachableClass()
    {
        var harness = new Harness();
        harness.Api.MemberXml = "<blackoutrugby_api_response><error>No data requested</error></blackoutrugby_api_response>";

        var result = await harness.Service.LinkAsync(202665, MemberKey);

        Assert.Equal(ClubLinkStatus.ApiUnreachable, result.Status);
    }

    [Fact]
    public async Task LinkAsync_ApiUnreachable_MapsToApiUnreachable()
    {
        var harness = new Harness();
        harness.Api.Throw = new HttpRequestException("connection refused");

        var result = await harness.Service.LinkAsync(202665, MemberKey);

        Assert.Equal(ClubLinkStatus.ApiUnreachable, result.Status);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task LinkAsync_MemberAlreadyLinkedByAnotherUser_RefusedBeforeAnyProbe()
    {
        var harness = new Harness(memberTakenByOther: true);

        var result = await harness.Service.LinkAsync(202665, MemberKey);

        Assert.Equal(ClubLinkStatus.MemberAlreadyLinked, result.Status);
        Assert.Null(harness.Api.ProbedMemberId);
    }

    [Fact]
    public async Task LinkAsync_DeveloperCredentialsMissing_RefusedBeforeAnyProbe()
    {
        var harness = new Harness(developerKey: "");

        var result = await harness.Service.LinkAsync(202665, MemberKey);

        Assert.Equal(ClubLinkStatus.DeveloperCredentialsMissing, result.Status);
        Assert.Null(harness.Api.ProbedMemberId);
    }

    [Fact]
    public async Task LinkAsync_BlankInput_RefusedBeforeAnyProbe()
    {
        var harness = new Harness();

        var blankKey = await harness.Service.LinkAsync(202665, "   ");
        var zeroId = await harness.Service.LinkAsync(0, MemberKey);

        Assert.Equal(ClubLinkStatus.BlankInput, blankKey.Status);
        Assert.Equal(ClubLinkStatus.BlankInput, zeroId.Status);
        Assert.Null(harness.Api.ProbedMemberId);
    }

    [Fact]
    public async Task LinkAsync_NotSignedIn_Refused()
    {
        var harness = new Harness(signedIn: false);

        var result = await harness.Service.LinkAsync(202665, MemberKey);

        Assert.Equal(ClubLinkStatus.NotSignedIn, result.Status);
        Assert.Null(harness.Api.ProbedMemberId);
    }

    [Fact]
    public async Task LinkAsync_ReLinkWithRotatedKey_ReEncryptsTheKey()
    {
        var harness = new Harness();
        await harness.Service.LinkAsync(202665, MemberKey);

        var result = await harness.Service.LinkAsync(202665, "rotated-key");

        Assert.True(result.Success);
        var user = await harness.Db.Users.SingleAsync(row => row.Id == "user-1");
        Assert.Equal("rotated-key", harness.Protector.Unprotect(user.EncryptedMemberKey));
    }

    [Fact]
    public async Task UnlinkAsync_WipesMemberFields_KeepsAccountAndMatchCache()
    {
        var harness = new Harness();
        await harness.Service.LinkAsync(202665, MemberKey);
        harness.Db.Fixtures.Add(new FixtureRow { FixtureId = 900001, Season = 62, LeagueId = 1, Round = 2, HomeTeamId = 45047, GuestTeamId = 45037, Competition = "League", BotMatch = 0, WeatherId = 2, DataRemoved = 0, Stadium = "0", CountryIso = "IE", MatchStartUnix = 1789220100, MatchFinishUnix = 1789226106 });
        harness.Db.TeamFacts.Add(new TeamFactRow { TeamId = 45047, CapturedAt = DateTime.UtcNow, Name = "Doylester", Bot = false, AverageTop15Csr = 210000, LeagueId = 349097 });
        await harness.Db.SaveChangesAsync();

        await harness.Service.UnlinkAsync();

        var user = await harness.Db.Users.SingleAsync(row => row.Id == "user-1");
        Assert.Null(user.MemberId);
        Assert.Null(user.TeamId);
        Assert.Null(user.EncryptedMemberKey);
        Assert.Null(user.LinkedAt);
        Assert.Equal("one@test.local", user.UserName);
        Assert.Equal(1, await harness.Db.Fixtures.CountAsync());
        Assert.Equal(1, await harness.Db.TeamFacts.CountAsync());
    }

    [Fact]
    public async Task TheStore_ForbidsTwoUsersOnOneMember()
    {
        var harness = new Harness();
        var first = await harness.Db.Users.SingleAsync(row => row.Id == "user-1");
        first.MemberId = 202665;
        first.TeamId = 45047;
        first.EncryptedMemberKey = "enc";
        first.LinkedAt = DateTime.UtcNow;
        await harness.Db.SaveChangesAsync();

        harness.Db.Users.Add(new DashboardUser { Id = "user-3", UserName = "three@test.local", Email = "three@test.local", MemberId = 202665 });

        await Assert.ThrowsAsync<DbUpdateException>(() => harness.Db.SaveChangesAsync());
    }

    [Fact]
    public async Task ResolveCurrentMemberCredentials_FollowsTheLinkState()
    {
        var harness = new Harness();

        Assert.Null(harness.Service.ResolveCurrentMemberCredentials());
        Assert.False(harness.Service.IsLinked);

        await harness.Service.LinkAsync(202665, MemberKey);

        var credentials = harness.Service.ResolveCurrentMemberCredentials();
        Assert.NotNull(credentials);
        Assert.Equal(202665, credentials!.MemberId);
        Assert.Equal(45047, credentials.TeamId);
        Assert.Equal(MemberKey, credentials.MemberKey);
        Assert.True(harness.Service.IsLinked);
    }

    [Fact]
    public void ResolveCurrentMemberCredentials_Anonymous_YieldsNothing()
    {
        var harness = new Harness(signedIn: false);

        Assert.Null(harness.Service.ResolveCurrentMemberCredentials());
        Assert.False(harness.Service.IsLinked);
    }

    [Fact]
    public async Task GetLinkState_ForLinkedUser_CarriesTheCachedTeamName()
    {
        var harness = new Harness();
        await harness.Service.LinkAsync(202665, MemberKey);

        Assert.Null(harness.Service.GetLinkState()?.TeamName);

        harness.Db.TeamFacts.Add(new TeamFactRow { TeamId = 45047, CapturedAt = DateTime.UtcNow, Name = "Doylester", Bot = false, AverageTop15Csr = 210000, LeagueId = 349097 });
        await harness.Db.SaveChangesAsync();

        var named = harness.Service.GetLinkState();
        Assert.NotNull(named);
        Assert.Equal(202665, named!.MemberId);
        Assert.Equal(45047, named.TeamId);
        Assert.Equal("Doylester", named.TeamName);
    }

    [Fact]
    public void GetLinkState_Unlinked_YieldsNothing()
    {
        var harness = new Harness();

        Assert.Null(harness.Service.GetLinkState());
    }
}