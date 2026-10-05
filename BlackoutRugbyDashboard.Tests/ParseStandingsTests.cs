using BlackoutRugbyDashboard.Services;
using Xunit;

namespace BlackoutRugbyDashboard.Tests;

/// <summary>
/// The standings parser (Home's league table, r=s): position, team id, and the
/// played / won / drawn / lost / for / against / bonus / points record. The
/// fixture is the live shape probed 2026-10-06 (league 349097, season 62).
/// </summary>
public class ParseStandingsTests
{
    [Fact]
    public void ParseStandings_LeagueResponse_ReadsEveryRowAndNotThePositionChild()
    {
        var rows = TestXml.CreateAdapter().ParseStandings(TestXml.Load("r6-standings-349097.xml"));

        // The <standing> container nests a <standing> position child; that child
        // has no teamid, so only the eight container rows survive.
        Assert.Equal(8, rows.Count);
        Assert.DoesNotContain(rows, row => row.TeamId == 0);
    }

    [Fact]
    public void ParseStandings_ReadsTheRecordAndBonusSumForOneTeam()
    {
        var rows = TestXml.CreateAdapter().ParseStandings(TestXml.Load("r6-standings-349097.xml"));

        var own = Assert.Single(rows, row => row.TeamId == 45047);
        Assert.Equal(4, own.Position);
        Assert.Equal(62, own.Season);
        Assert.Equal(5, own.Played);
        Assert.Equal(3, own.Won);
        Assert.Equal(0, own.Drawn);
        Assert.Equal(2, own.Lost);
        Assert.Equal(248, own.PointsFor);
        Assert.Equal(90, own.PointsAgainst);
        Assert.Equal(4, own.BonusPoints); // b1 3 + b2 1
        Assert.Equal(16, own.Points);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseStandings_BlankInput_YieldsNoRows(string xml)
    {
        Assert.Empty(TestXml.CreateAdapter().ParseStandings(xml));
    }

    [Fact]
    public void ParseStandings_MalformedXml_YieldsNoRows()
    {
        Assert.Empty(TestXml.CreateAdapter().ParseStandings("<standings><standing><teamid>2</standing>"));
    }
}