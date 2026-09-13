using BlackoutRugbyDashboard.Services;
using Xunit;

namespace BlackoutRugbyDashboard.Tests;

public class ParseTeamTests
{
    [Fact]
    public void ParseTeam_DecodesEncodedName_AndReadsCountryIso()
    {
        var team = TestXml.CreateAdapter().ParseTeam(TestXml.Load("dashboard-team.xml"));

        Assert.NotNull(team);
        Assert.Equal(2, team!.Id);
        Assert.Equal("Wellington Rugby", team.Name);
        Assert.Equal("NZ", team.CountryIso);
    }

    [Fact]
    public void ParseTeam_PlainName_IsReturnedUnchanged()
    {
        const string xml = "<teams><team><id>31</id><name>Plain Town RFC</name><country_iso>IE</country_iso></team></teams>";

        var team = TestXml.CreateAdapter().ParseTeam(xml);

        Assert.NotNull(team);
        Assert.Equal("Plain Town RFC", team!.Name);
        Assert.Equal("IE", team.CountryIso);
    }

    [Fact]
    public void ParseTeam_MissingOptionalFields_YieldDefaults()
    {
        const string xml = "<teams><team><id>5</id></team></teams>";

        var team = TestXml.CreateAdapter().ParseTeam(xml);

        Assert.NotNull(team);
        Assert.Equal(5, team!.Id);
        Assert.Equal(string.Empty, team.Name);
        Assert.Equal(string.Empty, team.CountryIso);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseTeam_BlankInput_YieldsNoTeam(string xml)
    {
        Assert.Null(TestXml.CreateAdapter().ParseTeam(xml));
    }

    [Fact]
    public void ParseTeam_ResponseWithoutTeamElement_YieldsNoTeam()
    {
        Assert.Null(TestXml.CreateAdapter().ParseTeam("<players><player><id>1</id></player></players>"));
    }

    [Fact]
    public void ParseTeam_MalformedXml_YieldsNoTeam()
    {
        Assert.Null(TestXml.CreateAdapter().ParseTeam("<teams><team><id>2</team></players>"));
    }
}
