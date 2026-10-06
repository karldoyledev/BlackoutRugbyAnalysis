using BlackoutRugbyDashboard.Data;
using BlackoutRugbyDashboard.Services;
using Xunit;

namespace BlackoutRugbyDashboard.Tests;

/// <summary>
/// The Match Analysis page's pure pieces (D6): the twelve fixed team-compare pairs
/// (D4) from the cached full-time team stats, the attendance breakdown and the
/// Match Summary's scoring lines.
/// </summary>
public class MatchAnalysisTests
{
    [Fact]
    public void BuildCompare_YieldsTheTwelveFixedPairsInOrder()
    {
        var rows = MatchAnalysis.BuildCompare(new TeamFixtureStatRow(), new TeamFixtureStatRow());

        Assert.Equal(12, rows.Count);
        Assert.Equal(
            new[]
            {
                "Possession", "Territory", "Tries", "Tackles", "Rucks won", "Mauls won",
                "Scrums won", "Lineouts won", "Phases", "Minutes in 22", "Penalties conceded", "Turnovers won"
            },
            rows.Select(row => row.Label).ToArray());
    }

    [Fact]
    public void BuildCompare_PossessionAndTerritory_ReadAsComputedShares()
    {
        var us = new TeamFixtureStatRow { Possession = 3000, Territory = 42 };
        var them = new TeamFixtureStatRow { Possession = 2000, Territory = 58 };

        var rows = MatchAnalysis.BuildCompare(us, them);

        var possession = rows.Single(row => row.Label == "Possession");
        Assert.Equal("60%", possession.Us);
        Assert.Equal("40%", possession.Them);
        Assert.Equal(60, possession.UsShare);

        var territory = rows.Single(row => row.Label == "Territory");
        Assert.Equal("42%", territory.Us);
        Assert.Equal("58%", territory.Them);
        Assert.Equal(42, territory.UsShare);
    }

    [Fact]
    public void BuildCompare_CountsReadVerbatim_AndAMissingSideReadsAsADash()
    {
        var rows = MatchAnalysis.BuildCompare(new TeamFixtureStatRow { Tries = 3, Tackles = 88 }, null);

        Assert.Equal("3", rows.Single(row => row.Label == "Tries").Us);
        Assert.Equal("—", rows.Single(row => row.Label == "Tries").Them);
        Assert.Equal("88", rows.Single(row => row.Label == "Tackles").Us);
    }

    [Fact]
    public void Attendance_SumTheFiveTiers_AndReadAsADashWithoutASummary()
    {
        var summary = new MatchSummaryRow
        {
            Standing = 100,
            Uncovered = 200,
            Covered = 50,
            Members = 400,
            Corporate = 10
        };

        Assert.Equal(760, MatchAnalysis.AttendanceTotal(summary));
        Assert.Contains("Members 400", MatchAnalysis.AttendanceBreakdown(summary));
        Assert.Equal("—", MatchAnalysis.AttendanceBreakdown(null));
        Assert.Equal(0, MatchAnalysis.AttendanceTotal(null));
    }

    [Fact]
    public void ScoringLines_GroupByTypeInOrder_AndFallBackToThePlayerId()
    {
        var scorers = new List<MatchSummaryScorerRow>
        {
            new() { ScorerType = "tries", PlayerId = 1, Count = 2 },
            new() { ScorerType = "tries", PlayerId = 2, Count = 1 },
            new() { ScorerType = "penalties", PlayerId = 1, Count = 3 }
        };
        var names = new Dictionary<int, string> { [1] = "M. Ratu" };

        var lines = MatchAnalysis.ScoringLines(scorers, names);

        Assert.Equal(2, lines.Count);
        Assert.Equal("Tries: M. Ratu ×2, Player 2", lines[0]);
        Assert.Equal("Penalties: M. Ratu ×3", lines[1]);
    }
}