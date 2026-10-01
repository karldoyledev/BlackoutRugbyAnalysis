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
        TeamFixtureStatRow? theirs,
        TeamFixtureStatRow? firstHalf = null) =>
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
            theirs,
            firstHalf);

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
        int turnoversConceded = 0,
        int tackles = 0,
        int tries = 0,
        int metresGained = 0,
        int linebreaks = 0,
        int phases = 0,
        int sevenplusPhases = 0,
        int rucksWon = 0,
        int maulsWon = 0,
        int kickingMetres = 0,
        int missedTackles = 0,
        int knockons = 0,
        int forwardPasses = 0,
        int handlingErrors = 0,
        int kicks = 0,
        int badKicks = 0,
        int kicksOutOnTheFull = 0,
        int badUpAndUnders = 0,
        int conversions = 0,
        int missedConversions = 0,
        int injuries = 0,
        int injuryBreaks = 0,
        int totalPoints = 0) =>
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
            TurnoversConceded = turnoversConceded,
            Tackles = tackles,
            Tries = tries,
            MetresGained = metresGained,
            Linebreaks = linebreaks,
            Phases = phases,
            SevenplusPhases = sevenplusPhases,
            RucksWon = rucksWon,
            MaulsWon = maulsWon,
            KickingMetres = kickingMetres,
            MissedTackles = missedTackles,
            Knockons = knockons,
            ForwardPasses = forwardPasses,
            HandlingErrors = handlingErrors,
            Kicks = kicks,
            BadKicks = badKicks,
            KicksOutOnTheFull = kicksOutOnTheFull,
            BadUpAndUnders = badUpAndUnders,
            Conversions = conversions,
            MissedConversions = missedConversions,
            Injuries = injuries,
            InjuryBreaks = injuryBreaks,
            TotalPoints = totalPoints
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
    public void Tabs_LeadWithMatchControlAndParseRoundTrips()
    {
        Assert.Equal(new[] { TeamReviewTab.MatchControl, TeamReviewTab.Attack, TeamReviewTab.Defence }, TeamReviewBuilder.Tabs);
        Assert.Equal(TeamReviewTab.MatchControl, TeamReviewBuilder.Parse(null));
        Assert.Equal(TeamReviewTab.MatchControl, TeamReviewBuilder.Parse("control"));
        Assert.Equal(TeamReviewTab.Attack, TeamReviewBuilder.Parse("attack"));
        Assert.Equal(TeamReviewTab.Defence, TeamReviewBuilder.Parse("defence"));
        Assert.Equal(TeamReviewTab.MatchControl, TeamReviewBuilder.Parse("nonsense"));

        foreach (var tab in TeamReviewBuilder.Tabs)
        {
            var key = TeamReviewBuilder.KeyFor(tab);
            Assert.Equal(key, TeamReviewBuilder.KeyFor(TeamReviewBuilder.Parse(key)));
        }
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

    // ---------------------------------------------------------------------
    // The Attack tab (S6 / #39): eight attacking outputs, ours vs theirs
    // ---------------------------------------------------------------------

    [Fact]
    public void Build_ReturnsTheEightAttackChartsInTheLockedOrder()
    {
        var charts = TeamReviewBuilder.Build(
            Page(Fixture(1001, 0, home: true, homePoints: 20, guestPoints: 10,
                ours: Stats(TeamId), theirs: Stats(OpponentId))),
            TeamReviewTab.Attack);

        Assert.Equal(
            new[]
            {
                "Tries for / against",
                "Metres gained for / against",
                "Linebreaks for / against",
                "Phases for / against",
                "7+ phases for / against",
                "Rucks won for / against",
                "Mauls won for / against",
                "Kicking metres for / against"
            },
            charts.Select(chart => chart.Title));
        Assert.All(charts, chart => Assert.Equal(2, chart.Series.Count));
    }

    [Fact]
    public void Attack_ReadsOurSideAndTheirsOldestToNewest()
    {
        var page = Page(
            Fixture(1002, dayOffset: 7, home: true, homePoints: 30, guestPoints: 10,
                ours: Stats(TeamId, tries: 4, metresGained: 500, kickingMetres: 300),
                theirs: Stats(OpponentId, tries: 1, metresGained: 250, kickingMetres: 210)),
            Fixture(1001, dayOffset: 0, home: false, homePoints: 12, guestPoints: 22,
                ours: Stats(TeamId, tries: 2, metresGained: 300, kickingMetres: 150),
                theirs: Stats(OpponentId, tries: 3, metresGained: 410, kickingMetres: 260)));

        var charts = TeamReviewBuilder.Build(page, TeamReviewTab.Attack);

        var tries = charts[0];
        Assert.Equal(ChartTone.Us, tries.Series[0].Tone);
        Assert.Equal(new double?[] { 2, 4 }, tries.Series[0].Values); // ours, oldest first
        Assert.Equal(ChartTone.Opponent, tries.Series[1].Tone);
        Assert.Equal(new double?[] { 3, 1 }, tries.Series[1].Values); // theirs

        Assert.Equal(new double?[] { 300, 500 }, charts[1].Series[0].Values); // metres gained
        Assert.Equal(new double?[] { 150, 300 }, charts[7].Series[0].Values); // kicking metres
    }

    /// <summary>A Fixture with no cached row for the opponent is a gap, never a zero.</summary>
    [Fact]
    public void Attack_MissingOpponentRow_IsAGapNotAZero()
    {
        var page = Page(Fixture(1001, 0, home: true, homePoints: 20, guestPoints: 10,
            ours: Stats(TeamId, tries: 3), theirs: null));

        var tries = TeamReviewBuilder.Build(page, TeamReviewTab.Attack)[0];

        Assert.Equal(new double?[] { 3 }, tries.Series[0].Values);
        Assert.Equal(new double?[] { null }, tries.Series[1].Values);
    }

    [Fact]
    public void Attack_EmptyWindow_IsNoCharts()
    {
        Assert.Empty(TeamReviewBuilder.Build(Page(), TeamReviewTab.Attack));
    }

    // ---------------------------------------------------------------------
    // The Defence, discipline & kicking tab (S6 / #40)
    // ---------------------------------------------------------------------

    [Fact]
    public void Build_ReturnsTheEightDefenceChartsInTheLockedOrder()
    {
        var charts = TeamReviewBuilder.Build(
            Page(Fixture(1001, 0, home: true, homePoints: 20, guestPoints: 10,
                ours: Stats(TeamId), theirs: Stats(OpponentId))),
            TeamReviewTab.Defence);

        Assert.Equal(
            new[]
            {
                "Tackle completion %",
                "Missed tackles for / against",
                "Sloppiness (knock-ons + forward passes + handling errors)",
                "Kick accuracy %",
                "Needless kicks (bad up-and-unders + kicks out on the full)",
                "Shots at goal: conversions vs missed",
                "Injuries / injury breaks",
                "1st half vs 2nd half points"
            },
            charts.Select(chart => chart.Title));
    }

    /// <summary>Every composite is a share or a sum of the locked cached fields:
    /// completion and kick accuracy as shares, sloppiness and needless kicks as
    /// sums — never an invented field.</summary>
    [Fact]
    public void Defence_ComputesCompositesFromTheLockedFields()
    {
        var page = Page(Fixture(1001, 0, home: true, homePoints: 20, guestPoints: 10,
            ours: Stats(TeamId, tackles: 90, missedTackles: 10,
                kicks: 20, badKicks: 3, kicksOutOnTheFull: 2, badUpAndUnders: 5,
                knockons: 4, forwardPasses: 3, handlingErrors: 2),
            theirs: Stats(OpponentId, tackles: 80, missedTackles: 20,
                kicksOutOnTheFull: 1, badUpAndUnders: 2,
                knockons: 1, forwardPasses: 1, handlingErrors: 1)));

        var charts = TeamReviewBuilder.Build(page, TeamReviewTab.Defence);

        var completion = charts[0];
        Assert.Equal(ChartTone.Us, completion.Series[0].Tone);
        Assert.Equal(90.0, completion.Series[0].Values[0]); // 90 / (90 + 10)
        Assert.Equal(ChartTone.Opponent, completion.Series[1].Tone);
        Assert.Equal(80.0, completion.Series[1].Values[0]); // 80 / (80 + 20)

        var sloppiness = charts[2];
        Assert.Equal(3.0, sloppiness.Series[0].Values[0]); // opponent: 1 + 1 + 1
        Assert.Equal(9.0, sloppiness.Series[1].Values[0]); // ours: 4 + 3 + 2

        var accuracy = Assert.Single(charts[3].Series);
        Assert.Equal(75.0, accuracy.Values[0]); // (20 - 3 - 2) / 20

        var needless = charts[4];
        Assert.Equal(3.0, needless.Series[0].Values[0]); // opponent: 2 + 1
        Assert.Equal(7.0, needless.Series[1].Values[0]); // ours: 5 + 2
    }

    /// <summary>The half split reads the cached first-half row; the second half is
    /// the full-time total minus it.</summary>
    [Fact]
    public void Defence_HalfSplitReadsTheCachedFirstHalfRow()
    {
        var page = Page(Fixture(1001, 0, home: true, homePoints: 31, guestPoints: 17,
            ours: Stats(TeamId, totalPoints: 31),
            theirs: Stats(OpponentId),
            firstHalf: Stats(TeamId, totalPoints: 13)));

        var halves = TeamReviewBuilder.Build(page, TeamReviewTab.Defence)[7];

        Assert.Equal("2nd half", halves.Series[0].Label);
        Assert.Equal(18.0, halves.Series[0].Values[0]); // 31 - 13
        Assert.Equal("1st half", halves.Series[1].Label);
        Assert.Equal(13.0, halves.Series[1].Values[0]);
    }

    [Fact]
    public void Defence_MissingRows_AreGapsNotZeros()
    {
        var page = Page(Fixture(1001, 0, home: true, homePoints: 20, guestPoints: 10,
            ours: Stats(TeamId, tackles: 10),
            theirs: null));

        var charts = TeamReviewBuilder.Build(page, TeamReviewTab.Defence);

        Assert.Equal(100.0, charts[0].Series[0].Values[0]); // ours
        Assert.Null(charts[0].Series[1].Values[0]);         // no opponent row
        Assert.Null(charts[2].Series[0].Values[0]);         // opponent sloppiness
        Assert.Null(charts[7].Series[0].Values[0]);         // no first-half row => 2nd half gap
        Assert.Null(charts[7].Series[1].Values[0]);         // 1st half gap
    }

    [Fact]
    public void Defence_EmptyWindow_IsNoCharts()
    {
        Assert.Empty(TeamReviewBuilder.Build(Page(), TeamReviewTab.Defence));
    }
}
