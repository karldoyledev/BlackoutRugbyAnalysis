using BlackoutRugbyDashboard.Data;
using BlackoutRugbyDashboard.Models;

namespace BlackoutRugbyDashboard.Services;

/// <summary>
/// One of the seven named Stat Groups (Context.md) that organise per-Fixture
/// Player output — the Game review card's tabs, in the locked D4 order.
/// </summary>
public enum StatGroup
{
    Attack,
    Defence,
    Kicking,
    Handling,
    Discipline,
    Lineout,
    Other
}

/// <summary>One column of a Stat Group's tab: the `fs` field it reads verbatim
/// (D4's locked field map). <paramref name="Title"/> is the full name for the
/// header's tooltip; <paramref name="Label"/> is the compact header.</summary>
public sealed record GameReviewField(string Key, string Title, string Label, Func<PlayerFixtureRow, int> Value);

/// <summary>
/// One Player's row in the Game review matrix: the always-visible columns (D4) and
/// the selected tab's values, in that tab's field order. <see cref="Csr"/> comes
/// from the latest Squad Snapshot and is null when the Player is absent from it —
/// the API has no per-Fixture CSR, so it is blank rather than invented.
/// </summary>
public sealed record GameReviewRow(
    int PlayerId,
    int Slot,
    string Name,
    bool IsBench,
    int Minutes,
    int Points,
    int EnergyAfter,
    int? Csr,
    IReadOnlyList<int> Values);

/// <summary>
/// The Game review matrix (S3 + D4, build slice #35): the locked seven Stat-Group
/// field map, the always-visible columns, XV/Bench marking, and sorting. It is a
/// pure transformation of cached PlayerFixture rows plus the latest capture — no
/// API client and no store, so the card is cache-only by construction.
/// </summary>
public static class GameReviewMatrix
{
    /// <summary>The always-visible output columns (D4).</summary>
    public const string MinutesKey = "minutes_played";

    public const string PointsKey = "total_points";

    public const string EnergyAfterKey = "energy_after";

    /// <summary>The comparison column: current-state only, from the latest capture.</summary>
    public const string CsrKey = "csr";

    /// <summary>The XV/Bench boundary: slots 1–15 start, 16–23 are the bench.</summary>
    public const int BenchStartsAt = 16;

    public static IReadOnlyList<StatGroup> Groups { get; } = new[]
    {
        StatGroup.Attack,
        StatGroup.Defence,
        StatGroup.Kicking,
        StatGroup.Handling,
        StatGroup.Discipline,
        StatGroup.Lineout,
        StatGroup.Other
    };

    private static readonly IReadOnlyDictionary<StatGroup, string> GroupKeys = new Dictionary<StatGroup, string>
    {
        [StatGroup.Attack] = "attack",
        [StatGroup.Defence] = "defence",
        [StatGroup.Kicking] = "kicking",
        [StatGroup.Handling] = "handling",
        [StatGroup.Discipline] = "discipline",
        [StatGroup.Lineout] = "lineout",
        [StatGroup.Other] = "other"
    };

    private static readonly IReadOnlyDictionary<StatGroup, string> GroupTitles = new Dictionary<StatGroup, string>
    {
        [StatGroup.Attack] = "Attack",
        [StatGroup.Defence] = "Defence",
        [StatGroup.Kicking] = "Kicking",
        [StatGroup.Handling] = "Handling",
        [StatGroup.Discipline] = "Discipline",
        [StatGroup.Lineout] = "Lineout",
        [StatGroup.Other] = "Other"
    };

    /// <summary>The locked D4 field→tab map: 37 verbatim `fs` fields. Excluded by
    /// decision: the 8 unreliable caps fields, `played`, and `energy_before`
    /// (cached, never shown).</summary>
    private static readonly IReadOnlyDictionary<StatGroup, IReadOnlyList<GameReviewField>> Map =
        new Dictionary<StatGroup, IReadOnlyList<GameReviewField>>
        {
            [StatGroup.Attack] = new GameReviewField[]
            {
                new("tries", "Tries", "Tr", row => row.Tries),
                new("try_assists", "Try assists", "TryA", row => row.TryAssists),
                new("metres_gained", "Metres gained", "Mtr", row => row.MetresGained),
                new("linebreaks", "Linebreaks", "LB", row => row.Linebreaks),
                new("beaten_defenders", "Beaten defenders", "Btn", row => row.BeatenDefenders)
            },
            [StatGroup.Defence] = new GameReviewField[]
            {
                new("tackles", "Tackles", "Tkl", row => row.Tackles),
                new("missed_tackles", "Missed tackles", "M-Tkl", row => row.MissedTackles),
                new("turnovers", "Turnovers", "TO", row => row.Turnovers),
                new("intercepts", "Intercepts", "Int", row => row.Intercepts)
            },
            [StatGroup.Kicking] = new GameReviewField[]
            {
                new("kicks", "Kicks", "Kk", row => row.Kicks),
                new("good_kicks", "Good kicks", "G-Kk", row => row.GoodKicks),
                new("bad_kicks", "Bad kicks", "B-Kk", row => row.BadKicks),
                new("kicking_metres", "Kicking metres", "K-Mtr", row => row.KickingMetres),
                new("kicks_out_on_the_full", "Kicks out on the full", "OF", row => row.KicksOutOnTheFull),
                new("up_and_unders", "Up and unders", "U&U", row => row.UpAndUnders),
                new("good_up_and_unders", "Good up and unders", "G-U&U", row => row.GoodUpAndUnders),
                new("bad_up_and_unders", "Bad up and unders", "B-U&U", row => row.BadUpAndUnders),
                new("conversions", "Conversions", "Cv", row => row.Conversions),
                new("missed_conversions", "Missed conversions", "M-Cv", row => row.MissedConversions),
                new("penalties", "Penalties", "Pn", row => row.Penalties),
                new("missed_penalties", "Missed penalties", "M-Pn", row => row.MissedPenalties),
                new("dropgoals", "Drop goals", "DG", row => row.DropGoals),
                new("missed_dropgoals", "Missed drop goals", "M-DG", row => row.MissedDropGoals)
            },
            [StatGroup.Handling] = new GameReviewField[]
            {
                new("knockons", "Knock ons", "KO", row => row.Knockons),
                new("handling_errors", "Handling errors", "HE", row => row.HandlingErrors),
                new("forward_passes", "Forward passes", "FP", row => row.ForwardPasses)
            },
            [StatGroup.Discipline] = new GameReviewField[]
            {
                new("penalties_conceded", "Penalties conceded", "PC", row => row.PenaltiesConceded),
                new("yellow_cards", "Yellow cards", "YC", row => row.YellowCards),
                new("red_cards", "Red cards", "RC", row => row.RedCards),
                new("fights", "Fights", "Ft", row => row.Fights)
            },
            [StatGroup.Lineout] = new GameReviewField[]
            {
                new("lineouts_secured", "Lineouts secured", "Sec", row => row.LineoutsSecured),
                new("lineouts_conceded", "Lineouts conceded", "Con", row => row.LineoutsConceded),
                new("lineouts_stolen", "Lineouts stolen", "Stl", row => row.LineoutsStolen),
                new("successful_lineout_throws", "Successful lineout throws", "S-Thr", row => row.SuccessfulLineoutThrows),
                new("unsuccessful_lineout_throws", "Unsuccessful lineout throws", "U-Thr", row => row.UnsuccessfulLineoutThrows)
            },
            [StatGroup.Other] = new GameReviewField[]
            {
                new("injuries", "Injuries", "Inj", row => row.Injuries),
                new("ball_time", "Ball time", "BallT", row => row.BallTime)
            }
        };

    public static string KeyFor(StatGroup group) => GroupKeys[group];

    public static string TitleFor(StatGroup group) => GroupTitles[group];

    public static StatGroup ParseGroup(string? key) =>
        Groups.FirstOrDefault(group => string.Equals(KeyFor(group), key?.Trim(), StringComparison.OrdinalIgnoreCase),
            StatGroup.Attack);

    public static IReadOnlyList<GameReviewField> FieldsFor(StatGroup group) => Map[group];

    /// <summary>
    /// The matrix rows for one cached Fixture, in slot order: every squad slot the
    /// cache holds for our side, XV/Bench marked, the always-visible columns read
    /// verbatim, the CSR column from the latest capture (blank when the Player is
    /// absent from it), and <paramref name="group"/>'s values in field order.
    /// </summary>
    public static IReadOnlyList<GameReviewRow> BuildRows(
        SquadFixtureRows fixture, TeamSnapshot? capture, StatGroup group = StatGroup.Attack)
    {
        var captured = capture?.Players.ToDictionary(player => player.Id) ?? new Dictionary<int, PlayerSnapshotRecord>();
        var fields = FieldsFor(group);

        return fixture.Players
            .OrderBy(row => row.Slot)
            .Select(row =>
            {
                captured.TryGetValue(row.PlayerId, out var player);
                return new GameReviewRow(
                    row.PlayerId,
                    row.Slot,
                    player?.Name is { Length: > 0 } name ? name : $"Player {row.PlayerId}",
                    row.Slot >= BenchStartsAt,
                    row.MinutesPlayed,
                    row.TotalPoints,
                    row.EnergyAfter,
                    capture is null ? null : player?.Csr,
                    fields.Select(field => field.Value(row)).ToList());
            })
            .ToList();
    }

    /// <summary>
    /// Slot order by default; any column of the selected tab sorts, and so do the
    /// always-visible outputs. An unknown key (a field belonging to another tab, or
    /// nonsense) leaves slot order alone rather than sorting by something invisible.
    /// </summary>
    public static IReadOnlyList<GameReviewRow> Sort(
        IReadOnlyList<GameReviewRow> rows, StatGroup group, string? sortKey, bool descending)
    {
        var fields = FieldsFor(group);
        var index = sortKey is null ? -1 : fields.ToList().FindIndex(field => field.Key == sortKey);
        if (index >= 0)
        {
            var ordered = descending
                ? rows.OrderByDescending(row => row.Values[index])
                : rows.OrderBy(row => row.Values[index]);
            return ordered.ThenBy(row => row.Slot).ToList();
        }

        Func<GameReviewRow, int>? output = sortKey switch
        {
            MinutesKey => row => row.Minutes,
            PointsKey => row => row.Points,
            EnergyAfterKey => row => row.EnergyAfter,
            CsrKey => row => row.Csr ?? int.MinValue,
            _ => null
        };

        if (output is null)
        {
            return rows.OrderBy(row => row.Slot).ToList();
        }

        var sorted = descending ? rows.OrderByDescending(output) : rows.OrderBy(output);
        return sorted.ThenBy(row => row.Slot).ToList();
    }
}
