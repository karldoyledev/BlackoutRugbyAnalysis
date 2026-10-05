using System.Globalization;
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

/// <summary>
/// How every stat cell reads (S3, build slice #36): the game's value, the value
/// with its movement beside it, or the movement alone.
/// </summary>
public enum DeltaMode
{
    Values,
    Both,
    Delta
}

/// <summary>
/// The two views of the Game review card (S3, build slice #37): the per-game
/// matrix (one Fixture, every squad slot, by Stat Group) or the trend (one stat,
/// every squad member, across the whole cached window). The second mode lives on
/// the same card, chosen by a query-string link.
/// </summary>
public enum GameReviewView
{
    Matrix,
    Trend
}

/// <summary>One column of a Stat Group's tab: the `fs` field it reads verbatim
/// (D4's locked field map). <paramref name="Title"/> is the full name for the
/// header's tooltip; <paramref name="Label"/> is the compact header.</summary>
public sealed record GameReviewField(string Key, string Title, string Label, Func<PlayerFixtureRow, int> Value);

/// <summary>
/// One rendered stat cell: the value as it reads, and how it moved against the
/// previous cached game (null when there is nothing to compare). <paramref name="TracksHistory"/>
/// is false for the CSR column — present state from the latest capture, with no
/// per-Fixture history in the API, so it shows no delta and never a dash.
/// </summary>
public sealed record GameReviewCell(string Text, int? Delta, bool TracksHistory = true);

/// <summary>
/// One Player's row in the Game review matrix: the always-visible columns (D4) and
/// the selected tab's values, in that tab's field order, each carrying its delta
/// against the previous cached game (null where the previous game holds no row for
/// this Player). <see cref="Csr"/> comes from the latest Squad Snapshot and is null
/// when the Player is absent from it — the API has no per-Fixture CSR, so it is
/// blank rather than invented.
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
    IReadOnlyList<int> Values,
    int? MinutesDelta,
    int? PointsDelta,
    int? EnergyAfterDelta,
    IReadOnlyList<int?> ValueDeltas)
{
    /// <summary>True when the previous cached game holds a row for this Player, so
    /// the delta columns have an answer instead of a dash.</summary>
    public bool HasPrevious => MinutesDelta is not null;

    /// <summary>The view's cell sequence: the three always-visible outputs, then CSR,
    /// then the selected tab's fields in field order.</summary>
    public IReadOnlyList<GameReviewCell> Cells { get; } =
        new[]
            {
                new GameReviewCell(Text(Minutes), MinutesDelta),
                new GameReviewCell(Text(Points), PointsDelta),
                new GameReviewCell(Text(EnergyAfter), EnergyAfterDelta),
                new GameReviewCell(Csr is { } csr ? Text(csr) : "—", null, TracksHistory: false)
            }
            .Concat(Values.Select((value, index) => new GameReviewCell(Text(value), ValueDeltas[index])))
            .ToList();

    private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// One half of a tab's Coach's eye: the metric the tab is judged on and the Player
/// who led it in the chosen game. <paramref name="IsAdverse"/> marks the wrong-way
/// metric, where the highest value is the worst news.
/// </summary>
public sealed record CoachEyeItem(
    string FieldKey, string Label, bool IsAdverse, int PlayerId, string PlayerName, int Value);

/// <summary>
/// The Game review matrix (S3 + D4, build slices #35 and #36): the locked seven
/// Stat-Group field map, the always-visible columns, XV/Bench marking, sorting, the
/// delta against the previous cached game and the Coach's eye. It is a pure
/// transformation of cached PlayerFixture rows plus the latest capture — no API
/// client and no store, so the card is cache-only by construction.
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

    private static readonly IReadOnlyDictionary<DeltaMode, string> ModeKeys = new Dictionary<DeltaMode, string>
    {
        [DeltaMode.Values] = "values",
        [DeltaMode.Both] = "both",
        [DeltaMode.Delta] = "delta"
    };

    /// <summary>The mode the card opens in — the prototype's default: the game's value
    /// with its movement beside it.</summary>
    public const DeltaMode DefaultMode = DeltaMode.Both;

    public static string KeyFor(DeltaMode mode) => ModeKeys[mode];

    public static DeltaMode ParseMode(string? key)
    {
        foreach (var entry in ModeKeys)
        {
            if (string.Equals(entry.Value, key?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return entry.Key;
            }
        }

        return DefaultMode;
    }

    private static readonly IReadOnlyDictionary<GameReviewView, string> ViewKeys = new Dictionary<GameReviewView, string>
    {
        [GameReviewView.Matrix] = "matrix",
        [GameReviewView.Trend] = "trend"
    };

    /// <summary>The view the card opens in — the per-game matrix, the prototype's default.</summary>
    public const GameReviewView DefaultView = GameReviewView.Matrix;

    public static string KeyFor(GameReviewView view) => ViewKeys[view];

    public static GameReviewView ParseView(string? key)
    {
        foreach (var entry in ViewKeys)
        {
            if (string.Equals(entry.Value, key?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return entry.Key;
            }
        }

        return DefaultView;
    }

    /// <summary>The movement as it reads: a signed integer, or an em dash when there
    /// is no previous game — or no row for this Player in it — to compare against.</summary>
    public static string DeltaText(int? delta) => delta switch
    {
        null => "—",
        > 0 => $"+{delta}",
        _ => delta.Value.ToString(CultureInfo.InvariantCulture)
    };

    /// <summary>The theme's movement class: up, down, flat (a zero — a real answer),
    /// or blank (nothing to compare).</summary>
    public static string DeltaClass(int? delta) => delta switch
    {
        null => "delta-blank",
        > 0 => "delta-up",
        < 0 => "delta-down",
        _ => "delta-flat"
    };

    public static IReadOnlyList<GameReviewField> FieldsFor(StatGroup group) => Map[group];

    /// <summary>
    /// The matrix rows for one cached Fixture, in slot order: every squad slot the
    /// cache holds for our side, XV/Bench marked, the always-visible columns read
    /// verbatim, the CSR column from the latest capture (blank when the Player is
    /// absent from it), and <paramref name="group"/>'s values in field order. When
    /// <paramref name="previous"/> — the next completed Fixture in the cached window,
    /// oldest-going-backwards — is given, every value also carries this game minus
    /// that one; a Player with no row there carries a null (a dash), never a zero.
    /// </summary>
    public static IReadOnlyList<GameReviewRow> BuildRows(
        SquadFixtureRows fixture,
        TeamSnapshot? capture,
        StatGroup group = StatGroup.Attack,
        SquadFixtureRows? previous = null)
    {
        var captured = capture?.Players.ToDictionary(player => player.Id) ?? new Dictionary<int, PlayerSnapshotRecord>();
        var before = previous?.Players.ToDictionary(row => row.PlayerId) ?? new Dictionary<int, PlayerFixtureRow>();
        var fields = FieldsFor(group);

        return fixture.Players
            .OrderBy(row => row.Slot)
            .Select(row =>
            {
                captured.TryGetValue(row.PlayerId, out var player);
                before.TryGetValue(row.PlayerId, out var earlier);

                return new GameReviewRow(
                    row.PlayerId,
                    row.Slot,
                    player?.Name is { Length: > 0 } name ? name : $"Player {row.PlayerId}",
                    row.Slot >= BenchStartsAt,
                    row.MinutesPlayed,
                    row.TotalPoints,
                    row.EnergyAfter,
                    capture is null ? null : player?.Csr,
                    fields.Select(field => field.Value(row)).ToList(),
                    Delta(row.MinutesPlayed, earlier?.MinutesPlayed),
                    Delta(row.TotalPoints, earlier?.TotalPoints),
                    Delta(row.EnergyAfter, earlier?.EnergyAfter),
                    fields.Select(field => Delta(field.Value(row), earlier is null ? null : field.Value(earlier))).ToList());
            })
            .ToList();

        static int? Delta(int value, int? earlier) => earlier is { } previousValue ? value - previousValue : null;
    }

    /// <summary>
    /// Slot order by default; any column of the selected tab sorts, and so do the
    /// always-visible outputs. An unknown key (a field belonging to another tab, or
    /// nonsense) leaves slot order alone rather than sorting by something invisible.
    /// Sorting never disturbs a row's deltas — they travel with the row.
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

    /// <summary>
    /// The locked per-tab metric pair (standout, adverse). The Squad page design
    /// map's choices (the S3 decision, #22), extended to the one tab the prototype
    /// left out (Other: time on the ball, injuries). Null is honest rather than
    /// invented: D4's field map holds no adverse metric in Attack and no headline
    /// metric in Handling or Discipline, so those tabs open with the half they have.
    /// </summary>
    private static readonly IReadOnlyDictionary<StatGroup, (string? Standout, string? Adverse)> CoachEyeMetrics =
        new Dictionary<StatGroup, (string?, string?)>
        {
            [StatGroup.Attack] = ("tries", null),
            [StatGroup.Defence] = ("tackles", "missed_tackles"),
            [StatGroup.Kicking] = ("good_kicks", "bad_kicks"),
            [StatGroup.Handling] = (null, "knockons"),
            [StatGroup.Discipline] = (null, "penalties_conceded"),
            [StatGroup.Lineout] = ("lineouts_secured", "lineouts_conceded"),
            [StatGroup.Other] = ("ball_time", "injuries")
        };

    /// <summary>The field key a tab is judged on, or null when its metric map has
    /// nothing for that side.</summary>
    public static string? CoachEyeMetricFor(StatGroup group, bool adverse) =>
        CoachEyeMetrics.TryGetValue(group, out var metrics)
            ? adverse ? metrics.Adverse : metrics.Standout
            : null;

    /// <summary>
    /// The Coach's eye for one game and tab: the standout on the tab's headline
    /// metric, then the worst offender on its adverse metric. Only a positive value
    /// qualifies — a zero is absence, not performance — and ties go to the lower
    /// slot. A tab whose whole game was zeros (or which judges on nothing) opens
    /// with no line rather than a made-up name.
    /// </summary>
    public static IReadOnlyList<CoachEyeItem> BuildCoachEye(IReadOnlyList<GameReviewRow> rows, StatGroup group)
    {
        var fields = FieldsFor(group);
        var items = new List<CoachEyeItem>();

        foreach (var adverse in new[] { false, true })
        {
            if (CoachEyeMetricFor(group, adverse) is not { } key)
            {
                continue;
            }

            var index = fields.ToList().FindIndex(field => field.Key == key);
            if (index < 0)
            {
                continue;
            }

            var leader = rows
                .Where(row => row.Values[index] > 0)
                .OrderByDescending(row => row.Values[index])
                .ThenBy(row => row.Slot)
                .FirstOrDefault();

            if (leader is not null)
            {
                items.Add(new CoachEyeItem(
                    key, fields[index].Title, adverse, leader.PlayerId, leader.Name, leader.Values[index]));
            }
        }

        return items;
    }
}
