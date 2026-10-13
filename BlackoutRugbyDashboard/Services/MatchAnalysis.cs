using System.Globalization;
using System.Text.Json;
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

    /// <summary>
    /// One slot of the tactics panel's team sheet (D6 §2): the slot number (1–15 the
    /// XV, 16–23 the Bench), the Player id, the name resolved from the latest Squad
    /// Snapshot, and the captain / kicker badges.
    /// </summary>
    public sealed record TeamSheetSlot(
        int Slot, int PlayerId, string Name, bool IsBench, bool IsCaptain, bool IsKicker);

    /// <summary>
    /// The tactics panel's team sheet (D6 §2) from the cached `lu` lineup: the XV
    /// (slots 1–15) then the Bench (slots 16–23), each with its Player name resolved
    /// from the latest Squad Snapshot and the captain / kicker badges. A null lineup
    /// (nothing archived) yields no rows; a slot with no Player id is skipped; a Player
    /// absent from the capture reads as "Player {id}" rather than a made-up name.
    /// Strategy sliders are deliberately absent — the API cannot verify them (R1).
    /// </summary>
    public static IReadOnlyList<TeamSheetSlot> BuildTeamSheet(
        Lineup? lineup, IReadOnlyDictionary<int, string> names)
    {
        if (lineup is null)
        {
            return [];
        }

        var slots = new List<TeamSheetSlot>();
        for (var index = 0; index < lineup.Xv.Count; index++)
        {
            AddSlot(slots, index + 1, lineup.Xv[index], isBench: false, lineup, names);
        }

        for (var index = 0; index < lineup.Bench.Count; index++)
        {
            AddSlot(slots, index + 16, lineup.Bench[index], isBench: true, lineup, names);
        }

        return slots;
    }

    /// <summary>The intensity chip's text from the cached Match Summary ("Intensity: 2"),
    /// or null when our side's intensity is not cached (0 / missing) — never a made-up value.</summary>
    public static string? IntensityText(int? intensity) =>
        intensity is int value && value > 0 ? $"Intensity: {value}" : null;

    /// <summary>
    /// The weather pill's text from the cached Match Summary ("Weather 2 · Night"), or
    /// null when no weather id is cached. The API documents only the night flag
    /// (`night=1`) and carries no id-to-name map, so the id reads as itself.
    /// </summary>
    public static string? WeatherText(int? weatherId, bool? night)
    {
        if (weatherId is not int id || id <= 0)
        {
            return null;
        }

        return night == true ? $"Weather {id} · Night" : $"Weather {id}";
    }

    /// <summary>
    /// The half-time score ("HT 12-7") from the cached half-1 team rows, or null when
    /// either side's half-1 row is not cached (D6 §1: never a hard dependency — omit
    /// cleanly). The half-1 team row carries the points at the break in `total_points`.
    /// </summary>
    public static string? HalfTimeScore(TeamFixtureStatRow? homeFirstHalf, TeamFixtureStatRow? guestFirstHalf) =>
        homeFirstHalf is null || guestFirstHalf is null
            ? null
            : $"HT {homeFirstHalf.TotalPoints}-{guestFirstHalf.TotalPoints}";

    /// <summary>
    /// Our side's injuries as the notes card reads them ("M. Ratu 45' — 7 days, replaced
    /// by J. Doe"), one line per event; empty when the summary is absent or holds none.
    /// </summary>
    public static IReadOnlyList<string> InjuryLines(
        MatchSummaryRow? summary, bool oursIsHome, IReadOnlyDictionary<int, string> names) =>
        EventLines(summary?.InjuriesJson, oursIsHome, names, injury: true);

    /// <summary>Our side's substitutions, as the notes card reads them ("J. Doe 60' — replaced by K. Ali").</summary>
    public static IReadOnlyList<string> SubstitutionLines(
        MatchSummaryRow? summary, bool oursIsHome, IReadOnlyDictionary<int, string> names) =>
        EventLines(summary?.SubstitutionsJson, oursIsHome, names, injury: false);

    /// <summary>
    /// Formats our side's events from one Match Summary JSON blob (`{ home: [...],
    /// guest: [...] }` of `MatchSummaryEvent`s, as the cache writes it). A malformed or
    /// absent blob, or the entity's default `"[]"`, reads as no events rather than
    /// throwing; a Player unknown to the capture reads as "Player {id}".
    /// </summary>
    private static IReadOnlyList<string> EventLines(
        string? json, bool oursIsHome, IReadOnlyDictionary<int, string> names, bool injury)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return [];
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty(oursIsHome ? "home" : "guest", out var events)
                || events.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var lines = new List<string>();
            foreach (var item in events.EnumerateArray())
            {
                var playerId = ReadJsonInt(item, "PlayerId");
                if (playerId <= 0)
                {
                    continue;
                }

                var minute = ReadJsonInt(item, "Minute");
                var replacedBy = ReadJsonInt(item, "ReplacedById");
                var name = PlayerNameFor(playerId, names);
                if (injury)
                {
                    var days = ReadJsonInt(item, "DaysInjured");
                    var tail = replacedBy > 0 ? $", replaced by {PlayerNameFor(replacedBy, names)}" : string.Empty;
                    lines.Add($"{name} {minute}' — {days} {(days == 1 ? "day" : "days")}{tail}");
                }
                else
                {
                    var tail = replacedBy > 0 ? $" — replaced by {PlayerNameFor(replacedBy, names)}" : string.Empty;
                    lines.Add($"{name} {minute}'{tail}");
                }
            }

            return lines;
        }
    }

    private static int ReadJsonInt(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : 0;

    private static string PlayerNameFor(int playerId, IReadOnlyDictionary<int, string> names) =>
        names.TryGetValue(playerId, out var name) && !string.IsNullOrWhiteSpace(name)
            ? name
            : $"Player {playerId}";

    private static void AddSlot(
        List<TeamSheetSlot> slots, int slot, int playerId, bool isBench, Lineup lineup,
        IReadOnlyDictionary<int, string> names)
    {
        if (playerId <= 0)
        {
            return;
        }

        var name = names.TryGetValue(playerId, out var resolved) && !string.IsNullOrWhiteSpace(resolved)
            ? resolved
            : $"Player {playerId}";
        slots.Add(new TeamSheetSlot(slot, playerId, name, isBench, lineup.CaptainId == playerId, lineup.KickerId == playerId));
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