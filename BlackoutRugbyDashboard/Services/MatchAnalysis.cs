using System.Globalization;
using BlackoutRugbyDashboard.Data;

namespace BlackoutRugbyDashboard.Services;

/// <summary>One row of the team compare (D6/D4): the pair's label, both sides'
/// readings, and — for the percentage pairs — our share of the split bar.</summary>
public sealed record CompareRow(string Label, string Us, string Them, int? UsShare);

/// <summary>
/// The Match Analysis page's pure pieces (D6): the twelve fixed team-compare pairs
/// (D4) from the cached full-time TeamFixtureStat rows of both sides, plus the
/// Match Summary's attendance and scorer lines the masthead and notes read. No API
/// client and no store, so the page renders from the Match Cache by construction.
/// </summary>
public static class MatchAnalysis
{
    /// <summary>The twelve fixed pairs (D4): possession and territory as computed
    /// shares, the rest verbatim. A missing side (a foreign Fixture, `data_removed`)
    /// reads as a dash rather than a made-up zero.</summary>
    public static IReadOnlyList<CompareRow> BuildCompare(TeamFixtureStatRow? us, TeamFixtureStatRow? them) =>
    [
        Share("Possession", us?.Possession, them?.Possession),
        Share("Territory", us?.Territory, them?.Territory),
        Count("Tries", us?.Tries, them?.Tries),
        Count("Tackles", us?.Tackles, them?.Tackles),
        Count("Rucks won", us?.RucksWon, them?.RucksWon),
        Count("Mauls won", us?.MaulsWon, them?.MaulsWon),
        Count("Scrums won", us?.ScrumsWon, them?.ScrumsWon),
        Count("Lineouts won", us?.LineoutsWon, them?.LineoutsWon),
        Count("Phases", us?.Phases, them?.Phases),
        Count("Minutes in 22", us?.MinutesIn22, them?.MinutesIn22),
        Count("Penalties conceded", us?.PenaltiesConceded, them?.PenaltiesConceded),
        Count("Turnovers won", us?.Turnovers, them?.Turnovers)
    ];

    /// <summary>The five attendance tiers as hover text ("Standing 4,200 · …"),
    /// largest first, or "—" when the Match Summary is not cached.</summary>
    public static string AttendanceBreakdown(MatchSummaryRow? summary) =>
        summary is null
            ? "—"
            : string.Join(" · ", new[]
                {
                    ("Standing", summary.Standing),
                    ("Uncovered", summary.Uncovered),
                    ("Covered", summary.Covered),
                    ("Members", summary.Members),
                    ("Corporate", summary.Corporate)
                }
                .Where(tier => tier.Item2 > 0)
                .OrderByDescending(tier => tier.Item2)
                .Select(tier => $"{tier.Item1} {tier.Item2:N0}"));

    /// <summary>The total crowd, or 0 when the Match Summary is not cached.</summary>
    public static int AttendanceTotal(MatchSummaryRow? summary) =>
        summary is null
            ? 0
            : summary.Standing + summary.Uncovered + summary.Covered + summary.Members + summary.Corporate;

    /// <summary>The Match Summary's scorers as readable lines ("Tries: M. Ratu ×2"),
    /// grouped tries → conversions → penalties → drop goals. An unknown Player id
    /// reads as its id rather than an invented name.</summary>
    public static IReadOnlyList<string> ScoringLines(
        IReadOnlyList<MatchSummaryScorerRow> scorers, IReadOnlyDictionary<int, string> names)
    {
        var order = new[]
        {
            ("tries", "Tries"),
            ("conversions", "Conversions"),
            ("penalties", "Penalties"),
            ("dropgoals", "Drop goals")
        };

        return order
            .Select(entry => (Label: entry.Item2, Rows: scorers.Where(row => row.ScorerType == entry.Item1).ToList()))
            .Where(entry => entry.Rows.Count > 0)
            .Select(entry => $"{entry.Label}: " + string.Join(", ", entry.Rows.Select(row =>
                (names.TryGetValue(row.PlayerId, out var name) && !string.IsNullOrWhiteSpace(name)
                    ? name
                    : $"Player {row.PlayerId}")
                + (row.Count > 1 ? $" ×{row.Count}" : string.Empty))))
            .ToList();
    }

    private static CompareRow Share(string label, int? us, int? them)
    {
        if (us is not int ours || them is not int theirs || ours + theirs <= 0)
        {
            return new CompareRow(label, "—", "—", null);
        }

        var share = (int)Math.Round(ours * 100.0 / (ours + theirs));
        return new CompareRow(label, $"{share}%", $"{100 - share}%", share);
    }

    private static CompareRow Count(string label, int? us, int? them) =>
        new(label, Text(us), Text(them), null);

    private static string Text(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "—";
}