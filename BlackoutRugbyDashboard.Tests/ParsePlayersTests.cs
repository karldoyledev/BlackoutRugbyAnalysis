using BlackoutRugbyDashboard.Services;
using Xunit;

namespace BlackoutRugbyDashboard.Tests;

public class ParsePlayersTests
{
    [Fact]
    public void ParsePlayers_ParsesRegressionPlayers_WithDecodedNamesAndGroupedSalary()
    {
        var players = TestXml.CreateAdapter().ParsePlayers(TestXml.Load("dashboard-players.xml"));

        Assert.Equal(2, players.Count); // the player with an invalid id is skipped

        var first = players[0];
        Assert.Equal(101, first.Id);
        Assert.Equal("Seán O'Brien", first.Name);
        Assert.Equal(24, first.Age);
        Assert.Equal(145, first.Csr);
        Assert.Equal(1250000, first.Salary);
        Assert.Equal(7, first.Form);
        Assert.Equal(92, first.Energy);
        Assert.Equal(new[] { "kicking 4", "pace 2" }, first.RecentPops);

        var second = players[1];
        Assert.Equal(102, second.Id);
        Assert.Equal("Tane Williams", second.Name);
        Assert.Equal(29, second.Age);
        Assert.Equal(980500, second.Salary);
        Assert.Empty(second.RecentPops);
    }

    [Fact]
    public void ParsePlayers_MissingOptionalFields_YieldDefaults()
    {
        const string xml = "<players><player><id>7</id></player></players>";

        var players = TestXml.CreateAdapter().ParsePlayers(xml);

        var player = Assert.Single(players);
        Assert.Equal(7, player.Id);
        Assert.Equal(string.Empty, player.Name);
        Assert.Equal(0, player.Age);
        Assert.Equal(0, player.Csr);
        Assert.Equal(0, player.Salary);
        Assert.Equal(0, player.Form);
        Assert.Equal(0, player.Energy);
        Assert.Empty(player.RecentPops);
    }

    [Fact]
    public void ParsePlayers_InvalidPlayerIds_AreSkipped()
    {
        const string xml = "<players>" +
                           "<player><id>0</id><fname>No</fname><lname>Id</lname></player>" +
                           "<player><id>oops</id><fname>Bad</fname><lname>Id</lname></player>" +
                           "<player><id>9</id><fname>Valid</fname><lname>Player</lname></player>" +
                           "</players>";

        var players = TestXml.CreateAdapter().ParsePlayers(xml);

        var player = Assert.Single(players);
        Assert.Equal(9, player.Id);
        Assert.Equal("Valid Player", player.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ParsePlayers_BlankInput_YieldsNoPlayers(string xml)
    {
        Assert.Empty(TestXml.CreateAdapter().ParsePlayers(xml));
    }

    [Fact]
    public void ParsePlayers_MalformedXml_YieldsNoPlayers()
    {
        Assert.Empty(TestXml.CreateAdapter().ParsePlayers("<players><player><id>1</players>"));
    }

    [Fact]
    public void ParsePlayers_ResponseWithoutPlayerElements_YieldsEmptyList()
    {
        Assert.Empty(TestXml.CreateAdapter().ParsePlayers("<teams><team><id>2</id></team></teams>"));
    }
}
