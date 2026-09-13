using BlackoutRugbyDashboard.Services;
using Xunit;

namespace BlackoutRugbyDashboard.Tests;

public class ParseFixturesTests
{
    [Fact]
    public void ParseFixtures_ParsesRegressionFixtures_WithAliasesAndBothDateStyles()
    {
        var fixtures = TestXml.CreateAdapter().ParseFixtures(TestXml.Load("comparison-fixtures.xml"));

        Assert.Equal(2, fixtures.Count); // the fixture with an unparseable date is skipped

        var textDated = fixtures[0];
        Assert.Equal(3812596, textDated.Id);
        Assert.Equal(80, textDated.Season);
        Assert.Equal(12, textDated.Round);
        Assert.Equal("National League", textDated.Competition);
        Assert.Equal(2, textDated.HomeTeamId);
        Assert.Equal(7, textDated.AwayTeamId);
        Assert.Equal("Marist Brothers", textDated.HomeTeamName);
        Assert.Equal("North Harbour", textDated.AwayTeamName);
        Assert.Equal(24, textDated.HomeScore);
        Assert.Equal(17, textDated.AwayScore);
        Assert.Equal(DateTime.Parse("2026-08-22 14:30:00"), textDated.Date);

        var unixDated = fixtures[1];
        Assert.Equal(3812601, unixDated.Id);
        Assert.Equal(80, unixDated.Season);
        Assert.Equal(13, unixDated.Round);
        Assert.Equal(string.Empty, unixDated.Competition);
        Assert.Equal(9, unixDated.HomeTeamId);
        Assert.Equal(2, unixDated.AwayTeamId);
        Assert.Equal("Poneke", unixDated.HomeTeamName);
        Assert.Equal("Oriental-Rongotai", unixDated.AwayTeamName);
        Assert.Equal(10, unixDated.HomeScore);
        Assert.Equal(10, unixDated.AwayScore);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1755774600).LocalDateTime, unixDated.Date);
    }

    [Fact]
    public void ParseFixtures_PreservesResponseOrder_RegardlessOfDates()
    {
        const string xml = "<fixtures>" +
                           "<fixture><id>1</id><date>2026-03-01</date></fixture>" +
                           "<fixture><id>2</id><date>2026-01-01</date></fixture>" +
                           "<fixture><id>3</id><matchstart>1755774600</matchstart></fixture>" +
                           "</fixtures>";

        var fixtures = TestXml.CreateAdapter().ParseFixtures(xml);

        Assert.Equal(new[] { 1, 2, 3 }, fixtures.Select(fixture => fixture.Id));
    }

    [Fact]
    public void ParseFixtures_UnparseableDates_SkipFixture()
    {
        const string xml = "<fixtures>" +
                           "<fixture><id>1</id><date>not-a-date</date></fixture>" +
                           "<fixture><id>2</id><date>2026-01-01</date></fixture>" +
                           "<fixture><id>3</id></fixture>" +
                           "</fixtures>";

        var fixtures = TestXml.CreateAdapter().ParseFixtures(xml);

        var fixture = Assert.Single(fixtures);
        Assert.Equal(2, fixture.Id);
    }

    [Fact]
    public void ParseFixtures_MissingTeamNameElements_FallBackToTeamIdLabels()
    {
        const string xml = "<fixtures>" +
                           "<fixture><id>5</id><date>2026-02-02</date><home_team_id>2</home_team_id><away_team_id>4</away_team_id></fixture>" +
                           "</fixtures>";

        var fixture = Assert.Single(TestXml.CreateAdapter().ParseFixtures(xml));

        Assert.Equal("Team 2", fixture.HomeTeamName);
        Assert.Equal("Team 4", fixture.AwayTeamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseFixtures_BlankInput_YieldsNoFixtures(string xml)
    {
        Assert.Empty(TestXml.CreateAdapter().ParseFixtures(xml));
    }

    [Fact]
    public void ParseFixtures_MalformedXml_YieldsNoFixtures()
    {
        Assert.Empty(TestXml.CreateAdapter().ParseFixtures("<fixtures><fixture><id>1</fixtures>"));
    }

    [Fact]
    public void ParseFixtures_ResponseWithoutFixtures_YieldsEmptyList()
    {
        Assert.Empty(TestXml.CreateAdapter().ParseFixtures("<teams><team><id>2</id></team></teams>"));
    }
}
