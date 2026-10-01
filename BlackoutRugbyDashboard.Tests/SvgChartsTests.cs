using BlackoutRugbyDashboard.Services;
using Xunit;

namespace BlackoutRugbyDashboard.Tests;

/// <summary>
/// The inline-SVG chart helpers (S6, build slice #38): the shapes the page needs —
/// a single series, a two-series comparison, a sparkline and a small-multiple grid
/// — emitted as SVG with no chart library, no canvas and no CDN reference, coloured
/// from the theme tokens and readable on hover through native SVG titles.
/// </summary>
public class SvgChartsTests
{
    private static SvgChart Chart(params SvgSeries[] series) =>
        new("Possession %", new[] { "Sep 1", "Sep 8" }, series);

    [Fact]
    public void Line_EmitsSvgWithHoverValuesAndNoLibrary()
    {
        var svg = SvgCharts.Line(Chart(new SvgSeries("Possession", ChartTone.Us, new double?[] { 55.5, 61 })));

        Assert.Contains("<svg", svg);
        Assert.Contains("<polyline", svg);
        Assert.Contains("<title>Sep 1: 55.5</title>", svg);
        Assert.Contains("<title>Sep 8: 61</title>", svg);
        // No chart library, no canvas, no script, no inline hex colours: the theme
        // tokens are CSS classes, so the palette stays in site.css.
        Assert.DoesNotContain("canvas", svg);
        Assert.DoesNotContain("<script", svg);
        Assert.DoesNotContain("#", svg);
    }

    [Fact]
    public void Line_DrawsOneSeriesOrTwo()
    {
        var single = SvgCharts.Line(Chart(new SvgSeries("A", ChartTone.Us, new double?[] { 1, 2 })));
        Assert.Equal(1, Count(single, "<polyline"));

        var pair = SvgCharts.Line(Chart(
            new SvgSeries("Us", ChartTone.Us, new double?[] { 1, 2 }),
            new SvgSeries("Them", ChartTone.Opponent, new double?[] { 3, 1 })));
        Assert.Equal(2, Count(pair, "<polyline"));
        Assert.Contains("svg-us", pair);
        Assert.Contains("svg-adverse", pair);
    }

    /// <summary>A null reading is a gap: no point, no title, nothing invented.</summary>
    [Fact]
    public void Line_SkipsNullReadings()
    {
        var svg = SvgCharts.Line(Chart(new SvgSeries("Possession", ChartTone.Us, new double?[] { 55.5, null })));

        Assert.Contains("<title>Sep 1: 55.5</title>", svg);
        Assert.DoesNotContain("Sep 8:", svg);
    }

    [Fact]
    public void Sparkline_NeedsTwoPointsAndOtherwiseDrawsAPolyline()
    {
        Assert.Equal(string.Empty, SvgCharts.Sparkline(new double?[] { 5 }));
        Assert.Equal(string.Empty, SvgCharts.Sparkline(Array.Empty<double?>()));

        var spark = SvgCharts.Sparkline(new double?[] { 5, 9, 7 });
        Assert.Contains("<svg", spark);
        Assert.Contains("<polyline", spark);
        Assert.Contains("<title>9</title>", spark);
        Assert.DoesNotContain("#", spark);
    }

    [Fact]
    public void SmallMultipleGrid_OneFigurePerChartWithItsTitle()
    {
        var charts = new[]
        {
            Chart(new SvgSeries("A", ChartTone.Us, new double?[] { 1, 2 })),
            new SvgChart(
                "Territory %",
                new[] { "Sep 1", "Sep 8" },
                new[] { new SvgSeries("B", ChartTone.Us, new double?[] { 2, 3 }) })
        };

        var grid = SvgCharts.SmallMultipleGrid(charts);

        Assert.Equal(2, Count(grid, "<figure"));
        Assert.Contains("Possession %", grid);
        Assert.Contains("Territory %", grid);
        Assert.Equal(2, Count(grid, "<svg"));
    }

    [Fact]
    public void SmallMultipleGrid_OfNothingIsNothing()
    {
        Assert.Equal(string.Empty, SvgCharts.SmallMultipleGrid(Array.Empty<SvgChart>()));
    }

    private static int Count(string haystack, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }
}
