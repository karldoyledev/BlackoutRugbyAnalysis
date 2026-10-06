using BlackoutRugbyDashboard.Data;
using BlackoutRugbyDashboard.Services;
using Xunit;

namespace BlackoutRugbyDashboard.Tests;

/// <summary>
/// The Player History page's pure pieces (D7) at their seam: the per-fixture trends
/// table's context columns and values, the column sort, the per-field sparklines, and
/// the season block — all from cached rows, no API.
/// </summary>
public class PlayerHistoryTests
{
    private static PlayerFixtureHistoryRow Entry(
        int fixtureId, bool isHome, int startUnix, int score, int opposition,
        int tries = 0, int slot = 1, int minutes = 80, bool hasResult = true)
    {
        var fixture = new FixtureRow
        {
            FixtureId = fixtureId,
            HomeTeamId = isHome ? 45047 : 45037,
            GuestTeamId = isHome ? 45037 : 45047,
            Competition = "League",
            Round = fixtureId,
            MatchStartUnix = startUnix,
            MatchFinishUnix = startUnix + 60
        };
        var row = new PlayerFixtureRow
        {
            FixtureId = fixtureId,
            TeamId = 45047,
            PlayerId = 99,
            Slot = slot,
            MinutesPlayed = minutes,
            Tries = tries
        };
        return new PlayerFixtureHistoryRow(fixture, row, isHome, score, opposition, hasResult);
    }

    private static readonly IReadOnlyDictionary<int, string> Names = new Dictionary<int, string> { [45037] = "Foxes" };
    private static readonly IReadOnlyDictionary<int, bool> Bots = new Dictionary<int, bool> { [45037] = true };

    [Fact]
    public void BuildRows_ReadsTheContextColumns_FromTheFixtureAndSummary()
    {
        var history = new List<PlayerFixtureHistoryRow>
        {
            Entry(fixtureId: 2, isHome: false, startUnix: 300, score: 15, opposition: 5, tries: 2, slot: 1, minutes: 80)
        };

        var row = Assert.Single(PlayerHistory.BuildRows(history, 45047, Names, Bots, StatGroup.Attack));

        Assert.Equal(2, row.FixtureId);
        Assert.Equal("Foxes", row.Opponent);
        Assert.True(row.OpponentIsBot);
        Assert.Equal("W", row.Result);
        Assert.Equal("15-5", row.ResultText);
        Assert.Equal("League R2", row.CompetitionLabel);
        Assert.Equal(1, row.Slot);
        Assert.Equal(80, row.Minutes);
        Assert.Equal(2, row.Values[0]); // Attack's first field is tries
    }

    [Fact]
    public void BuildRows_WithoutASummary_ReadsNoResult()
    {
        var history = new List<PlayerFixtureHistoryRow>
        {
            Entry(fixtureId: 1, isHome: true, startUnix: 100, score: 0, opposition: 0, hasResult: false)
        };

        var row = Assert.Single(PlayerHistory.BuildRows(history, 45047, Names, Bots, StatGroup.Attack));

        Assert.False(row.HasResult);
        Assert.Equal("—", row.ResultText);
        Assert.Equal(string.Empty, row.Result);
    }

    [Fact]
    public void Sort_DefaultsToNewestFirst_AndSortsByAField()
    {
        var rows = PlayerHistory.BuildRows(
            new List<PlayerFixtureHistoryRow>
            {
                Entry(fixtureId: 2, isHome: false, startUnix: 300, score: 15, opposition: 5, tries: 1),
                Entry(fixtureId: 1, isHome: true, startUnix: 100, score: 20, opposition: 10, tries: 3)
            },
            45047, Names, Bots, StatGroup.Attack);

        var newest = PlayerHistory.Sort(rows, StatGroup.Attack, PlayerHistory.DateKey, descending: true);
        Assert.Equal(2, newest[0].FixtureId);

        var oldest = PlayerHistory.Sort(rows, StatGroup.Attack, PlayerHistory.DateKey, descending: false);
        Assert.Equal(1, oldest[0].FixtureId);

        var byTries = PlayerHistory.Sort(rows, StatGroup.Attack, "tries", descending: true);
        Assert.Equal(1, byTries[0].FixtureId); // fixture 1 scored 3 tries
    }

    [Fact]
    public void Sparklines_ProduceOnePerField_AndStayShortWithoutTwoReadings()
    {
        var twoRows = PlayerHistory.BuildRows(
            new List<PlayerFixtureHistoryRow>
            {
                Entry(fixtureId: 2, isHome: false, startUnix: 300, score: 15, opposition: 5, tries: 2),
                Entry(fixtureId: 1, isHome: true, startUnix: 100, score: 20, opposition: 10, tries: 1)
            },
            45047, Names, Bots, StatGroup.Attack);

        var sparks = PlayerHistory.Sparklines(twoRows, StatGroup.Attack);
        Assert.Equal(GameReviewMatrix.FieldsFor(StatGroup.Attack).Count, sparks.Count);
        Assert.Contains("<svg", sparks[0]);

        var oneRow = PlayerHistory.BuildRows(
            new List<PlayerFixtureHistoryRow>
            {
                Entry(fixtureId: 1, isHome: true, startUnix: 100, score: 20, opposition: 10, tries: 1)
            },
            45047, Names, Bots, StatGroup.Attack);
        Assert.All(PlayerHistory.Sparklines(oneRow, StatGroup.Attack), spark => Assert.Equal(string.Empty, spark));
    }

    [Fact]
    public void BuildSeasonBlock_GroupsByStatGroup_AndSumsCaps()
    {
        var row = new PlayerSeasonRow
        {
            LeagueCaps = 5,
            CupCaps = 2,
            Tries = 7,
            Tackles = 40,
            AvKickingMetres = 12
        };

        var block = PlayerHistory.BuildSeasonBlock(62, row);

        Assert.NotNull(block);
        Assert.Equal(7, block!.TotalCaps);
        Assert.Equal(12, block.AverageKickingMetres);
        Assert.Equal(GameReviewMatrix.Groups.Count, block.Groups.Count);
        var attack = block.Groups.Single(group => group.Group == StatGroup.Attack);
        Assert.Equal(7, attack.Fields.Single(field => field.Label == "Tries").Value);
        var defence = block.Groups.Single(group => group.Group == StatGroup.Defence);
        Assert.Equal(40, defence.Fields.Single(field => field.Label == "Tackles").Value);
    }

    [Fact]
    public void BuildSeasonBlock_NoRowOrNoAverage_ReadsNull()
    {
        Assert.Null(PlayerHistory.BuildSeasonBlock(62, null));

        var block = PlayerHistory.BuildSeasonBlock(62, new PlayerSeasonRow());
        Assert.Null(block!.AverageKickingMetres);
    }
}
