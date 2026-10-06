using BlackoutRugbyDashboard.Data;

namespace BlackoutRugbyDashboard.Services;

/// <summary>
/// How loud one Recommendation reads (D8 §4): <see cref="Watch"/> is the amber
/// early warning, <see cref="Act"/> the red one — the Act threshold is the stricter
/// (larger) of the pair, so an item is Act when it clears its Act bound, else Watch.
/// </summary>
public enum RecommendationSeverity
{
    Watch,
    Act
}

/// <summary>Which way a Tactic Slider is moved (D8 §2): Raise it, Cut it, or — for
/// Discipline — Ease off it. Advice names the control and the direction, never a
/// current value, because strategy fields are unreadable through the API (R1).</summary>
public enum SlideDirection
{
    Raise,
    Cut,
    EaseOff
}

/// <summary>One Tactic Slider move: the in-game control by name and the way it goes.</summary>
public sealed record SliderMove(string Slider, SlideDirection Direction)
{
    /// <summary>The move as it reads in an advice line ("Raise Driving", "Cut Expansive").</summary>
    public string Text => Direction switch
    {
        SlideDirection.Raise => $"Raise {Slider}",
        SlideDirection.Cut => $"Cut {Slider}",
        _ => $"Ease off {Slider}"
    };
}

/// <summary>
/// One Recommendation (D8 §5): a severity, the diagnosis headline, the evidence line
/// carrying the actual compare numbers, and the advice — zero or more Tactic Slider
/// moves. The engine is deterministic: the same compare row always yields the same
/// ordered items.
/// </summary>
public sealed record RecommendationItem(
    RecommendationSeverity Severity,
    string Diagnosis,
    string Evidence,
    IReadOnlyList<SliderMove> Advice)
{
    /// <summary>The severity as it reads on the chip and as its theme class.</summary>
    public string SeverityText => Severity == RecommendationSeverity.Act ? "Act" : "Watch";

    /// <summary>The severity's theme class.</summary>
    public string SeverityClass => Severity == RecommendationSeverity.Act ? "rec-act" : "rec-watch";

    /// <summary>The advice line ("Raise Driving · Cut Expansive"), or the empty string
    /// when the rule is diagnosis-only (no v1 rule is).</summary>
    public string AdviceText => string.Join(" · ", Advice.Select(move => move.Text));
}

/// <summary>
/// The deterministic Recommendation rules v1 (D8, build slice: Match Analysis
/// completion). Input is the full-time team compare row only — both sides' cached
/// bare-`fs` TeamFixtureStat rows, the twelve fixed D4 pairs. Thresholds live in one
/// constants table here, tweakable without touching rule structure. Pure: no API
/// client and no store, so the output is a function of the cached row alone.
/// </summary>
public static class RecommendationEngine
{
    /// <summary>The v1 thresholds (D8 §3), one table so they can be tuned in one place.</summary>
    private static class Thresholds
    {
        public const int PossessionWatch = 45;
        public const int PossessionAct = 35;
        public const int TerritoryWatch = 45;
        public const int TerritoryAct = 35;
        public const int LineoutLostMinusWonWatch = 3;
        public const int LineoutLostMinusWonAct = 6;
        public const int ScrumLostWatch = 2;
        public const int ScrumLostAct = 4;
        public const int RuckWonTheirsMinusYoursWatch = 5;
        public const int RuckWonTheirsMinusYoursAct = 10;
        public const int TurnoverConcededMinusWonWatch = 3;
        public const int TurnoverConcededMinusWonAct = 6;
        public const int PenaltyConcededMinusWonWatch = 4;
        public const int PenaltyConcededMinusWonAct = 8;
        public const int TriesConcededWatch = 3;
        public const int TriesConcededAct = 5;
    }

    // The Tactic Slider controls (D8 §2), named exactly as the game names them.
    private const string PickAndGo = "Pick and Go";
    private const string Driving = "Driving";
    private const string Expansive = "Expansive";
    private const string Creative = "Creative";
    private const string Defence = "Defence";
    private const string Kicking = "Kicking";
    private const string KickForTouch = "Kick for Touch";
    private const string Discipline = "Discipline";

    /// <summary>
    /// The rules run against our side's and the opponent's full-time bare-`fs` rows.
    /// Items come back Act first, then Watch; within a level they read in compare-row
    /// order (the D8 §4 rule). No item means every comparison sat inside its Watch
    /// threshold.
    /// </summary>
    public static IReadOnlyList<RecommendationItem> Build(TeamFixtureStatRow us, TeamFixtureStatRow them)
    {
        // Each rule records the compare-row position of the pair it reads (the D4 order),
        // so the output orders Act first, then by compare-row within a level (D8 §4).
        var items = new List<(int Order, RecommendationItem Item)>();

        // Possession share (compare pair 1).
        if (Share(us.Possession, them.Possession) is int possession && possession < Thresholds.PossessionWatch)
        {
            items.Add((1, Item(
                possession < Thresholds.PossessionAct,
                "Lost the possession battle",
                $"Possession {possession}% to {100 - possession}%",
                Raise(Driving), Cut(Expansive))));
        }

        // 2 - Territory share.
        if (Share(us.Territory, them.Territory) is int territory && territory < Thresholds.TerritoryWatch)
        {
            items.Add((2, Item(
                territory < Thresholds.TerritoryAct,
                "Starved of territory",
                $"Territory {territory}% to {100 - territory}%",
                Raise(KickForTouch), Raise(Kicking))));
        }

        // 3 - Lineouts: lost minus won.
        var lineoutLoss = us.LineoutsLost - us.LineoutsWon;
        if (lineoutLoss >= Thresholds.LineoutLostMinusWonWatch)
        {
            items.Add((8, Item(
                lineoutLoss >= Thresholds.LineoutLostMinusWonAct,
                "Lineout battle lost",
                $"Lineouts: won {us.LineoutsWon}, lost {us.LineoutsLost}",
                Raise(Driving), Cut(Expansive))));
        }

        // 4 - Scrums lost on our own ball.
        if (us.ScrumsLost >= Thresholds.ScrumLostWatch)
        {
            items.Add((7, Item(
                us.ScrumsLost >= Thresholds.ScrumLostAct,
                "Scrum under pressure",
                $"Scrums lost on our own ball: {us.ScrumsLost}",
                Cut(Expansive))));
        }

        // 5 - Rucks won: theirs minus yours.
        var ruckDeficit = them.RucksWon - us.RucksWon;
        if (ruckDeficit >= Thresholds.RuckWonTheirsMinusYoursWatch)
        {
            items.Add((5, Item(
                ruckDeficit >= Thresholds.RuckWonTheirsMinusYoursAct,
                "Ruck battle lost",
                $"Rucks won: {us.RucksWon} to {them.RucksWon}",
                Raise(PickAndGo), Raise(Driving), Cut(Expansive))));
        }

        // 6 - Turnovers: conceded minus won.
        var turnoverLoss = us.TurnoversConceded - us.Turnovers;
        if (turnoverLoss >= Thresholds.TurnoverConcededMinusWonWatch)
        {
            items.Add((12, Item(
                turnoverLoss >= Thresholds.TurnoverConcededMinusWonAct,
                "Throwing it away",
                $"Turnovers: conceded {us.TurnoversConceded}, won {us.Turnovers}",
                Cut(Creative), Cut(Expansive), Raise(Driving))));
        }

        // 7 - Penalties: conceded minus won.
        var penaltyLoss = us.PenaltiesConceded - us.PenaltiesWon;
        if (penaltyLoss >= Thresholds.PenaltyConcededMinusWonWatch)
        {
            items.Add((11, Item(
                penaltyLoss >= Thresholds.PenaltyConcededMinusWonAct,
                "Discipline is costing points",
                $"Penalties: conceded {us.PenaltiesConceded}, won {us.PenaltiesWon}",
                EaseOff(Discipline))));
        }

        // 8 - Tries conceded.
        if (them.Tries >= Thresholds.TriesConcededWatch)
        {
            items.Add((3, Item(
                them.Tries >= Thresholds.TriesConcededAct,
                "Defence broken repeatedly",
                $"Tries conceded: {them.Tries}",
                Raise(Defence))));
        }

        // Act items first, then Watch; within a level, compare-row order (D8 §4).
        return items
            .OrderBy(entry => entry.Item.Severity == RecommendationSeverity.Act ? 0 : 1)
            .ThenBy(entry => entry.Order)
            .Select(entry => entry.Item)
            .ToList();
    }
    /// <summary>Our percentage share of a two-sided count, or null when neither side
    /// recorded a value (so the rule is simply silent, never fed a made-up zero).</summary>
    private static int? Share(int? ours, int? theirs)
    {
        if (ours is not int ourValue || theirs is not int theirValue || ourValue + theirValue <= 0)
        {
            return null;
        }

        return (int)Math.Round(ourValue * 100.0 / (ourValue + theirValue));
    }

    private static RecommendationItem Item(bool act, string diagnosis, string evidence, params SliderMove[] advice) =>
        new(act ? RecommendationSeverity.Act : RecommendationSeverity.Watch, diagnosis, evidence, advice);

    private static SliderMove Raise(string slider) => new(slider, SlideDirection.Raise);

    private static SliderMove Cut(string slider) => new(slider, SlideDirection.Cut);

    private static SliderMove EaseOff(string slider) => new(slider, SlideDirection.EaseOff);


}
