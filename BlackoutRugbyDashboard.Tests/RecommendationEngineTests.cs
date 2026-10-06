using BlackoutRugbyDashboard.Data;
using BlackoutRugbyDashboard.Services;
using Xunit;

namespace BlackoutRugbyDashboard.Tests;

/// <summary>
/// The Recommendation engine (D8) at its pure seam (spec Testing Decisions §2): the
/// eight v1 rules' Watch/Act boundaries, severity ordering, the empty state and the
/// evidence/advice rendering — all from two bare-`fs` TeamFixtureStat rows, no API.
/// </summary>
public class RecommendationEngineTests
{
    private static IReadOnlyList<RecommendationItem> Build(
        Action<TeamFixtureStatRow>? configureUs = null, Action<TeamFixtureStatRow>? configureThem = null)
    {
        var us = new TeamFixtureStatRow();
        var them = new TeamFixtureStatRow();
        configureUs?.Invoke(us);
        configureThem?.Invoke(them);
        return RecommendationEngine.Build(us, them);
    }

    [Fact]
    public void BalancedRow_ProducesNoFlags()
    {
        // Both sides identical: no share is under 45%, no differential clears its Watch bound.
        Assert.Empty(Build(us =>
        {
            us.Possession = 50;
            us.Territory = 50;
            us.LineoutsWon = 10;
            us.LineoutsLost = 10;
            us.RucksWon = 20;
            us.TurnoversConceded = 2;
            us.Turnovers = 2;
            us.PenaltiesConceded = 3;
            us.PenaltiesWon = 3;
        }, them =>
        {
            them.Possession = 50;
            them.Territory = 50;
            them.RucksWon = 20;
            them.Tries = 2;
        }));
    }

    [Theory]
    [InlineData(40, RecommendationSeverity.Watch)] // 40% — under Watch(45), over Act(35)
    [InlineData(35, RecommendationSeverity.Watch)] // exactly Act bound: not Act, so Watch
    [InlineData(34, RecommendationSeverity.Act)]   // under Act bound
    public void PossessionShare_ReadsWatchOrActAtTheBoundaries(int oursShare, RecommendationSeverity expected)
    {
        var item = Assert.Single(Build(
            us => us.Possession = oursShare,
            them => them.Possession = 100 - oursShare));

        Assert.Equal("Lost the possession battle", item.Diagnosis);
        Assert.Equal(expected, item.Severity);
        Assert.Contains($"{oursShare}%", item.Evidence);
    }

    [Fact]
    public void PossessionShare_WithNoReadings_IsSilent()
    {
        Assert.Empty(Build());
    }

    [Theory]
    [InlineData(44, RecommendationSeverity.Watch)]
    [InlineData(34, RecommendationSeverity.Act)]
    public void TerritoryShare_ReadsWatchOrAct(int oursShare, RecommendationSeverity expected)
    {
        var item = Assert.Single(Build(us => us.Territory = oursShare, them => them.Territory = 100 - oursShare));

        Assert.Equal("Starved of territory", item.Diagnosis);
        Assert.Equal(expected, item.Severity);
    }

    [Theory]
    [InlineData(11, 8, RecommendationSeverity.Watch)] // lost − won = 3
    [InlineData(14, 8, RecommendationSeverity.Act)]   // lost − won = 6
    public void Lineouts_LostMinusWon(int lost, int won, RecommendationSeverity expected)
    {
        var item = Assert.Single(Build(us =>
        {
            us.LineoutsWon = won;
            us.LineoutsLost = lost;
        }));

        Assert.Equal("Lineout battle lost", item.Diagnosis);
        Assert.Equal(expected, item.Severity);
        Assert.Equal($"Lineouts: won {won}, lost {lost}", item.Evidence);
        Assert.Equal("Raise Driving · Cut Expansive", item.AdviceText);
    }

    [Theory]
    [InlineData(2, RecommendationSeverity.Watch)]
    [InlineData(4, RecommendationSeverity.Act)]
    public void Scrums_LostOnOwnBall(int lost, RecommendationSeverity expected)
    {
        var item = Assert.Single(Build(us => us.ScrumsLost = lost));

        Assert.Equal("Scrum under pressure", item.Diagnosis);
        Assert.Equal(expected, item.Severity);
        Assert.Equal("Cut Expansive", item.AdviceText);
    }

    [Theory]
    [InlineData(23, RecommendationSeverity.Watch)] // theirs − yours = 5
    [InlineData(28, RecommendationSeverity.Act)]   // theirs − yours = 10
    public void Rucks_TheirsMinusYours(int theirRucks, RecommendationSeverity expected)
    {
        var item = Assert.Single(Build(
            us => us.RucksWon = 18,
            them => them.RucksWon = theirRucks));

        Assert.Equal("Ruck battle lost", item.Diagnosis);
        Assert.Equal(expected, item.Severity);
        Assert.Equal("Raise Pick and Go · Raise Driving · Cut Expansive", item.AdviceText);
    }

    [Theory]
    [InlineData(5, RecommendationSeverity.Watch)] // conceded − won = 3
    [InlineData(8, RecommendationSeverity.Act)]   // conceded − won = 6
    public void Turnovers_ConcededMinusWon(int conceded, RecommendationSeverity expected)
    {
        var item = Assert.Single(Build(us =>
        {
            us.TurnoversConceded = conceded;
            us.Turnovers = 2;
        }));

        Assert.Equal("Throwing it away", item.Diagnosis);
        Assert.Equal(expected, item.Severity);
        Assert.Equal("Cut Creative · Cut Expansive · Raise Driving", item.AdviceText);
    }

    [Theory]
    [InlineData(4, RecommendationSeverity.Watch)]
    [InlineData(8, RecommendationSeverity.Act)]
    public void Penalties_ConcededMinusWon(int conceded, RecommendationSeverity expected)
    {
        var item = Assert.Single(Build(us =>
        {
            us.PenaltiesConceded = conceded;
            us.PenaltiesWon = 0;
        }));

        Assert.Equal("Discipline is costing points", item.Diagnosis);
        Assert.Equal(expected, item.Severity);
        Assert.Equal("Ease off Discipline", item.AdviceText);
    }

    [Theory]
    [InlineData(3, RecommendationSeverity.Watch)]
    [InlineData(5, RecommendationSeverity.Act)]
    public void Tries_Conceded(int tries, RecommendationSeverity expected)
    {
        var item = Assert.Single(Build(configureThem: them => them.Tries = tries));

        Assert.Equal("Defence broken repeatedly", item.Diagnosis);
        Assert.Equal(expected, item.Severity);
        Assert.Equal($"Tries conceded: {tries}", item.Evidence);
        Assert.Equal("Raise Defence", item.AdviceText);
    }

    [Fact]
    public void Items_OrderActBeforeWatch_ThenRuleOrderWithinALevel()
    {
        var items = Build(us =>
        {
            us.Possession = 40;          // Watch (rule 1)
            us.LineoutsWon = 8;
            us.LineoutsLost = 14;        // Act (rule 3)
            us.PenaltiesConceded = 4;    // Watch (rule 7)
        }, them => them.Possession = 60);

        Assert.Collection(
            items,
            item => Assert.Equal(RecommendationSeverity.Act, item.Severity),
            item => Assert.Equal("Lost the possession battle", item.Diagnosis),
            item => Assert.Equal("Discipline is costing points", item.Diagnosis));
    }
}
