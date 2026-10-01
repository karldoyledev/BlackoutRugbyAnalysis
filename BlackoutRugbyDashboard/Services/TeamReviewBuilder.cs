using System.Globalization;
using BlackoutRugbyDashboard.Data;

namespace BlackoutRugbyDashboard.Services;

/// <summary>
/// One tab of the Team review card (S6): a set of eight small-multiple charts over
/// the cached window. #38 built Match control, #39 the Attack tab, #40 the
/// Defence/discipline/kicking tab.
/// </summary>
public enum TeamReviewTab
{
    MatchControl,
    Attack,
    Defence
}

/// <summary>
/// The Team review card's builder (S6 / #38): pure, cache-only turns of the
/// cached window into eight team trends per tab. It reads the window's team-stat
/// rows (both sides, from the bare <c>fs</c> blocks) and the Match Summary's final
/// points and nothing else — no API client exists here, so "cache-only" is
/// structural rather than asserted. Possession, territory and minutes-in-22 are
/// rendered as shares of the Fixture total, exactly as the card's note promises.
/// </summary>
public static class TeamReviewBuilder
{
    public const string MatchControlKey = "control";
    public const string AttackKey = "attack";
    public const string DefenceKey = "defence";

    public static TeamReviewTab DefaultTab => TeamReviewTab.MatchControl;

    /// <summary>The tabs this slice can render, in the card's order. Only the
    /// implemented ones are listed, so the tab bar never offers an empty chart.</summary>
    public static IReadOnlyList<TeamReviewTab> Tabs { get; } = new[]
    {
        TeamReviewTab.MatchControl, TeamReviewTab.Attack, TeamReviewTab.Defence
    };

    public static string KeyFor(TeamReviewTab tab) => tab switch
    {
        TeamReviewTab.Attack => AttackKey,
        TeamReviewTab.Defence => DefenceKey,
        _ => MatchControlKey
    };

    public static string LabelFor(TeamReviewTab tab) => tab switch
    {
        TeamReviewTab.Attack => "Attack",
        TeamReviewTab.Defence => "Defence, discipline & kicking",
        _ => "Match control"
    };

    public static TeamReviewTab Parse(string? key) => key?.Trim().ToLowerInvariant() switch
    {
        AttackKey => TeamReviewTab.Attack,
        DefenceKey => TeamReviewTab.Defence,
        _ => DefaultTab
    };

    /// <summary>The tab's eight charts, oldest Fixture to newest, each with its own
    /// scale and its points' values readable on hover.</summary>
    public static IReadOnlyList<SvgChart> Build(SquadPageData page, TeamReviewTab tab) => tab switch
    {
        TeamReviewTab.Attack => BuildAttack(page),
        TeamReviewTab.Defence => BuildDefence(page),
        _ => BuildMatchControl(page)
    };

    /// <summary>The x-axis labels for a window already in oldest-to-newest order:
    /// each Fixture's start date, or an em dash when it is unset.</summary>
    private static IReadOnlyList<string> LabelsFor(IReadOnlyList<SquadFixtureRows> fixtures) =>
        fixtures
            .Select(entry => entry.Fixture.MatchStartUnix > 0
                ? DateTimeOffset.FromUnixTimeSeconds(entry.Fixture.MatchStartUnix)
                    .LocalDateTime.ToString("MMM d", CultureInfo.InvariantCulture)
                : "—")
            .ToList();

    private static IReadOnlyList<SvgChart> BuildMatchControl(SquadPageData page)
    {
        // The window arrives newest-first; every chart runs oldest to newest.
        var fixtures = page.Window.Reverse().ToList();
        if (fixtures.Count == 0)
        {
            return Array.Empty<SvgChart>();
        }

        var labels = LabelsFor(fixtures);

        var pointsFor = new List<double?>();
        var pointsAgainst = new List<double?>();
        var possession = new List<double?>();
        var territory = new List<double?>();
        var lineoutWin = new List<double?>();
        var scrumWin = new List<double?>();
        var penaltiesConceded = new List<double?>();
        var penaltiesWon = new List<double?>();
        var turnoversConceded = new List<double?>();
        var turnoversWon = new List<double?>();
        var minutesIn22 = new List<double?>();

        foreach (var entry in fixtures)
        {
            // Points are the Match Summary's: our side's score regardless of venue.
            var summary = entry.Summary;
            if (summary is null)
            {
                pointsFor.Add(null);
                pointsAgainst.Add(null);
            }
            else
            {
                var isHome = entry.Fixture.HomeTeamId == page.TeamId;
                pointsFor.Add(isHome ? summary.HomePoints : summary.GuestPoints);
                pointsAgainst.Add(isHome ? summary.GuestPoints : summary.HomePoints);
            }

            var ours = entry.TeamStats;
            var theirs = entry.OpponentStats;

            possession.Add(Share(ours?.Possession, theirs?.Possession));
            territory.Add(Share(ours?.Territory, theirs?.Territory));
            minutesIn22.Add(Share(ours?.MinutesIn22, theirs?.MinutesIn22));
            lineoutWin.Add(Share(ours?.LineoutsWon, ours?.LineoutsLost));
            scrumWin.Add(Share(ours?.ScrumsWon, ours?.ScrumsLost));

            penaltiesConceded.Add(ours?.PenaltiesConceded);
            penaltiesWon.Add(ours?.PenaltiesWon);
            turnoversConceded.Add(ours?.TurnoversConceded);
            turnoversWon.Add(ours?.Turnovers);
        }

        return new[]
        {
            Chart("Points for / against", labels,
                Series("Points for", ChartTone.Us, pointsFor),
                Series("Points against", ChartTone.Opponent, pointsAgainst)),
            Chart("Possession %", labels, Series("Possession", ChartTone.Us, possession)),
            Chart("Territory %", labels, Series("Territory", ChartTone.Us, territory)),
            Chart("Lineout win %", labels, Series("Lineout win", ChartTone.Us, lineoutWin)),
            Chart("Scrum win %", labels, Series("Scrum win", ChartTone.Us, scrumWin)),
            Chart("Penalties conceded / won", labels,
                Series("Conceded", ChartTone.Opponent, penaltiesConceded),
                Series("Won", ChartTone.Us, penaltiesWon)),
            Chart("Turnovers conceded / won", labels,
                Series("Conceded", ChartTone.Opponent, turnoversConceded),
                Series("Won", ChartTone.Us, turnoversWon)),
            Chart("Minutes in 22 %", labels, Series("Minutes in 22", ChartTone.Us, minutesIn22))
        };
    }

    /// <summary>
    /// The Attack tab (S6 / #39): the eight attacking outputs, our side against the
    /// opponent's, oldest Fixture to newest — straight reads of the cached bare-fs
    /// team rows, no computed shares and no invented fields. A Fixture with no
    /// cached row for a side is a gap, never a zero.
    /// </summary>
    private static IReadOnlyList<SvgChart> BuildAttack(SquadPageData page)
    {
        var fixtures = page.Window.Reverse().ToList();
        if (fixtures.Count == 0)
        {
            return Array.Empty<SvgChart>();
        }

        var labels = LabelsFor(fixtures);

        return new[]
        {
            Paired("Tries for / against", labels, fixtures, row => row?.Tries),
            Paired("Metres gained for / against", labels, fixtures, row => row?.MetresGained),
            Paired("Linebreaks for / against", labels, fixtures, row => row?.Linebreaks),
            Paired("Phases for / against", labels, fixtures, row => row?.Phases),
            Paired("7+ phases for / against", labels, fixtures, row => row?.SevenplusPhases),
            Paired("Rucks won for / against", labels, fixtures, row => row?.RucksWon),
            Paired("Mauls won for / against", labels, fixtures, row => row?.MaulsWon),
            Paired("Kicking metres for / against", labels, fixtures, row => row?.KickingMetres)
        };
    }

    /// <summary>One "for / against" chart: the chosen cached field read from both
    /// sides' team rows, ours on the accent tone and the opponent's on the adverse
    /// one.</summary>
    private static SvgChart Paired(
        string title,
        IReadOnlyList<string> labels,
        IReadOnlyList<SquadFixtureRows> fixtures,
        Func<TeamFixtureStatRow?, int?> value) =>
        Chart(title, labels,
            Series("For", ChartTone.Us, fixtures.Select(entry => (double?)value(entry.TeamStats)).ToList()),
            Series("Against", ChartTone.Opponent, fixtures.Select(entry => (double?)value(entry.OpponentStats)).ToList()));

    /// <summary>
    /// The Defence, discipline and kicking tab (S6 / #40): eight charts over the
    /// cached window — tackle completion and kick accuracy as shares of the locked
    /// fields, sloppiness and needless kicks as sums of the locked fields, and the
    /// half split read from the cached first-half team row (the second half is the
    /// full row minus it). Every value is a read or a sum of cached fields; nothing
    /// is invented, and a missing row is a gap.
    /// </summary>
    private static IReadOnlyList<SvgChart> BuildDefence(SquadPageData page)
    {
        var fixtures = page.Window.Reverse().ToList();
        if (fixtures.Count == 0)
        {
            return Array.Empty<SvgChart>();
        }

        var labels = LabelsFor(fixtures);

        return new[]
        {
            // Completion is a quality both sides hold, so each rides its own tone.
            Chart("Tackle completion %", labels,
                Series("Us", ChartTone.Us, fixtures.Select(entry => Completion(entry.TeamStats)).ToList()),
                Series("Opponent", ChartTone.Opponent, fixtures.Select(entry => Completion(entry.OpponentStats)).ToList())),
            // Adverse counts: the opponent's line leads, as the prototype draws them.
            Chart("Missed tackles for / against", labels,
                Series("Against", ChartTone.Opponent, fixtures.Select(entry => (double?)entry.OpponentStats?.MissedTackles).ToList()),
                Series("For", ChartTone.Us, fixtures.Select(entry => (double?)entry.TeamStats?.MissedTackles).ToList())),
            Chart("Sloppiness (knock-ons + forward passes + handling errors)", labels,
                Series("Opponent", ChartTone.Opponent, fixtures.Select(entry => Sloppiness(entry.OpponentStats)).ToList()),
                Series("Us", ChartTone.Us, fixtures.Select(entry => Sloppiness(entry.TeamStats)).ToList())),
            Chart("Kick accuracy %", labels,
                Series("Us", ChartTone.Us, fixtures.Select(entry => KickAccuracy(entry.TeamStats)).ToList())),
            Chart("Needless kicks (bad up-and-unders + kicks out on the full)", labels,
                Series("Opponent", ChartTone.Opponent, fixtures.Select(entry => NeedlessKicks(entry.OpponentStats)).ToList()),
                Series("Us", ChartTone.Us, fixtures.Select(entry => NeedlessKicks(entry.TeamStats)).ToList())),
            Chart("Shots at goal: conversions vs missed", labels,
                Series("Made", ChartTone.Us, fixtures.Select(entry => (double?)entry.TeamStats?.Conversions).ToList()),
                Series("Missed", ChartTone.Opponent, fixtures.Select(entry => (double?)entry.TeamStats?.MissedConversions).ToList())),
            Chart("Injuries / injury breaks", labels,
                Series("Injuries", ChartTone.Opponent, fixtures.Select(entry => (double?)entry.TeamStats?.Injuries).ToList()),
                Series("Breaks", ChartTone.Us, fixtures.Select(entry => (double?)entry.TeamStats?.InjuryBreaks).ToList())),
            Chart("1st half vs 2nd half points", labels,
                Series("2nd half", ChartTone.Us, fixtures.Select(SecondHalfPoints).ToList()),
                Series("1st half", ChartTone.Opponent, fixtures.Select(FirstHalfPoints).ToList()))
        };
    }

    /// <summary>Tackle completion: made tackles as a share of all attempts, or a gap
    /// when the Fixture holds no row for that side (or no attempts were made).</summary>
    private static double? Completion(TeamFixtureStatRow? row) => Share(row?.Tackles, row?.MissedTackles);

    /// <summary>Kick accuracy: the kicks neither bad nor out on the full as a share
    /// of all kicks — the prototype's `(kicks - bad - out) / kicks`, expressed as a
    /// share so the existing arithmetic guards a zero total.</summary>
    private static double? KickAccuracy(TeamFixtureStatRow? row) =>
        row is null
            ? null
            : Share(row.Kicks - row.BadKicks - row.KicksOutOnTheFull, row.BadKicks + row.KicksOutOnTheFull);

    /// <summary>Sloppiness: knock-ons plus forward passes plus handling errors.</summary>
    private static double? Sloppiness(TeamFixtureStatRow? row) =>
        row is null ? null : row.Knockons + row.ForwardPasses + row.HandlingErrors;

    /// <summary>Needless kicks: bad up-and-unders plus kicks out on the full.</summary>
    private static double? NeedlessKicks(TeamFixtureStatRow? row) =>
        row is null ? null : row.BadUpAndUnders + row.KicksOutOnTheFull;

    /// <summary>The first half's points, from our cached first-half team row.</summary>
    private static double? FirstHalfPoints(SquadFixtureRows entry) => entry.TeamFirstHalfStats?.TotalPoints;

    /// <summary>The second half's points: the full-time total minus the first half
    /// (the API serves no second-half block), or a gap when either is uncached.</summary>
    private static double? SecondHalfPoints(SquadFixtureRows entry) =>
        entry.TeamStats is { } full && entry.TeamFirstHalfStats is { } first
            ? full.TotalPoints - first.TotalPoints
            : null;

    private static SvgChart Chart(string title, IReadOnlyList<string> labels, params SvgSeries[] series) =>
        new(title, labels, series);

    private static SvgSeries Series(string label, ChartTone tone, IReadOnlyList<double?> values) =>
        new(label, tone, values);

    /// <summary>The metric as a share of the Fixture total (S6): null when either
    /// side is uncached or the total is zero — a gap, never a fabricated 100%.</summary>
    private static double? Share(int? part, int? other)
    {
        if (part is null || other is null)
        {
            return null;
        }

        var total = part.Value + other.Value;
        return total <= 0
            ? null
            : Math.Round(part.Value / (double)total * 100, 1, MidpointRounding.AwayFromZero);
    }
}
