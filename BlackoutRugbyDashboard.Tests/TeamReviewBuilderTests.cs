using BlackoutRugbyDashboard.Data;
using BlackoutRugbyDashboard.Models;
using BlackoutRugbyDashboard.Services;
using Xunit;

namespace BlackoutRugbyDashboard.Tests;

/// <summary>
/// The Team review card's builder (S6, build slice #38): the locked eight Match
/// control charts, the share arithmetic (possession, territory and minutes-in-22
/// as shares of the Fixture total), the oldest-to-newest window, and points read
/// from our own perspective. The builder is pure — it takes the page's cached
/// window and returns charts — so "cache-only" is structural, not asserted.
/// </summary>
public class TeamReviewBuilderTests
{
    private const int TeamId = 45047;
    private const int OpponentId = 45037;

    private static readonly DateTime Day0 = new(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc);

    private static long Unix(int dayOffset) => new DateTimeOffset(Day0.AddDays(dayOffset)).ToUnixTimeSeconds();

    private static SquadFixtureRows Fixture(
        int fixtureId,
        int dayOffset,
        bool home,
        int homePoints,
        int guestPoints,
        TeamFixtureStatRow? ours,
        TeamFixtureStatRow? theirs) =>
        new(
            new FixtureRow
            {
                FixtureId = fixtureId,
                Season = 62,
                HomeTeamId = home ? TeamId : OpponentId,
                GuestTeamId = home ? OpponentId : TeamId,
                Competition = "League",
                Round = 1,
                MatchStartUnix = Unix(dayOffset),
                MatchFinishUnix = Unix(dayOffset) + 5400
            },
            new MatchSummaryRow { FixtureId = fixtureId, HomePoints = homePoints, GuestPoints = guestPoints },
            Array.Empty<PlayerFixtureRow>(),
            ours,
            theirs);

    private static TeamFixtureStatRow Stats(
        int teamId,
        int possession = 0,
        int territory = 0,
        int minutesIn22 = 0,
        int lineoutsWon = 0,
        int lineoutsLost = 0,
        int scrumsWon = 0,
        int scrumsLost = 0,
        int penaltiesConceded = 0,
        int penaltiesWon = 0,
        int turnovers = 0,
        int turnoversConceded = 0) =>
        new()
        {
            TeamId = teamId,
            Side = teamId == TeamId ? "home" : "guest",
            Half = "full",
            Possession = possession,
            Territory = territory,
            MinutesIn22 = minutesIn22,
            LineoutsWon = lineoutsWon,
            LineoutsLost = lineoutsLost,
            ScrumsWon = scrumsWon,
            ScrumsLost = scrumsLost,
            PenaltiesConceded = penaltiesConceded,
            PenaltiesWon = penaltiesWon,
            Turnovers = turnovers,
            TurnoversConceded = turnoversConceded
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
        null,
        new Dictionary<int, PlayerStatistics>());

    /// <summary>A window of one home win with a full set of both sides' team stats.</summary>
    private static SquadPageData OneGame() => Page(Fixture(
        1001, dayOffset: 0, home: true, homePoints: 31, guestPoints: 17,
        ours: Stats(TeamId, possession: 600, territory: 550, minutesIn22: 300, lineoutsWon: 9, lineoutsLost: 3,
            scrumsWon: 7, scrumsLost: 2, penaltiesConceded: 4, penaltiesWon: 6, turnovers: 5, turnoversConceded: 3),
        theirs: Stats(OpponentId, possession: 400, territory: 450, minutesIn22: 200, lineoutsWon: 5, lineoutsLost: 5,
            scrumsWon: 3, scrumsLost: 4, penaltiesConceded: 6, penaltiesWon: 4, turnovers: 3, turnoversConceded: 5)));

    [Fact]
    public void Tabs_StartWithMatchControlAndParseRoundTrips()
    {
        Assert.Equal(new[] { TeamReviewTab.MatchControl }, TeamReviewBuilder.Tabs);
        Assert.Equal(TeamReviewTab.MatchControl, TeamReviewBuilder.Parse(null));
        Assert.Equal(TeamReviewTab.MatchControl, TeamReviewBuilder.Parse("control"));
        Assert.Equal(TeamReviewTab.MatchControl, TeamReviewBuilder.Parse("nonsense"));

        var key = TeamReviewBuilder.KeyFor(TeamReviewBuilder.DefaultTab);
        Assert.Equal(key, TeamReviewBuilder.KeyFor(TeamReviewBuilder.Parse(key)));
    }

    [Fact]
    public void Build_ReturnsTheEightMatchControlChartsInTheLockedOrder()
    {
        var charts = TeamReviewBuilder.Build(OneGame(), TeamReviewTab.MatchControl);

        Assert.Equal(
            new[]
            {
                "Points for / against",
                "Possession %",
                "Territory %",
                "Lineout win %",
                "Scrum win %",
                "Penalties conceded / won",
                "Turnovers conceded / won",
                "Minutes in 22 %"
            },
            charts.Select(chart => chart.Title));
    }

    [Fact]
    public void Shares_AreComputedAsSharesOfTheFixtureTotal()
    {
        var charts = TeamReviewBuilder.Build(OneGame(), TeamReviewTab.MatchControl);

        Assert.Equal(60.0, Assert.Single(charts[1].Series).Values[0]); // 600 / (600 + 400)
        Assert.Equal(55.0, Assert.Single(charts[2].Series).Values[0]); // 550 / (550 + 450)
        Assert.Equal(60.0, Assert.Single(charts[7].Series).Values[0]); // 300 / (300 + 200)
        Assert.Equal(75.0, Assert.Single(charts[3].Series).Values[0]); // 9 / (9 + 3)
        Assert.Equal(77.8, Assert.Single(charts[4].Series).Values[0]); // 7 / (7 + 2), rounded
    }

    [Fact]
    public void ConcededAndWon_ReadFromOurOwnRowWithTheAdverseSideFirst()
    {
        var charts = TeamReviewBuilder.Build(OneGame(), TeamReviewTab.MatchControl);

        var penalties = charts[5].Series;
        Assert.Equal(ChartTone.Opponent, penalties[0].Tone);
        Assert.Equal(4.0, penalties[0].Values[0]); // our penalties conceded
        Assert.Equal(ChartTone.Us, penalties[1].Tone);
        Assert.Equal(6.0, penalties[1].Values[0]); // our penalties won

        var turnovers = charts[6].Series;
        Assert.Equal(3.0, turnovers[0].Values[0]); // our turnovers conceded
        Assert.Equal(5.0, turnovers[1].Values[0]); // our turnovers won
    }

    [Fact]
    public void Points_ReadFromOurPerspectiveRegardlessOfVenue()
    {
        var home = TeamReviewBuilder.Build(
            Page(Fixture(1001, 0, home: true, homePoints: 31, guestPoints: 17,
                ours: Stats(TeamId), theirs: Stats(OpponentId))),
            TeamReviewTab.MatchControl);
        Assert.Equal(31.0, home[0].Series[0].Values[0]);
        Assert.Equal(17.0, home[0].Series[1].Values[0]);

        var away = TeamReviewBuilder.Build(
            Page(Fixture(1002, 0, home: false, homePoints: 10, guestPoints: 24,
                ours: Stats(TeamId), theirs: Stats(OpponentId))),
            TeamReviewTab.MatchControl);
        Assert.Equal(24.0, away[0].Series[0].Values[0]);
        Assert.Equal(10.0, away[0].Series[1].Values[0]);
    }

    [Fact]
    public void Build_RunsOldestToNewest_WhateverOrderTheWindowArrivesIn()
    {
        // The window arrives newest-first (SquadPageReader), so the newest game is
        // first in the list; the charts must still read left-to-right by time.
        var page = Page(
            Fixture(1002, dayOffset: 7, home: true, homePoints: 40, guestPoints: 3,
                ours: Stats(TeamId), theirs: Stats(OpponentId)),
            Fixture(1001, dayOffset: 0, home: true, homePoints: 12, guestPoints: 20,
                ours: Stats(TeamId), theirs: Stats(OpponentId)));

        var charts = TeamReviewBuilder.Build(page, TeamReviewTab.MatchControl);

        Assert.Equal(new double?[] { 12.0, 40.0 }, charts[0].Series[0].Values);
        Assert.Equal(new double?[] { 20.0, 3.0 }, charts[0].Series[1].Values);
        Assert.Equal(2, charts[0].Labels.Count);
        Assert.True(string.CompareOrdinal(charts[0].Labels[0], charts[0].Labels[1]) < 0); // oldest first
    }

    /// <summary>A share with nothing to divide by is a gap, not a fabricated 100%.</summary>
    [Fact]
    public void MissingOpponentStats_LeaveAGapRatherThanAFabricatedShare()
    {
        var page = Page(Fixture(1001, 0, home: true, homePoints: 31, guestPoints: 17,
            ours: Stats(TeamId, possession: 600, territory: 550, minutesIn22: 300),
            theirs: null));

        var charts = TeamReviewBuilder.Build(page, TeamReviewTab.MatchControl);

        Assert.Null(Assert.Single(charts[1].Series).Values[0]); // possession
        Assert.Null(Assert.Single(charts[2].Series).Values[0]); // territory
        Assert.Null(Assert.Single(charts[7].Series).Values[0]); // minutes in 22
    }

    [Fact]
    public void ZeroTotal_IsAlsoAGap()
    {
        var page = Page(Fixture(1001, 0, home: true, homePoints: 0, guestPoints: 0,
            ours: Stats(TeamId),
            theirs: Stats(OpponentId)));

        var charts = TeamReviewBuilder.Build(page, TeamReviewTab.MatchControl);

        Assert.Null(Assert.Single(charts[1].Series).Values[0]);
    }

    [Fact]
    public void EmptyWindow_IsNoCharts()
    {
        Assert.Empty(TeamReviewBuilder.Build(Page(), TeamReviewTab.MatchControl));
    }
}
