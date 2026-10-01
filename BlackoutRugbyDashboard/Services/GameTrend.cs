using BlackoutRugbyDashboard.Data;
using BlackoutRugbyDashboard.Models;

namespace BlackoutRugbyDashboard.Services;

/// <summary>
/// One pickable Trend stat (S3, build slice #37): the field key the query string
/// carries, its picker label, the optgroup it sits under, and the cached
/// PlayerFixture field it reads. Every charted field — the D4 Stat-Group map —
/// has one, plus the three always-visible outputs (minutes, points, energy-after).
/// </summary>
public sealed record GameTrendStat(string Key, string Label, string Group, Func<PlayerFixtureRow, int> Value);

/// <summary>
/// One Player's row in the Trend view (S3, #37): their series across the cached
/// window (oldest to newest, a null for a Fixture they did not play — a gap, never
/// a fabricated zero), the latest reading they have, its movement against their
/// previous reading, and the window total. Every figure is null when this Player
/// has no reading for the chosen stat anywhere in the window.
/// </summary>
public sealed record GameTrendRow(
    int PlayerId,
    string Name,
    int Slot,
    IReadOnlyList<double?> Series,
    int? Latest,
    int? Delta,
    int? Season);

/// <summary>
/// The Game review card's second mode (S3 / #37): every squad member's series of
/// one chosen stat across the whole cached window, oldest to newest, across
/// competitions — the picker spans the D4 field map plus the three output columns.
/// It is a pure transformation of the page's cached window and the latest capture —
/// no API client and no store — so the trend is cache-only by construction.
/// </summary>
public static class GameTrend
{
    /// <summary>The optgroup the three always-visible outputs sit under (S3): the
    /// prototype's "Output" bucket, kept so the picker reads like the card.</summary>
    public const string OutputGroup = "Output";

    /// <summary>Every pickable stat: the three outputs, then the D4 field map in
    /// Stat-Group order. Built from <see cref="GameReviewMatrix"/>'s own map, so the
    /// two views can never drift apart.</summary>
    public static IReadOnlyList<GameTrendStat> Stats { get; } = BuildStats();

    public static GameTrendStat DefaultStat => Stats[0];

    /// <summary>The stat a key names, falling back to the first option so a
    /// hand-edited query string can never blank the card.</summary>
    public static GameTrendStat Resolve(string? key) =>
        Stats.FirstOrDefault(stat => string.Equals(stat.Key, key?.Trim(), StringComparison.Ordinal)) ?? DefaultStat;

    /// <summary>
    /// Every squad member's series for one stat across the cached window, oldest
    /// Fixture to newest. The window arrives newest-first (the Squad page reader),
    /// so it is reversed here. Rows read in squad order — the slot the Player last
    /// appeared in, then id — so the XV surfaces above the bench.
    /// </summary>
    public static IReadOnlyList<GameTrendRow> Build(SquadPageData page, string? statKey)
    {
        var stat = Resolve(statKey);
        var fixtures = page.Window.Reverse().ToList();
        if (fixtures.Count == 0)
        {
            return Array.Empty<GameTrendRow>();
        }

        var captured = page.Capture?.Players.ToDictionary(player => player.Id)
            ?? new Dictionary<int, PlayerSnapshotRecord>();

        var order = new List<int>();
        var series = new Dictionary<int, double?[]>();
        var latestSlot = new Dictionary<int, int>();

        for (var index = 0; index < fixtures.Count; index++)
        {
            foreach (var row in fixtures[index].Players.OrderBy(row => row.Slot))
            {
                if (!series.TryGetValue(row.PlayerId, out var values))
                {
                    values = new double?[fixtures.Count];
                    series[row.PlayerId] = values;
                    order.Add(row.PlayerId);
                }

                values[index] = stat.Value(row);
                latestSlot[row.PlayerId] = row.Slot;
            }
        }

        return order
            .Select(playerId =>
            {
                var values = series[playerId];
                var (latest, delta, season) = Summarize(values);
                captured.TryGetValue(playerId, out var player);
                var name = player?.Name is { Length: > 0 } capturedName ? capturedName : $"Player {playerId}";
                return new GameTrendRow(playerId, name, latestSlot[playerId], values, latest, delta, season);
            })
            .OrderBy(row => row.Slot)
            .ThenBy(row => row.PlayerId)
            .ToList();
    }

    /// <summary>The three outputs first (S3: minutes, points and energy-after), then
    /// every Stat Group's locked field set in order.</summary>
    private static IReadOnlyList<GameTrendStat> BuildStats()
    {
        var stats = new List<GameTrendStat>
        {
            new(GameReviewMatrix.MinutesKey, "Minutes", OutputGroup, row => row.MinutesPlayed),
            new(GameReviewMatrix.PointsKey, "Points", OutputGroup, row => row.TotalPoints),
            new(GameReviewMatrix.EnergyAfterKey, "Energy after", OutputGroup, row => row.EnergyAfter)
        };

        foreach (var group in GameReviewMatrix.Groups)
        {
            foreach (var field in GameReviewMatrix.FieldsFor(group))
            {
                stats.Add(new GameTrendStat(field.Key, field.Title, GameReviewMatrix.TitleFor(group), field.Value));
            }
        }

        return stats;
    }

    /// <summary>
    /// The latest reading a Player has, its movement since their previous reading,
    /// and the window total. A reading is skipped when it is a gap (the Player did
    /// not play that Fixture), so the movement reads between the two most recent
    /// games this Player actually featured in. No reading at all is all three nulls;
    /// one reading has a latest and a total but a dash for movement.
    /// </summary>
    private static (int? Latest, int? Delta, int? Season) Summarize(IReadOnlyList<double?> series)
    {
        var readings = series
            .Where(value => value.HasValue)
            .Select(value => (int)value!.Value)
            .ToList();

        if (readings.Count == 0)
        {
            return (null, null, null);
        }

        var latest = readings[^1];
        int? delta = readings.Count >= 2 ? latest - readings[^2] : null;
        return (latest, delta, readings.Sum());
    }
}
