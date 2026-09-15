using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using System.Xml.Linq;
using BlackoutRugbyDashboard.Data;
using BlackoutRugbyDashboard.Services;
using Xunit;

namespace BlackoutRugbyDashboard.Tests;

/// <summary>
/// Parser tests for the Match Cache entry-scope reads (D1), with a
/// reflection-based completeness check: every [Column]-named stat field on a
/// row must exist in the live artifact XML, so the "all fs fields verbatim"
/// rule (D1 §6) cannot silently rot when the API grows a field.
/// </summary>
public class ParseMatchCacheTests
{
    [Fact]
    public void ParseFixtureRecords_ReadsFullFMetadata()
    {
        var rows = TestXml.CreateAdapter().ParseFixtureRecords(TestXml.Load("r4-f-last8-refresh.xml"));

        Assert.Equal(8, rows.Count);
        var leagueR2 = rows.Single(row => row.FixtureId == 21416928);
        Assert.Equal(62, leagueR2.Season);
        Assert.Equal(2, leagueR2.Round);
        Assert.Equal("League", leagueR2.Competition);
        Assert.Equal(45047, leagueR2.HomeTeamId);
        Assert.Equal(45037, leagueR2.GuestTeamId);
        Assert.Equal(0, leagueR2.DataRemoved);
        Assert.True(leagueR2.MatchFinishUnix > 0);
        Assert.Equal("IE", leagueR2.CountryIso);

        var leagueR1 = rows.Single(row => row.FixtureId == 21416925);
        Assert.Equal(1, leagueR1.DataRemoved);
    }

    [Fact]
    public void ParsePlayerFixtureStats_SquadRead_Yields23SlotsWithJerseyAndSide()
    {
        var rows = TestXml.CreateAdapter().ParsePlayerFixtureStats(TestXml.Load("r3-fs-teamplayers-21416928.xml"));

        Assert.Equal(23, rows.Count);
        Assert.All(rows, row => Assert.Equal(45047, row.TeamId));
        Assert.All(rows, row => Assert.Equal("home", row.Side));
        Assert.All(rows, row => Assert.Equal(21416928, row.FixtureId));
        Assert.Equal(1, rows[0].Jersey);
        Assert.Equal(23, rows[22].Slot);
        Assert.Equal(Enumerable.Range(1, 23), rows.Select(row => row.Slot));
    }

    [Fact]
    public void ParsePlayerFixtureStats_ValuesArePerFixtureAndVerbatim()
    {
        var rows = TestXml.CreateAdapter().ParsePlayerFixtureStats(TestXml.Load("r3-fs-teamplayers-21416928.xml"));

        var slot1 = rows.Single(row => row.Slot == 1);
        Assert.Equal(16728687, slot1.PlayerId);
        Assert.Equal(11, slot1.EnergyBefore);
        Assert.Equal(1, slot1.Tries);
        Assert.Equal(5, slot1.TotalPoints);
        Assert.Equal(81, slot1.MinutesPlayed);
        Assert.Equal(17, slot1.BallTime);
        Assert.Equal(1, slot1.LeagueCaps);

        var kicker = rows.Single(row => row.PlayerId == 16396178);
        Assert.Equal(13, kicker.Conversions);
        Assert.Equal(10, kicker.Jersey);
    }

    [Fact]
    public void ParsePlayerFixtureStats_EveryColumnFieldExistsInLiveXml()
    {
        var xml = TestXml.Load("r3-fs-teamplayers-21416928.xml");
        var element = XElement.Parse(xml).Descendants().First(node => node.Name.LocalName == "home_player_1");

        AssertColumnFieldsPresent<PlayerFixtureRow>(element, expectedCount: 50);
    }

    [Fact]
    public void ParseTeamFixtureStats_BareRead_YieldsFourBlocksBothSidesBothHalves()
    {
        var rows = TestXml.CreateAdapter().ParseTeamFixtureStats(TestXml.Load("r3-fs-bare-21416928.xml"));

        Assert.Equal(4, rows.Count);
        Assert.All(rows, row => Assert.Equal(21416928, row.FixtureId));
        var full = rows.Single(row => row.Side == "home" && row.Half == "full");
        Assert.Equal(45047, full.TeamId);
        Assert.Equal(13, full.Tries);
        Assert.Equal(97, full.TotalPoints);
        Assert.Equal(13, full.Conversions);
        Assert.True(full.Possession > 0);
        Assert.True(full.Phases > 0);
        Assert.Equal(2, rows.Count(row => row.Half == "half1"));
    }

    [Fact]
    public void ParseTeamFixtureStats_EveryColumnFieldExistsInLiveXml()
    {
        var xml = TestXml.Load("r3-fs-bare-21416928.xml");
        var element = XElement.Parse(xml).Descendants().First(node => node.Name.LocalName == "home_team_stats");

        AssertColumnFieldsPresent<TeamFixtureStatRow>(element, expectedCount: 58);
    }

    [Fact]
    public void ParsePlayerFixtureStats_BlankOrMalformed_YieldsNoRows()
    {
        Assert.Empty(TestXml.CreateAdapter().ParsePlayerFixtureStats("   "));
        Assert.Empty(TestXml.CreateAdapter().ParsePlayerFixtureStats("<fixture_statistics id=\"1\"><home_player_1>"));
    }

    private static void AssertColumnFieldsPresent<TRow>(XElement element, int expectedCount)
    {
        var columnNames = typeof(TRow)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.GetCustomAttribute<ColumnAttribute>()?.Name)
            .Where(name => name is not null)
            .Select(name => name!)
            .ToList();

        Assert.Equal(expectedCount, columnNames.Count);

        var present = element
            .Elements()
            .Select(node => node.Name.LocalName)
            .ToHashSet(StringComparer.Ordinal);

        var missing = columnNames.Where(name => !present.Contains(name)).ToList();
        Assert.True(missing.Count == 0, $"Missing verbatim fs fields in live XML: {string.Join(", ", missing)}");
    }
}
