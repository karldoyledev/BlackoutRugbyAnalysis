using BlackoutRugbyDashboard.Data;
using BlackoutRugbyDashboard.Models;
using BlackoutRugbyDashboard.Services;
using Xunit;

namespace BlackoutRugbyDashboard.Tests;

/// <summary>
/// The C7 results strip (S7, build slice #41): the cached window as a slim,
/// newest-first navigation strip — date, competition + round, opponent (BOT
/// marked), result and margin. The builder is pure — it takes the page's cached
/// window, cached names and captured bot flags and returns rows — so "cache-only"
/// is structural, not asserted.
/// </summary>
public class SquadResultsTests
{
    private const int TeamId = 45047;
    private const int OpponentId = 45037;

    private static readonly DateTime Day0 = new(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc);

    private static long Unix(int dayOffset) => new DateTimeOffset(Day0.AddDays(dayOffset)).ToUnixTimeSeconds();

    private static SquadFixtureRows Fixture(
        int fixtureId, int dayOffset, int homeTeam, int guestTeam,
        int round = 1, string competition = "League",
        int? homePoints = null, int? guestPoints = null) =>
        new(
            new FixtureRow
            {
                FixtureId = fixtureId,
                Season = 62,
                LeagueId = 349097,
                Round = round,
                Competition = competition,
                HomeTeamId = homeTeam,
                GuestTeamId = guestTeam,
                MatchStartUnix = Unix(dayOffset),
                MatchFinishUnix = Unix(dayOffset) + 5400
            },
            homePoints is null
                ? null
                : new MatchSummaryRow { FixtureId = fixtureId, HomePoints = homePoints.Value, GuestPoints = guestPoints ?? 0 },
            Array.Empty<PlayerFixtureRow>(),
            null);

    private static SquadPageData Page(
        IReadOnlyList<SquadFixtureRows> window,
        IReadOnlyDictionary<int, string>? names = null,
        IReadOnlyDictionary<int, bool>? bots = null) =>
        new(
            TeamId,
            "Ashgrove RFC",
            "IE",
            false,
            71,
            62,
            window,
            names ?? new Dictionary<int, string>(),
            null,
            new Dictionary<int, PlayerStatistics>())
        {
            TeamBots = bots ?? new Dictionary<int, bool>()
        };

    [Fact]
    public void Build_ListsTheNewestFixtureFirst()
    {
        var page = Page(new[]
        {
            Fixture(1001, dayOffset: 0, TeamId, OpponentId),
            Fixture(1003, dayOffset: 14, TeamId, OpponentId),
            Fixture(1002, dayOffset: 7, TeamId, OpponentId)
        });

        var rows = SquadResults.Build(page);

        Assert.Equal(new[] { 1003, 1002, 1001 }, rows.Select(row => row.FixtureId));
    }

    [Fact]
    public void Build_NamesTheOpponentFromCachedFacts_ElseTheTeamId()
    {
        var page = Page(
            new[]
            {
                Fixture(1001, dayOffset: 0, TeamId, OpponentId),
                Fixture(1002, dayOffset: 7, OpponentId, TeamId)
            },
            names: new Dictionary<int, string> { [OpponentId] = "Riverton Kestrels" });

        var rows = SquadResults.Build(page);

        // Home and away both resolve to the same opponent — the cached name when we hold one.
        Assert.Equal("Riverton Kestrels", rows[0].Opponent);
        Assert.Equal("Riverton Kestrels", rows[1].Opponent);

        // A team we have never read falls back to its linked id.
        Assert.Equal($"Team {OpponentId}", SquadResults.Build(Page(new[] { Fixture(1001, 0, TeamId, OpponentId) }))[0].Opponent);
    }

    [Fact]
    public void Build_MarksBotOpponentsFromTheirCapturedFact()
    {
        var window = new[]
        {
            Fixture(1001, dayOffset: 0, TeamId, OpponentId),
            Fixture(1002, dayOffset: 7, TeamId, OpponentId)
        };

        var bots = SquadResults.Build(Page(window, bots: new Dictionary<int, bool> { [OpponentId] = true }));
        Assert.True(bots[0].OpponentIsBot);

        var humans = SquadResults.Build(Page(window, bots: new Dictionary<int, bool> { [OpponentId] = false }));
        Assert.False(humans[0].OpponentIsBot);

        // No captured fact at all: no flag, so the tag is absent rather than guessed.
        Assert.False(SquadResults.Build(Page(window))[0].OpponentIsBot);
    }

    [Fact]
    public void Build_ReadsTheResultAndMarginFromOurSide()
    {
        // Home: our score is the home score.
        var home = SquadResults.Build(Page(new[] { Fixture(1001, 0, TeamId, OpponentId, homePoints: 31, guestPoints: 17) }))[0];
        Assert.Equal("W", home.Result);
        Assert.Equal(31, home.Score);
        Assert.Equal(17, home.OppositionScore);
        Assert.Equal(14, home.Margin);
        Assert.Equal("+14", home.MarginText);
        Assert.Equal("margin-win", home.MarginClass);

        // Away: our score is the guest score, so the margin flips.
        var away = SquadResults.Build(Page(new[] { Fixture(1002, 0, OpponentId, TeamId, homePoints: 31, guestPoints: 17) }))[0];
        Assert.Equal("L", away.Result);
        Assert.Equal(17, away.Score);
        Assert.Equal(31, away.OppositionScore);
        Assert.Equal(-14, away.Margin);
        Assert.Equal("-14", away.MarginText);
        Assert.Equal("margin-loss", away.MarginClass);

        // A draw is neither a win nor a loss.
        var draw = SquadResults.Build(Page(new[] { Fixture(1003, 0, TeamId, OpponentId, homePoints: 20, guestPoints: 20) }))[0];
        Assert.Equal("D", draw.Result);
        Assert.Equal(0, draw.Margin);
        Assert.Equal("0", draw.MarginText);
        Assert.Equal("margin-flat", draw.MarginClass);
    }

    [Fact]
    public void Build_WithoutACachedSummary_HasNoResultRatherThanA0to0Draw()
    {
        var row = SquadResults.Build(Page(new[] { Fixture(1001, 0, TeamId, OpponentId) }))[0];

        Assert.False(row.HasResult);
        Assert.Equal(string.Empty, row.Result);
    }

    [Fact]
    public void Build_ReadsTheCompetitionAndRoundTogether()
    {
        var league = SquadResults.Build(Page(new[] { Fixture(1001, 0, TeamId, OpponentId, round: 3, competition: "League") }))[0];
        Assert.Equal("League R3", league.CompetitionLabel);

        // A fixture with no round reads as the competition alone.
        var cup = SquadResults.Build(Page(new[] { Fixture(1002, 0, TeamId, OpponentId, round: 0, competition: "Cup") }))[0];
        Assert.Equal("Cup", cup.CompetitionLabel);
    }
}