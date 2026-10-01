using System.Globalization;
using System.Net;
using System.Text;

namespace BlackoutRugbyDashboard.Services;

/// <summary>
/// Which side of a chart a series reads as (S6 / #38). It selects the theme token
/// the line and its points are drawn with — <see cref="Us"/> the accent,
/// <see cref="Opponent"/> the adverse/loss colour — so a chart inherits the
/// command-centre palette instead of hard-coding hex values.
/// </summary>
public enum ChartTone
{
    Us,
    Opponent
}

/// <summary>
/// One line on a chart: its legend label, the side it reads as, and one value per
/// x position. A null is a Fixture with no reading for this metric — the point is
/// skipped rather than drawn as a zero, which would read as a real collapse.
/// </summary>
public sealed record SvgSeries(string Label, ChartTone Tone, IReadOnlyList<double?> Values);

/// <summary>
/// One chart: a title, its x-axis labels (oldest to newest) and its series —
/// everything a small multiple needs to render itself.
/// </summary>
public sealed record SvgChart(string Title, IReadOnlyList<string> Labels, IReadOnlyList<SvgSeries> Series);

/// <summary>
/// The Squad page's charting, inline and library-free (S6 / #38): single-series
/// and two-series lines, sparklines and small-multiple grids, all hand-rolled SVG.
/// Colours ride the site's theme tokens (the <c>.svg-us</c> / <c>.svg-adverse</c>
/// classes in site.css), so nothing here hard-codes the palette; hover values are
/// native SVG <c>&lt;title&gt;</c> children, so no tooltip script is needed. No
/// canvas, no CDN reference, no chart library.
/// </summary>
public static class SvgCharts
{
    private const double Width = 1040;
    private const double Height = 230;
    private const double Left = 52;
    private const double Right = 14;
    private const double Top = 14;
    private const double Bottom = 26;

    private const double SparkWidth = 76;
    private const double SparkHeight = 20;

    /// <summary>
    /// One line chart: a single series (a trend) or two (a comparison), each with
    /// its own scale, time running left to right and every point's value readable
    /// on hover.
    /// </summary>
    public static string Line(SvgChart chart)
    {
        var labels = chart.Labels;
        var innerWidth = Width - Left - Right;
        var innerHeight = Height - Top - Bottom;

        var readings = chart.Series
            .SelectMany(series => series.Values)
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToList();
        var dataMax = readings.Count == 0 ? 0 : readings.Max();
        var scaleMax = dataMax > 0 ? dataMax * 1.1 : 1;

        var sb = new StringBuilder();
        sb.Append("<svg class=\"svg-line\" viewBox=\"0 0 ")
          .Append(Format(Width)).Append(' ').Append(Format(Height))
          .Append("\" role=\"img\">");

        double X(int index) => Left + innerWidth * (labels.Count < 2 ? 0.5 : (double)index / (labels.Count - 1));
        double Y(double value) => Top + innerHeight * (1 - value / scaleMax);

        // The y grid: five rules with their value at the left.
        for (var step = 0; step <= 4; step++)
        {
            var gy = Top + innerHeight * step / 4;
            var value = (int)Math.Round(scaleMax * (1 - step / 4.0), MidpointRounding.AwayFromZero);
            sb.Append("<line class=\"svg-grid\" x1=\"").Append(Format(Left))
              .Append("\" x2=\"").Append(Format(Width - Right))
              .Append("\" y1=\"").Append(Format(gy))
              .Append("\" y2=\"").Append(Format(gy)).Append("\" />");
            sb.Append("<text class=\"svg-tick\" x=\"").Append(Format(Left - 6))
              .Append("\" y=\"").Append(Format(gy + 3))
              .Append("\" text-anchor=\"end\">").Append(value).Append("</text>");
        }

        foreach (var series in chart.Series)
        {
            var tone = ToneClass(series.Tone);
            var points = new List<string>();
            for (var index = 0; index < series.Values.Count && index < labels.Count; index++)
            {
                if (series.Values[index] is { } value)
                {
                    points.Add($"{Format(X(index))},{Format(Y(value))}");
                }
            }

            sb.Append("<polyline class=\"svg-series ").Append(tone)
              .Append("\" points=\"").Append(string.Join(' ', points)).Append("\" />");

            for (var index = 0; index < series.Values.Count && index < labels.Count; index++)
            {
                if (series.Values[index] is not { } value)
                {
                    continue;
                }

                sb.Append("<circle class=\"svg-point ").Append(tone)
                  .Append("\" cx=\"").Append(Format(X(index)))
                  .Append("\" cy=\"").Append(Format(Y(value)))
                  .Append("\" r=\"3\"><title>")
                  .Append(WebUtility.HtmlEncode(labels[index])).Append(": ").Append(Format(value))
                  .Append("</title></circle>");
            }
        }

        // The x axis: every third label, plus the newest, so it never crowds.
        for (var index = 0; index < labels.Count; index++)
        {
            if (index % 3 != 0 && index != labels.Count - 1)
            {
                continue;
            }

            sb.Append("<text class=\"svg-tick\" x=\"").Append(Format(X(index)))
              .Append("\" y=\"").Append(Format(Height - 8))
              .Append("\" text-anchor=\"middle\">")
              .Append(WebUtility.HtmlEncode(labels[index])).Append("</text>");
        }

        return sb.Append("</svg>").ToString();
    }

    /// <summary>
    /// A single-series mini line, for a table's column or a card's header. Hover
    /// still reads each point's value through its <c>&lt;title&gt;</c>.
    /// </summary>
    public static string Sparkline(IReadOnlyList<double?> values)
    {
        var readings = values.Where(value => value.HasValue).Select(value => value!.Value).ToList();
        if (readings.Count < 2)
        {
            return string.Empty;
        }

        var min = readings.Min();
        var max = readings.Max();
        var span = max - min > 0 ? max - min : 1;

        var sb = new StringBuilder();
        sb.Append("<svg class=\"svg-spark\" viewBox=\"0 0 ")
          .Append(Format(SparkWidth)).Append(' ').Append(Format(SparkHeight))
          .Append("\" role=\"img\">");

        double X(int index) => values.Count < 2 ? 0 : index / (double)(values.Count - 1) * SparkWidth;
        double Y(double value) => SparkHeight - 2 - (value - min) / span * (SparkHeight - 4);

        var points = new List<string>();
        for (var index = 0; index < values.Count; index++)
        {
            if (values[index] is { } value)
            {
                points.Add($"{Format(X(index))},{Format(Y(value))}");
            }
        }

        sb.Append("<polyline class=\"svg-spark-line\" points=\"").Append(string.Join(' ', points)).Append("\" />");

        for (var index = 0; index < values.Count; index++)
        {
            if (values[index] is not { } value)
            {
                continue;
            }

            sb.Append("<circle class=\"svg-spark-point\" cx=\"").Append(Format(X(index)))
              .Append("\" cy=\"").Append(Format(Y(value)))
              .Append("\" r=\"1.5\"><title>").Append(Format(value)).Append("</title></circle>");
        }

        return sb.Append("</svg>").ToString();
    }

    /// <summary>
    /// A grid of line charts, each on its own scale — the Team review form (S6):
    /// eight small multiples sharing one x-axis, so the shape of each trend reads
    /// at a glance.
    /// </summary>
    public static string SmallMultipleGrid(IReadOnlyList<SvgChart> charts)
    {
        if (charts.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder("<div class=\"chart-mini-grid\">");
        foreach (var chart in charts)
        {
            sb.Append("<figure class=\"chart-mini\"><figcaption>")
              .Append(WebUtility.HtmlEncode(chart.Title))
              .Append("</figcaption>")
              .Append(Line(chart))
              .Append("</figure>");
        }

        return sb.Append("</div>").ToString();
    }

    private static string ToneClass(ChartTone tone) => tone == ChartTone.Us ? "svg-us" : "svg-adverse";

    private static string Format(double value) =>
        Math.Abs(value - Math.Round(value)) < 0.0001
            ? ((long)Math.Round(value)).ToString(CultureInfo.InvariantCulture)
            : value.ToString("0.#", CultureInfo.InvariantCulture);
}
