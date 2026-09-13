using BlackoutRugbyDashboard.Services;
using Xunit;

namespace BlackoutRugbyDashboard.Tests;

public class ParseFixturePlayerStatisticsTests
{
    [Fact]
    public void ParseFixturePlayerStatistics_ParsesRegressionFixture_WithAliasesAndRosterCorrelation()
    {
        var roster = new Dictionary<int, string>
        {
            [101] = "Sean O'Brien",
            [102] = "Tane Williams"
        };

        var players = TestXml.CreateAdapter().ParseFixturePlayerStatistics(
            TestXml.Load("comparison-fixture-player-stats.xml"),
            teamId: 2,
            rosterNames: roster);

        var expected = new[]
        {
            new FixturePlayerStatistics(
                101,
                "Sean O'Brien",
                Tackles: 14,
                MetresGained: 185,
                Tries: 2,
                Conversions: 0,
                DropGoals: 1,
                Penalties: 0,
                TotalPoints: 12,
                YellowCards: 1,
                RedCards: 0,
                Linebreaks: 3,
                Intercepts: 1,
                Kicks: 2,
                KnockOns: 1,
                ForwardPasses: 0,
                TryAssists: 1,
                BeatenDefenders: 6,
                Injuries: 0,
                HandlingErrors: 1,
                MissedTackles: 2,
                Fights: 0,
                KickingMetres: 240,
                PenaltiesConceded: 2,
                KicksOutOnTheFull: 1,
                LineoutsWon: 4,
                LineoutsLost: 1,
                ScrumWins: 5,
                ScrumLosses: 2),
            new FixturePlayerStatistics(
                102,
                "Tane Williams",
                Tackles: 9,
                MetresGained: 88,
                Tries: 1,
                Conversions: 0,
                DropGoals: 0,
                Penalties: 0,
                TotalPoints: 5,
                YellowCards: 0,
                RedCards: 0,
                Linebreaks: 0,
                Intercepts: 0,
                Kicks: 0,
                KnockOns: 0,
                ForwardPasses: 0,
                TryAssists: 0,
                BeatenDefenders: 0,
                Injuries: 0,
                HandlingErrors: 0,
                MissedTackles: 0,
                Fights: 0,
                KickingMetres: 0,
                PenaltiesConceded: 0,
                KicksOutOnTheFull: 0,
                LineoutsWon: 0,
                LineoutsLost: 0,
                ScrumWins: 0,
                ScrumLosses: 0)
        };

        Assert.Equal(expected, players);
    }

    [Fact]
    public void ParseFixturePlayerStatistics_RanksByTotalPointsThenTacklesThenName()
    {
        const string xml = "<fixture_statistics>" +
                           "<player_stats teamid=\"2\"><id>1</id><totalpoints>10</totalpoints><tackles>5</tackles></player_stats>" +
                           "<player_stats teamid=\"2\"><id>2</id><totalpoints>10</totalpoints><tackles>9</tackles></player_stats>" +
                           "<player_stats teamid=\"2\"><id>3</id><totalpoints>12</totalpoints></player_stats>" +
                           "<player_stats teamid=\"2\"><id>4</id><totalpoints>8</totalpoints></player_stats>" +
                           "<player_stats teamid=\"2\"><id>5</id><totalpoints>8</totalpoints></player_stats>" +
                           "<player_stats teamid=\"2\"><id>6</id><totalpoints>8</totalpoints></player_stats>" +
                           "</fixture_statistics>";
        var roster = new Dictionary<int, string>
        {
            [4] = "Zane",
            [5] = "Adam",
            [6] = "Mia"
        };

        var players = TestXml.CreateAdapter().ParseFixturePlayerStatistics(xml, 2, roster);

        // 3 leads on points; 2 beats 1 on tackles; 5 ("Adam") beats 6 ("Mia") beats 4 ("Zane") by name.
        Assert.Equal(new[] { 3, 2, 1, 5, 6, 4 }, players.Select(player => player.PlayerId));
    }

    [Fact]
    public void ParseFixturePlayerStatistics_WithoutRoster_FallsBackToPlayerIdLabel()
    {
        const string xml = "<fixture_statistics>" +
                           "<player_stats teamid=\"9\"><id>201</id><tackles>11</tackles></player_stats>" +
                           "</fixture_statistics>";

        var players = TestXml.CreateAdapter().ParseFixturePlayerStatistics(xml, 9);

        var player = Assert.Single(players);
        Assert.Equal(201, player.PlayerId);
        Assert.Equal("Player 201", player.Name);
        Assert.Equal(11, player.Tackles);
    }

    [Fact]
    public void ParseFixturePlayerStatistics_OtherTeamsPlayers_AreExcluded()
    {
        var players = TestXml.CreateAdapter().ParseFixturePlayerStatistics(
            TestXml.Load("comparison-fixture-player-stats.xml"),
            teamId: 9,
            rosterNames: new Dictionary<int, string>());

        var player = Assert.Single(players);
        Assert.Equal(201, player.PlayerId);
        Assert.Equal(11, player.Tackles);
    }

    [Fact]
    public void ParseFixturePlayerStatistics_PlayersWithoutValidId_AreSkipped()
    {
        const string xml = "<fixture_statistics>" +
                           "<player_stats teamid=\"2\"><tackles>5</tackles></player_stats>" +
                           "<player_stats teamid=\"2\"><id>0</id><tackles>6</tackles></player_stats>" +
                           "<player_stats teamid=\"2\"><id>8</id><tackles>7</tackles></player_stats>" +
                           "</fixture_statistics>";

        var players = TestXml.CreateAdapter().ParseFixturePlayerStatistics(xml, 2);

        var player = Assert.Single(players);
        Assert.Equal(8, player.PlayerId);
        Assert.Equal(7, player.Tackles);
    }

    [Fact]
    public void ParseFixturePlayerStatistics_NoMatchingTeamRecords_YieldsEmptyList()
    {
        var players = TestXml.CreateAdapter().ParseFixturePlayerStatistics(
            TestXml.Load("comparison-fixture-player-stats.xml"),
            teamId: 99,
            rosterNames: new Dictionary<int, string>());

        Assert.Empty(players);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseFixturePlayerStatistics_BlankInput_YieldsNoPlayerStatistics(string xml)
    {
        Assert.Empty(TestXml.CreateAdapter().ParseFixturePlayerStatistics(xml, 2));
    }

    [Fact]
    public void ParseFixturePlayerStatistics_MalformedXml_YieldsNoPlayerStatistics()
    {
        Assert.Empty(TestXml.CreateAdapter().ParseFixturePlayerStatistics("<fixture_statistics><player_stats>", 2));
    }
}

