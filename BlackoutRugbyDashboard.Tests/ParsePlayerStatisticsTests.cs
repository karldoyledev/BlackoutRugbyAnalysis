using BlackoutRugbyDashboard.Services;
using Xunit;

namespace BlackoutRugbyDashboard.Tests;

public class ParsePlayerStatisticsTests
{
    [Fact]
    public void ParsePlayerStatistics_ParsesFullDisplayedStatisticSet_FromRegressionFixture()
    {
        var expected = new PlayerStatistics(
            101,
            Tackles: 1234,
            MetresGained: 12345,
            Tries: 14,
            Conversions: 6,
            DropGoals: 2,
            Penalties: 9,
            TotalPoints: 112,
            YellowCards: 3,
            RedCards: 1,
            Linebreaks: 21,
            Intercepts: 5,
            Kicks: 40,
            KnockOns: 7,
            ForwardPasses: 4,
            TryAssists: 8,
            BeatenDefenders: 33,
            Injuries: 2,
            HandlingErrors: 6,
            MissedTackles: 18,
            Fights: 1,
            KickingMetres: 1950,
            MissedConversions: 3,
            MissedDropGoals: 1,
            MissedPenalties: 2,
            GoodUpAndUnders: 11,
            BadUpAndUnders: 4,
            UpAndUnders: 15,
            GoodKicks: 25,
            BadKicks: 9,
            TurnoversWon: 12,
            LineoutsSecured: 17,
            LineoutsConceded: 6,
            LineoutsStolen: 3,
            SuccessfulLineoutThrows: 14,
            UnsuccessfulLineoutThrows: 5,
            PenaltiesConceded: 10,
            KicksOutOnTheFull: 2,
            BallTime: 45,
            PenaltyTime: 8,
            TotalCaps: 101,
            LeagueCaps: 50,
            FriendlyCaps: 12,
            CupCaps: 20,
            UnderTwentyCaps: 8,
            NationalCaps: 5,
            WorldCupCaps: 2,
            UnderTwentyWorldCupCaps: 1,
            OtherCaps: 3);

        var stats = TestXml.CreateAdapter().ParsePlayerStatistics(101, TestXml.Load("dashboard-player-statistics.xml"));

        Assert.Equal(expected, stats);
    }

    [Fact]
    public void ParsePlayerStatistics_GroupedValues_AreNormalized()
    {
        const string xml = "<player_statistics>" +
                           "<metresgained>12,345</metresgained>" +
                           "<kickingmetres>1,950</kickingmetres>" +
                           "<leaguecaps>50</leaguecaps>" +
                           "</player_statistics>";

        var stats = TestXml.CreateAdapter().ParsePlayerStatistics(3, xml);

        Assert.NotNull(stats);
        Assert.Equal(12345, stats!.MetresGained);
        Assert.Equal(1950, stats.KickingMetres);
        Assert.Equal(50, stats.LeagueCaps);
        Assert.Equal(50, stats.TotalCaps);
    }

    [Fact]
    public void ParsePlayerStatistics_MissingValues_DefaultToZero()
    {
        const string xml = "<player_statistics><tackles>3</tackles></player_statistics>";

        var stats = TestXml.CreateAdapter().ParsePlayerStatistics(4, xml);

        Assert.NotNull(stats);
        Assert.Equal(4, stats!.PlayerId);
        Assert.Equal(3, stats.Tackles);
        Assert.Equal(0, stats.MetresGained);
        Assert.Equal(0, stats.TotalCaps);
    }

    [Fact]
    public void ParsePlayerStatistics_AbsentStatisticsElement_YieldsNoStatistics()
    {
        Assert.Null(TestXml.CreateAdapter().ParsePlayerStatistics(5, "<players><player><id>5</id></player></players>"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ParsePlayerStatistics_BlankInput_YieldsNoStatistics(string xml)
    {
        Assert.Null(TestXml.CreateAdapter().ParsePlayerStatistics(5, xml));
    }

    [Fact]
    public void ParsePlayerStatistics_MalformedXml_YieldsNoStatistics()
    {
        Assert.Null(TestXml.CreateAdapter().ParsePlayerStatistics(5, "<player_statistics><tackles>1</player_statisitcs>"));
    }
}
