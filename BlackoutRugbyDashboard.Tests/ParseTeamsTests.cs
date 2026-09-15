using BlackoutRugbyDashboard.Services;
using Xunit;

namespace BlackoutRugbyDashboard.Tests;

public class ParseTeamsTests
{
    [Fact]
    public void ParseTeams_BatchResponse_ReadsEveryTeamWithStrengthFacts()
    {
        var teams = TestXml.CreateAdapter().ParseTeams(TestXml.Load("r5-t-batch-window-fixed.xml"));

        Assert.Equal(6, teams.Count);
        Assert.Contains(teams, team => team.Id == 45037 && team.Name == "Pok78" && !team.Bot);
        Assert.Contains(teams, team => team.Id == 44520 && team.Bot);
        Assert.Contains(teams, team => team.Id == 46229 && team.Bot);
        Assert.Contains(teams, team => team.Id == 45817 && team.Name == "Galway R.F.C" && !team.Bot);
    }

    [Fact]
    public void ParseTeams_SingleResponse_DeduplicatesAttributeAndElementId_AndReadsStrengthFacts()
    {
        var teams = TestXml.CreateAdapter().ParseTeams(TestXml.Load("r5-t-single-45037-fixed.xml"));

        var team = Assert.Single(teams);
        Assert.Equal(45037, team.Id);
        Assert.Equal("Pok78", team.Name);
        Assert.Equal("IE", team.CountryIso);
        Assert.False(team.Bot);
        Assert.True(team.AverageTop15Csr > 0);
        Assert.NotNull(team.RankingPoints);
        Assert.True(team.RankingPoints > 0);
    }

    [Fact]
    public void ParseTeams_RankingPoints_IsReadAsDecimalNotTruncated()
    {
        const string xml = "<blackoutrugby_api_response><team id=\"9\"><id>9</id><name>Decimal Town</name>"
            + "<ranking_points>37.211</ranking_points><average_top15_csr>220739</average_top15_csr></team></blackoutrugby_api_response>";

        var team = Assert.Single(TestXml.CreateAdapter().ParseTeams(xml));

        Assert.Equal(37.211, team.RankingPoints);
        Assert.Equal(220739, team.AverageTop15Csr);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseTeams_BlankInput_YieldsNoTeams(string xml)
    {
        Assert.Empty(TestXml.CreateAdapter().ParseTeams(xml));
    }

    [Fact]
    public void ParseTeams_MalformedXml_YieldsNoTeams()
    {
        Assert.Empty(TestXml.CreateAdapter().ParseTeams("<teams><team><id>2</team>"));
    }
}
