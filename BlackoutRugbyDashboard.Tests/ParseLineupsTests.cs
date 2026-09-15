using BlackoutRugbyDashboard.Services;
using Xunit;

namespace BlackoutRugbyDashboard.Tests;

public class ParseLineupsTests
{
    [Fact]
    public void ParseLineups_DuplicatedLineupElements_AreCollapsedToOne()
    {
        var lineups = TestXml.CreateAdapter().ParseLineups(TestXml.Load("r1-lineups-lu-fixture-21416928.xml"));

        var lineup = Assert.Single(lineups);
        Assert.Equal(45047, lineup.TeamId);
        Assert.Equal(21416928, lineup.FixtureId);
    }

    [Fact]
    public void ParseLineups_SlotsReadInOrder_XvFifteenAndBenchEight()
    {
        var lineup = Assert.Single(TestXml.CreateAdapter().ParseLineups(TestXml.Load("r1-lineups-lu-fixture-21416928.xml")));

        Assert.Equal(15, lineup.Xv.Count);
        Assert.Equal(8, lineup.Bench.Count);
        Assert.Equal(16728687, lineup.Xv[0]);
        Assert.Equal(16413443, lineup.Xv[14]);
        Assert.Equal(16319936, lineup.Bench[0]);
        Assert.Equal(16377958, lineup.Bench[7]);
    }

    [Fact]
    public void ParseLineups_CaptainAndKicker_AreRead()
    {
        var lineup = Assert.Single(TestXml.CreateAdapter().ParseLineups(TestXml.Load("r1-lineups-lu-fixture-21416928.xml")));

        Assert.Equal(16398979, lineup.CaptainId);
        Assert.Equal(16396178, lineup.KickerId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseLineups_BlankInput_YieldsNoLineups(string xml)
    {
        Assert.Empty(TestXml.CreateAdapter().ParseLineups(xml));
    }

    [Fact]
    public void ParseLineups_MalformedXml_YieldsNoLineups()
    {
        Assert.Empty(TestXml.CreateAdapter().ParseLineups("<lineup><p1>1</lineup>"));
    }
}
