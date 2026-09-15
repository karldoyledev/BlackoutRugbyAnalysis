using BlackoutRugbyDashboard.Services;
using Xunit;

namespace BlackoutRugbyDashboard.Tests;

public class ParseMatchSummariesTests
{
    [Fact]
    public void ParseMatchSummaries_BatchResponse_ReadsOneSummaryPerFixture()
    {
        var summaries = TestXml.CreateAdapter().ParseMatchSummaries(TestXml.Load("r2-ms-batch3.xml"));

        Assert.Equal(3, summaries.Count);
        Assert.Equal(new[] { 21416925, 21416928, 21460743 }, summaries.Select(summary => summary.FixtureId).ToArray());
    }

    [Fact]
    public void ParseMatchSummaries_Points_ReadForBothSides()
    {
        var summary = TestXml.CreateAdapter().ParseMatchSummaries(TestXml.Load("r2-ms-batch3.xml"))
            .Single(item => item.FixtureId == 21416925);

        Assert.Equal(52, summary.Home.Points);
        Assert.Equal(23, summary.Guest.Points);
    }

    [Fact]
    public void ParseMatchSummaries_Scorers_ReadWithTypeAndCount()
    {
        var summary = TestXml.CreateAdapter().ParseMatchSummaries(TestXml.Load("r2-ms-batch3.xml"))
            .Single(item => item.FixtureId == 21416925);

        var tries = summary.Home.Scorers.Where(scorer => scorer.ScorerType == "tries").ToList();
        Assert.Equal(7, tries.Count);
        Assert.All(tries, scorer => Assert.Equal(1, scorer.Count));
        Assert.Contains(summary.Home.Scorers, scorer => scorer.ScorerType == "conversions" && scorer.Count == 7);
    }

    [Fact]
    public void ParseMatchSummaries_AttendanceTiers_ReadAndSumToTheTotalCrowd()
    {
        var summary = TestXml.CreateAdapter().ParseMatchSummaries(TestXml.Load("r2-ms-batch3.xml"))
            .Single(item => item.FixtureId == 21416925);

        var attendance = summary.Attendance;
        Assert.NotNull(attendance);
        Assert.Equal(11168, attendance!.Standing);
        Assert.Equal(17250, attendance.Uncovered);
        Assert.Equal(14831, attendance.Covered);
        Assert.Equal(4025, attendance.Members);
        Assert.Equal(504, attendance.Corporate);
        Assert.Equal(47778,
            attendance.Standing + attendance.Uncovered + attendance.Covered + attendance.Members + attendance.Corporate);
    }

    [Fact]
    public void ParseMatchSummaries_WeatherAndIntensity_AreRead()
    {
        var summary = TestXml.CreateAdapter().ParseMatchSummaries(TestXml.Load("r2-ms-batch3.xml"))
            .Single(item => item.FixtureId == 21416925);

        Assert.Equal(1, summary.WeatherId);
        Assert.True(summary.WeatherNight);
        Assert.Equal(2, summary.Home.Intensity);
    }

    [Fact]
    public void ParseMatchSummaries_InjuriesAndSubs_AreReadWithMinutes()
    {
        var summary = TestXml.CreateAdapter().ParseMatchSummaries(TestXml.Load("r2-ms-batch3.xml"))
            .Single(item => item.FixtureId == 21416925);

        var injury = Assert.Single(summary.Home.Injuries.Where(item => item.PlayerId == 16439553));
        Assert.Equal(72, injury.Minute);
        Assert.Equal(16497736, injury.ReplacedById);

        Assert.Contains(summary.Home.Substitutions, sub => sub.PlayerId == 16366761 && sub.Minute == 51);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseMatchSummaries_BlankInput_YieldsNoSummaries(string xml)
    {
        Assert.Empty(TestXml.CreateAdapter().ParseMatchSummaries(xml));
    }

    [Fact]
    public void ParseMatchSummaries_MalformedXml_YieldsNoSummaries()
    {
        Assert.Empty(TestXml.CreateAdapter().ParseMatchSummaries("<match_summary fixtureid=\"1\"><home>"));
    }
}
