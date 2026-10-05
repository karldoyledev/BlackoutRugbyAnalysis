using BlackoutRugbyDashboard.Data;

namespace BlackoutRugbyDashboard.Services;

/// <summary>
/// The C7 results strip (S7, build slice #41): the cached window as navigation,
/// not analysis — one slim row per completed Fixture, newest first, carrying the
/// date, the competition and round, the opponent (BOT marked), the result and the
/// winning margin. It reads the page's cached window, cached team names and
/// captured bot flags and nothing else, so it makes no API call. The 12-column
/// stat table it replaces was C6's charts re-plotted as text; Match Analysis owns
/// per-fixture detail.
/// </summary>
public static class SquadResults
{
    /// <summary>The strip's rows, newest Fixture first — the order the cached window reads in.</summary>
    public static IReadOnlyList<ResultRow> Build(SquadPageData page) =>
        page.Window
            .OrderByDescending(entry => entry.Fixture.MatchStartUnix)
            .Select(entry => BuildRow(page, entry))
            .ToList();

    private static ResultRow BuildRow(SquadPageData page, SquadFixtureRows entry)
    {
        var row = entry.Fixture;
        var isHome = row.HomeTeamId == page.TeamId;
        var opponent = page.OpponentNameFor(row);

        // The opponent's bot flag rides its captured TeamFact; a team we have never
        // read carries no flag, so the tag is simply absent rather than guessed.
        var opponentIsBot = page.TeamBots.TryGetValue(page.OpponentIdFor(row), out var isBot) && isBot;

        // The score is our side's, whichever end of the Fixture we played. No cached
        // Match Summary means no result to report — never a made-up 0–0.
        var score = entry.Summary is null ? 0 : isHome ? entry.Summary.HomePoints : entry.Summary.GuestPoints;
        var oppositionScore = entry.Summary is null ? 0 : isHome ? entry.Summary.GuestPoints : entry.Summary.HomePoints;
        var result = entry.Summary is null
            ? string.Empty
            : score > oppositionScore ? "W" : score < oppositionScore ? "L" : "D";

        return new ResultRow(
            row.FixtureId,
            DateTimeOffset.FromUnixTimeSeconds(row.MatchStartUnix).LocalDateTime,
            row.Competition,
            row.Round,
            opponent,
            opponentIsBot,
            result,
            score,
            oppositionScore);
    }
}

/// <summary>
/// One row of the C7 results strip (S7): what the cached window says about one
/// completed Fixture, in the words the strip reads it in.
/// </summary>
public sealed record ResultRow(
    int FixtureId,
    DateTime Date,
    string Competition,
    int Round,
    string Opponent,
    bool OpponentIsBot,
    string Result,
    int Score,
    int OppositionScore)
{
    /// <summary>True when the Fixture's Match Summary is cached; otherwise the strip
    /// shows no result rather than a made-up 0–0.</summary>
    public bool HasResult => Result.Length > 0;

    /// <summary>Winning margin from our side; meaningful only when <see cref="HasResult"/>.</summary>
    public int Margin => Score - OppositionScore;

    /// <summary>The competition and its round, as the strip reads them ("League R3").</summary>
    public string CompetitionLabel => Round > 0 ? $"{Competition} R{Round}" : Competition;

    /// <summary>The margin as a signed word ("+14", "-7", "0").</summary>
    public string MarginText => Margin > 0 ? $"+{Margin}" : Margin.ToString();

    /// <summary>The margin's tone class — a win, a loss, or a draw.</summary>
    public string MarginClass => Margin > 0 ? "margin-win" : Margin < 0 ? "margin-loss" : "margin-flat";
}