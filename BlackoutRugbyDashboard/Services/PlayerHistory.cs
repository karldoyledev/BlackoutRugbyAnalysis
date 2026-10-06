using System.Globalization;
using BlackoutRugbyDashboard.Data;

namespace BlackoutRugbyDashboard.Services;

/// <summary>
/// One row of the Player History trends table (D7): the context columns
/// (date · competition+round · opponent · result · slot / minutes) plus the selected
/// Stat Group's field values for that Fixture. The context is read from the cached
/// Fixture and Match Summary; the values from the cached PlayerFixture row.
/// </summary>
public sealed record TrendRow(
    int FixtureId,
    DateTime Date,
    string CompetitionLabel,
    string Opponent,
    bool OpponentIsBot,
    string Result,
    int Score,
    int OppositionScore,
    int Slot,
    int Minutes,
    IReadOnlyList<int> Values)
{
    /// <summary>True when the Fixture's Match Summary is cached; otherwise the result
    /// column shows a dash rather than a made-up 0–0.</summary>
    public bool HasResult => Result.Length > 0;

    /// <summary>The result as it reads ("28-17", or "—" with no summary).</summary>
    public string ResultText => HasResult ? $"{Score}-{OppositionScore}" : "—";

    /// <summary>The result's tone class — the same win / loss / flat palette the rest
    /// of the theme uses.</summary>
    public string ResultClass => Result switch
    {
        "W" => "margin-win",
        "L" => "margin-loss",
        _ => "margin-flat"
    };
}

/// <summary>One field of a Stat Group's season block: its label and its season total.</summary>
public sealed record SeasonField(string Label, int Value);

/// <summary>One Stat Group's season totals (D7's season block).</summary>
public sealed record SeasonGroup(StatGroup Group, IReadOnlyList<SeasonField> Fields);

/// <summary>
/// The season block (D7): the cached PlayerSeason row's totals organised by the same
/// seven Stat Groups as the Match page, the reliable caps total, and the one `ps`
/// average the API provides (average kicking metres). Null when no row is cached.
/// </summary>
public sealed record SeasonBlock(
    int Season,
    int TotalCaps,
    int? AverageKickingMetres,
    IReadOnlyList<SeasonGroup> Groups);

/// <summary>
/// The Player History page's pure pieces (D7): the per-fixture trends table (context
/// columns plus the selected Stat Group's fields, sortable) with a per-field header
/// sparkline, and the season block. It is a transformation of the cached
/// PlayerFixture rows, the cached Fixture / Summary rows and the cached PlayerSeason
/// row — no API client and no store, so the trends are cache-only by construction.
/// </summary>
public static class PlayerHistory
{
    /// <summary>The context column sort keys the trends table offers.</summary>
    public const string DateKey = "date";

    public const string SlotKey = "slot";
    public const string MinutesKey = GameReviewMatrix.MinutesKey;

    /// <summary>
    /// The trends table's rows, newest Fixture first: the context columns plus the
    /// selected Stat Group's field values. The history arrives newest-first from the
    /// cache read; opponent names and bot flags come from the captured TeamFacts.
    /// </summary>
    public static IReadOnlyList<TrendRow> BuildRows(
        IReadOnlyList<PlayerFixtureHistoryRow> history,
        int teamId,
        IReadOnlyDictionary<int, string> teamNames,
        IReadOnlyDictionary<int, bool> teamBots,
        StatGroup group)
    {
        var fields = GameReviewMatrix.FieldsFor(group);
        return history
            .Select(entry =>
            {
                var fixture = entry.Fixture;
                var opponentId = entry.IsHome ? fixture.GuestTeamId : fixture.HomeTeamId;
                var opponent = teamNames.TryGetValue(opponentId, out var name) && !string.IsNullOrWhiteSpace(name)
                    ? name
                    : $"Team {opponentId}";
                var opponentIsBot = teamBots.TryGetValue(opponentId, out var bot) && bot;
                var result = !entry.HasResult
                    ? string.Empty
                    : entry.Score > entry.OppositionScore ? "W" : entry.Score < entry.OppositionScore ? "L" : "D";
                var competition = fixture.Round > 0 ? $"{fixture.Competition} R{fixture.Round}" : fixture.Competition;

                return new TrendRow(
                    fixture.FixtureId,
                    DateTimeOffset.FromUnixTimeSeconds(fixture.MatchStartUnix).LocalDateTime,
                    competition,
                    opponent,
                    opponentIsBot,
                    result,
                    entry.Score,
                    entry.OppositionScore,
                    entry.Row.Slot,
                    entry.Row.MinutesPlayed,
                    fields.Select(field => field.Value(entry.Row)).ToList());
            })
            .ToList();
    }

    /// <summary>
    /// The trends table's column sort. A context key ("date", "slot", "minutes") or a
    /// selected-group field key sorts by that column; anything else (and a missing key)
    /// falls back to the default — date, newest first. Ties read newest first.
    /// </summary>
    public static IReadOnlyList<TrendRow> Sort(
        IReadOnlyList<TrendRow> rows, StatGroup group, string? sortKey, bool descending)
    {
        var key = sortKey?.Trim();
        var fields = GameReviewMatrix.FieldsFor(group);
        var fieldIndex = fields
            .Select((field, index) => (field.Key, index))
            .Where(entry => string.Equals(entry.Key, key, StringComparison.Ordinal))
            .Select(entry => (int?)entry.index)
            .FirstOrDefault();

        Func<TrendRow, IComparable> selector = key switch
        {
            SlotKey => row => row.Slot,
            MinutesKey => row => row.Minutes,
            DateKey => row => row.Date,
            _ => fieldIndex is int index ? row => row.Values[index] : row => row.Date
        };

        var ordered = descending ? rows.OrderByDescending(selector) : rows.OrderBy(selector);
        return ordered.ThenByDescending(row => row.Date).ToList();
    }

    /// <summary>
    /// One inline-SVG sparkline per Stat Group field, in field order, each drawn over
    /// the whole table's own rows in chronological order (oldest to newest) — so the
    /// shape of every field's trend reads in its column header regardless of the
    /// table's current sort. An empty string means fewer than two readings to draw.
    /// </summary>
    public static IReadOnlyList<string> Sparklines(IReadOnlyList<TrendRow> rows, StatGroup group)
    {
        var fields = GameReviewMatrix.FieldsFor(group);
        var chronological = rows.OrderBy(row => row.Date).ToList();
        return fields
            .Select((_, index) => SvgCharts.Sparkline(
                chronological.Select(row => (double?)row.Values[index]).ToList()))
            .ToList();
    }


    /// <summary>
    /// The season block from the cached PlayerSeason row, or null when none is cached.
    /// Totals are grouped by the same seven Stat Groups as the Match page; caps is the
    /// reliable one the `ps` read carries; average kicking metres is the single `ps`
    /// average the API provides (surfaced only when it is above zero).
    /// </summary>
    public static SeasonBlock? BuildSeasonBlock(int season, PlayerSeasonRow? row)
    {
        if (row is null)
        {
            return null;
        }

        var groups = GameReviewMatrix.Groups
            .Select(group => new SeasonGroup(
                group,
                GameReviewMatrix.FieldsFor(group)
                    .Select(field => new SeasonField(
                        field.Title,
                        SeasonValues.TryGetValue(field.Key, out var read) ? read(row) : 0))
                    .ToList()))
            .ToList();

        return new SeasonBlock(
            season,
            row.LeagueCaps + row.FriendlyCaps + row.CupCaps + row.UnderTwentyCaps
                + row.NationalCaps + row.WorldCupCaps + row.UnderTwentyWorldCupCaps + row.OtherCaps,
            row.AvKickingMetres > 0 ? row.AvKickingMetres : null,
            groups);
    }

    // The D4 field map read off a cached PlayerSeason row, keyed by the same field key
    // the Match page's tabs use - so the season block and the per-fixture tables name
    // the same fields. Only the map's keys are present.
    private static readonly IReadOnlyDictionary<string, Func<PlayerSeasonRow, int>> SeasonValues =
        new Dictionary<string, Func<PlayerSeasonRow, int>>
        {
            ["tries"] = row => row.Tries,
            ["try_assists"] = row => row.TryAssists,
            ["metres_gained"] = row => row.MetresGained,
            ["linebreaks"] = row => row.Linebreaks,
            ["beaten_defenders"] = row => row.BeatenDefenders,
            ["tackles"] = row => row.Tackles,
            ["missed_tackles"] = row => row.MissedTackles,
            ["turnovers"] = row => row.TurnoversWon,
            ["intercepts"] = row => row.Intercepts,
            ["kicks"] = row => row.Kicks,
            ["good_kicks"] = row => row.GoodKicks,
            ["bad_kicks"] = row => row.BadKicks,
            ["kicking_metres"] = row => row.KickingMetres,
            ["kicks_out_on_the_full"] = row => row.KicksOutOnTheFull,
            ["up_and_unders"] = row => row.UpAndUnders,
            ["good_up_and_unders"] = row => row.GoodUpAndUnders,
            ["bad_up_and_unders"] = row => row.BadUpAndUnders,
            ["conversions"] = row => row.Conversions,
            ["missed_conversions"] = row => row.MissedConversions,
            ["penalties"] = row => row.Penalties,
            ["missed_penalties"] = row => row.MissedPenalties,
            ["dropgoals"] = row => row.DropGoals,
            ["missed_dropgoals"] = row => row.MissedDropGoals,
            ["knockons"] = row => row.Knockons,
            ["handling_errors"] = row => row.HandlingErrors,
            ["forward_passes"] = row => row.ForwardPasses,
            ["penalties_conceded"] = row => row.PenaltiesConceded,
            ["yellow_cards"] = row => row.YellowCards,
            ["red_cards"] = row => row.RedCards,
            ["fights"] = row => row.Fights,
            ["lineouts_secured"] = row => row.LineoutsSecured,
            ["lineouts_conceded"] = row => row.LineoutsConceded,
            ["lineouts_stolen"] = row => row.LineoutsStolen,
            ["successful_lineout_throws"] = row => row.SuccessfulLineoutThrows,
            ["unsuccessful_lineout_throws"] = row => row.UnsuccessfulLineoutThrows,
            ["injuries"] = row => row.Injuries,
            ["ball_time"] = row => row.BallTime
        };


}

