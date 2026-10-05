using BlackoutRugbyDashboard.Data;
using BlackoutRugbyDashboard.Models;
using BlackoutRugbyDashboard.Services;
using Xunit;

namespace BlackoutRugbyDashboard.Tests;

/// <summary>
/// The Game review matrix (S3 + D4, build slice #35): the locked seven Stat-Group
/// field map, the always-visible columns, XV/Bench marking, and sorting. The
/// builder is pure — it reads cached PlayerFixture rows and the latest capture and
/// nothing else, so "cache-only" is structural rather than asserted.
/// </summary>
public class GameReviewMatrixTests
{
    private const int TeamId = 45047;

    private static PlayerFixtureRow Player(int playerId, int slot, params (string Field, int Value)[] values)
    {
        var row = new PlayerFixtureRow
        {
            PlayerId = playerId,
            TeamId = TeamId,
            Side = "home",
            Jersey = slot,
            Slot = slot,
            MinutesPlayed = 80,
            TotalPoints = 5,
            EnergyAfter = 62
        };

        foreach (var (field, value) in values)
        {
            typeof(PlayerFixtureRow).GetProperty(field)!.SetValue(row, value);
        }

        return row;
    }

    private static SquadFixtureRows Fixture(params PlayerFixtureRow[] players) => Fixture(21416928, players);

    private static SquadFixtureRows Fixture(int fixtureId, params PlayerFixtureRow[] players) => new(
        new FixtureRow
        {
            FixtureId = fixtureId,
            Season = 62,
            LeagueId = 349097,
            Round = 3,
            Competition = "League",
            HomeTeamId = TeamId,
            GuestTeamId = 45037,
            WeatherId = 1,
            BotMatch = 0,
            DataRemoved = 0,
            Stadium = "0",
            CountryIso = "IE",
            MatchStartUnix = 1_789_220_100,
            MatchFinishUnix = 1_789_226_106,
            FetchedAt = new DateTime(2026, 9, 20, 8, 0, 0, DateTimeKind.Utc)
        },
        new MatchSummaryRow { FixtureId = 21416928, HomePoints = 31, GuestPoints = 17 },
        players,
        null);

    [Fact]
    public void Groups_CarryTheLockedSevenStatGroupsInOrder()
    {
        Assert.Equal(
            new[] { StatGroup.Attack, StatGroup.Defence, StatGroup.Kicking, StatGroup.Handling, StatGroup.Discipline, StatGroup.Lineout, StatGroup.Other },
            GameReviewMatrix.Groups);
    }

    /// <summary>The D4 field→tab map (spec §Match Analysis page): 37 tab fields —
    /// the spec's "40 fields in the locked 7 tabs" counts the three always-visible
    /// output columns with them.</summary>
    [Fact]
    public void FieldsFor_AreExactlyTheLockedFieldSets()
    {
        Assert.Equal(
            new[] { "tries", "try_assists", "metres_gained", "linebreaks", "beaten_defenders" },
            GameReviewMatrix.FieldsFor(StatGroup.Attack).Select(field => field.Key));

        Assert.Equal(
            new[] { "tackles", "missed_tackles", "turnovers", "intercepts" },
            GameReviewMatrix.FieldsFor(StatGroup.Defence).Select(field => field.Key));

        Assert.Equal(
            new[]
            {
                "kicks", "good_kicks", "bad_kicks", "kicking_metres", "kicks_out_on_the_full", "up_and_unders",
                "good_up_and_unders", "bad_up_and_unders", "conversions", "missed_conversions", "penalties",
                "missed_penalties", "dropgoals", "missed_dropgoals"
            },
            GameReviewMatrix.FieldsFor(StatGroup.Kicking).Select(field => field.Key));

        Assert.Equal(
            new[] { "knockons", "handling_errors", "forward_passes" },
            GameReviewMatrix.FieldsFor(StatGroup.Handling).Select(field => field.Key));

        Assert.Equal(
            new[] { "penalties_conceded", "yellow_cards", "red_cards", "fights" },
            GameReviewMatrix.FieldsFor(StatGroup.Discipline).Select(field => field.Key));

        Assert.Equal(
            new[] { "lineouts_secured", "lineouts_conceded", "lineouts_stolen", "successful_lineout_throws", "unsuccessful_lineout_throws" },
            GameReviewMatrix.FieldsFor(StatGroup.Lineout).Select(field => field.Key));

        Assert.Equal(
            new[] { "injuries", "ball_time" },
            GameReviewMatrix.FieldsFor(StatGroup.Other).Select(field => field.Key));

        Assert.Equal(37, GameReviewMatrix.Groups.Sum(group => GameReviewMatrix.FieldsFor(group).Count));
    }

    /// <summary>The excluded verbatim fields (D4): the 8 unreliable fs caps fields,
    /// `played`, and `energy_before` (cached, never shown).</summary>
    [Fact]
    public void FieldsFor_ExcludeTheUnreliableAndNeverShownFields()
    {
        var keys = GameReviewMatrix.Groups
            .SelectMany(GameReviewMatrix.FieldsFor)
            .Select(field => field.Key)
            .ToList();

        Assert.DoesNotContain("played", keys);
        Assert.DoesNotContain("energy_before", keys);
        Assert.DoesNotContain(keys, key => key.Contains("caps"));
    }

    private static TeamSnapshot Capture(params (int Id, string Name, int Csr)[] players) => new()
    {
        TeamId = TeamId,
        TeamName = "Doylester",
        CapturedAtUtc = new DateTime(2026, 9, 21, 19, 25, 0, DateTimeKind.Utc),
        Players = players
            .Select(player => new PlayerSnapshotRecord { Id = player.Id, Name = player.Name, Csr = player.Csr })
            .ToList()
    };

    [Fact]
    public void BuildRows_ReadsTheAlwaysVisibleColumnsVerbatim()
    {
        var fixture = Fixture(Player(16728687, 1, ("MetresGained", 43), ("Tries", 1), ("TryAssists", 1)));

        var row = Assert.Single(GameReviewMatrix.BuildRows(fixture, Capture()));

        Assert.Equal(80, row.Minutes);
        Assert.Equal(5, row.Points);
        Assert.Equal(62, row.EnergyAfter);
    }

    [Fact]
    public void BuildRows_MarksTheXVAndTheBench()
    {
        var fixture = Fixture(Player(1, 1), Player(2, 15), Player(3, 16), Player(4, 23));

        var rows = GameReviewMatrix.BuildRows(fixture, Capture());

        Assert.False(rows.Single(row => row.Slot == 1).IsBench);
        Assert.False(rows.Single(row => row.Slot == 15).IsBench);
        Assert.True(rows.Single(row => row.Slot == 16).IsBench);
        Assert.True(rows.Single(row => row.Slot == 23).IsBench);
    }

    [Fact]
    public void BuildRows_TakesTheCsrAndNameFromTheLatestCaptureAndBlanksAbsentPlayers()
    {
        var fixture = Fixture(Player(101, 1), Player(102, 16), Player(103, 17));
        var capture = Capture((101, "Seán O'Brien", 145), (102, "Tane Williams", 98));

        var rows = GameReviewMatrix.BuildRows(fixture, capture);

        Assert.Equal((int?)145, rows.Single(row => row.Slot == 1).Csr);
        Assert.Equal("Seán O'Brien", rows.Single(row => row.Slot == 1).Name);
        Assert.Equal((int?)98, rows.Single(row => row.Slot == 16).Csr);
        Assert.Null(rows.Single(row => row.Slot == 17).Csr); // absent from the capture — blank, never invented
        Assert.Equal("Player 103", rows.Single(row => row.Slot == 17).Name);
    }

    [Fact]
    public void BuildRows_WithoutACapture_BlanksEveryCsr()
    {
        var fixture = Fixture(Player(101, 1));

        var row = Assert.Single(GameReviewMatrix.BuildRows(fixture, capture: null));

        Assert.Null(row.Csr);
    }

    [Fact]
    public void BuildRows_AlignTheTabValuesWithTheTabsFields()
    {
        var fixture = Fixture(Player(101, 7, ("Tries", 2), ("TryAssists", 1), ("MetresGained", 43), ("Linebreaks", 3), ("BeatenDefenders", 4)));

        var row = Assert.Single(GameReviewMatrix.BuildRows(fixture, Capture()));
        var fields = GameReviewMatrix.FieldsFor(StatGroup.Attack).ToList();

        Assert.Equal(new[] { 2, 1, 43, 3, 4 }, row.Values);
        Assert.Equal(43, row.Values[fields.FindIndex(field => field.Key == "metres_gained")]);
    }

    [Fact]
    public void Sort_DefaultsToSlotOrderAndSortsByAnyTabFieldBothWays()
    {
        var fixture = Fixture(
            Player(202, 16, ("Tackles", 12)),
            Player(101, 1, ("Tackles", 3)),
            Player(103, 17, ("Tackles", 7)));
        var rows = GameReviewMatrix.BuildRows(fixture, Capture(), StatGroup.Defence);

        Assert.Equal(
            new[] { 1, 16, 17 },
            GameReviewMatrix.Sort(rows, StatGroup.Defence, sortKey: null, descending: false).Select(row => row.Slot));
        Assert.Equal(
            new[] { 1, 17, 16 },
            GameReviewMatrix.Sort(rows, StatGroup.Defence, "tackles", descending: false).Select(row => row.Slot));
        Assert.Equal(
            new[] { 16, 17, 1 },
            GameReviewMatrix.Sort(rows, StatGroup.Defence, "tackles", descending: true).Select(row => row.Slot));
    }

    [Fact]
    public void Sort_AlsoWorksOnTheAlwaysVisibleColumns()
    {
        var fixture = Fixture(Player(101, 1, ("MinutesPlayed", 45)), Player(102, 2, ("MinutesPlayed", 80)));
        var rows = GameReviewMatrix.BuildRows(fixture, Capture());

        Assert.Equal(
            new[] { 2, 1 },
            GameReviewMatrix.Sort(rows, StatGroup.Attack, "minutes_played", descending: true).Select(row => row.Slot));
    }

    [Fact]
    public void Sort_WithAFieldFromAnotherTab_FallsBackToSlotOrder()
    {
        var fixture = Fixture(Player(102, 2), Player(101, 1));
        var rows = GameReviewMatrix.BuildRows(fixture, Capture());

        Assert.Equal(
            new[] { 1, 2 },
            GameReviewMatrix.Sort(rows, StatGroup.Attack, "tackles", descending: true).Select(row => row.Slot));
    }

    [Fact]
    public void ParseGroup_ReadsTheTabQueryKeyAndFallsBackToAttack()
    {
        Assert.Equal(StatGroup.Defence, GameReviewMatrix.ParseGroup("defence"));
        Assert.Equal(StatGroup.Discipline, GameReviewMatrix.ParseGroup("Discipline"));
        Assert.Equal(StatGroup.Attack, GameReviewMatrix.ParseGroup("nonsense"));
        Assert.Equal(StatGroup.Attack, GameReviewMatrix.ParseGroup(null));
        Assert.Equal("defence", GameReviewMatrix.KeyFor(StatGroup.Defence));
    }

    // ---------------------------------------------------------------------
    // Delta vs the previous cached game (#36, S3) - the three display modes
    // ---------------------------------------------------------------------

    [Fact]
    public void ParseMode_ReadsTheModeQueryKeyAndFallsBackToGamePlusDelta()
    {
        Assert.Equal(DeltaMode.Values, GameReviewMatrix.ParseMode("values"));
        Assert.Equal(DeltaMode.Delta, GameReviewMatrix.ParseMode("Delta"));
        Assert.Equal(DeltaMode.Both, GameReviewMatrix.ParseMode("both"));
        Assert.Equal(DeltaMode.Both, GameReviewMatrix.ParseMode("nonsense"));
        Assert.Equal(DeltaMode.Both, GameReviewMatrix.ParseMode(null));

        Assert.Equal("values", GameReviewMatrix.KeyFor(DeltaMode.Values));
        Assert.Equal("both", GameReviewMatrix.KeyFor(DeltaMode.Both));
        Assert.Equal("delta", GameReviewMatrix.KeyFor(DeltaMode.Delta));
    }

    [Fact]
    public void BuildRows_DeltaIsThisGameMinusThePreviousCachedGame()
    {
        var previous = Fixture(1002, Player(101, 1, ("MetresGained", 40), ("Tries", 1)));
        var fixture = Fixture(1003, Player(101, 1, ("MetresGained", 43), ("Tries", 0)));

        var row = Assert.Single(GameReviewMatrix.BuildRows(fixture, Capture(), StatGroup.Attack, previous));
        var fields = GameReviewMatrix.FieldsFor(StatGroup.Attack).ToList();

        Assert.True(row.HasPrevious);
        Assert.Equal(3, row.ValueDeltas[fields.FindIndex(field => field.Key == "metres_gained")]);
        Assert.Equal(-1, row.ValueDeltas[fields.FindIndex(field => field.Key == "tries")]);
    }

    [Fact]
    public void BuildRows_AlsoDeltaTheAlwaysVisibleOutputs()
    {
        var previous = Fixture(1002, Player(101, 1, ("MinutesPlayed", 45), ("TotalPoints", 5), ("EnergyAfter", 70)));
        var fixture = Fixture(1003, Player(101, 1, ("MinutesPlayed", 80), ("TotalPoints", 2), ("EnergyAfter", 62)));

        var row = Assert.Single(GameReviewMatrix.BuildRows(fixture, Capture(), StatGroup.Attack, previous));

        Assert.Equal(35, row.MinutesDelta);
        Assert.Equal(-3, row.PointsDelta);
        Assert.Equal(-8, row.EnergyAfterDelta);
    }

    /// <summary>The two games swap the Players between slots: matching by id (not by
    /// slot) is what makes the movement mean anything.</summary>
    [Fact]
    public void BuildRows_MatchesPlayersByIdNotBySlot()
    {
        var previous = Fixture(1002, Player(202, 1, ("MetresGained", 10)), Player(101, 2, ("MetresGained", 90)));
        var fixture = Fixture(1003, Player(101, 1, ("MetresGained", 43)), Player(202, 2, ("MetresGained", 4)));

        var rows = GameReviewMatrix.BuildRows(fixture, Capture(), StatGroup.Attack, previous);
        var index = GameReviewMatrix.FieldsFor(StatGroup.Attack).ToList().FindIndex(field => field.Key == "metres_gained");

        Assert.Equal(-47, rows.Single(row => row.PlayerId == 101).ValueDeltas[index]); // 43 against 90, not against 10
        Assert.Equal(-6, rows.Single(row => row.PlayerId == 202).ValueDeltas[index]);
    }

    [Fact]
    public void BuildRows_WithoutAPreviousGame_DashesEveryDelta()
    {
        var row = Assert.Single(GameReviewMatrix.BuildRows(Fixture(Player(101, 1, ("Tries", 2))), Capture()));

        Assert.False(row.HasPrevious);
        Assert.Null(row.MinutesDelta);
        Assert.Null(row.PointsDelta);
        Assert.Null(row.EnergyAfterDelta);
        Assert.All(row.ValueDeltas, delta => Assert.Null(delta));
    }

    [Fact]
    public void BuildRows_APlayerAbsentFromThePreviousGame_DashesTheirDeltasOnly()
    {
        var previous = Fixture(1002, Player(202, 2, ("Tackles", 9)));
        var fixture = Fixture(1003, Player(101, 1, ("Tackles", 4)), Player(202, 2, ("Tackles", 11)));

        var rows = GameReviewMatrix.BuildRows(fixture, Capture(), StatGroup.Defence, previous);
        var index = GameReviewMatrix.FieldsFor(StatGroup.Defence).ToList().FindIndex(field => field.Key == "tackles");

        var newcomer = rows.Single(row => row.PlayerId == 101);
        Assert.False(newcomer.HasPrevious);
        Assert.Null(newcomer.ValueDeltas[index]);

        var regular = rows.Single(row => row.PlayerId == 202);
        Assert.True(regular.HasPrevious);
        Assert.Equal(2, regular.ValueDeltas[index]);
    }

    [Fact]
    public void Cells_AreTheOutputsWithCsrThenTheTabsFieldsInOrder()
    {
        var fixture = Fixture(Player(101, 1, ("Tries", 2), ("MetresGained", 43)));
        var row = Assert.Single(GameReviewMatrix.BuildRows(fixture, Capture((101, "Seán O'Brien", 145))));

        Assert.Equal(new[] { "80", "5", "62", "145", "2", "0", "43", "0", "0" }, row.Cells.Select(cell => cell.Text));
    }

    /// <summary>The CSR column is present state, not per-Fixture output: the API has
    /// no per-game CSR, so it never carries a delta (or a dash) at any mode.</summary>
    [Fact]
    public void Cells_OnlyTheCsrColumnTracksNoHistory()
    {
        var fixture = Fixture(Player(101, 1, ("Tries", 2)));
        var row = Assert.Single(GameReviewMatrix.BuildRows(fixture, Capture((101, "Seán O'Brien", 145))));

        Assert.Equal(new[] { "145" }, row.Cells.Where(cell => !cell.TracksHistory).Select(cell => cell.Text));
        Assert.Equal(8, row.Cells.Count(cell => cell.TracksHistory));
    }

    [Theory]
    [InlineData(3, "+3", "delta-up")]
    [InlineData(-2, "-2", "delta-down")]
    [InlineData(0, "0", "delta-flat")]
    [InlineData(null, "—", "delta-blank")]
    public void DeltaTextAndClass_StyleTheMovement(int? delta, string text, string cssClass)
    {
        Assert.Equal(text, GameReviewMatrix.DeltaText(delta));
        Assert.Equal(cssClass, GameReviewMatrix.DeltaClass(delta));
    }

    // ---------------------------------------------------------------------
    // The Coach's eye (#36, S3): one standout and one worst offender per tab
    // ---------------------------------------------------------------------

    [Fact]
    public void CoachEye_NamesTheStandoutAndTheWorstOffenderWithTheirMetric()
    {
        var fixture = Fixture(
            Player(101, 1, ("Tackles", 3), ("MissedTackles", 5)),
            Player(102, 2, ("Tackles", 12), ("MissedTackles", 2)),
            Player(103, 16, ("Tackles", 7), ("MissedTackles", 0)));
        var rows = GameReviewMatrix.BuildRows(fixture, Capture((102, "Tane Williams", 98)), StatGroup.Defence);

        var eye = GameReviewMatrix.BuildCoachEye(rows, StatGroup.Defence);

        Assert.Equal(new[] { "tackles", "missed_tackles" }, eye.Select(item => item.FieldKey));
        Assert.Equal(new[] { false, true }, eye.Select(item => item.IsAdverse));
        Assert.Equal("Tackles", eye[0].Label);
        Assert.Equal("Missed tackles", eye[1].Label);
        Assert.Equal(102, eye[0].PlayerId);
        Assert.Equal("Tane Williams", eye[0].PlayerName);
        Assert.Equal(12, eye[0].Value);
        Assert.Equal(101, eye[1].PlayerId); // five missed tackles is the worst offending
        Assert.Equal("Player 101", eye[1].PlayerName);
        Assert.Equal(5, eye[1].Value);
    }

    /// <summary>A zero is absence, not performance: nobody with nothing to show is
    /// named, and a tab whose whole game was zeros opens with no line at all.</summary>
    [Fact]
    public void CoachEye_AZeroIsAbsenceNotPerformance()
    {
        var fixture = Fixture(
            Player(101, 1, ("Tackles", 0), ("MissedTackles", 0)),
            Player(102, 2, ("Tackles", 0), ("MissedTackles", 0)));
        var rows = GameReviewMatrix.BuildRows(fixture, Capture(), StatGroup.Defence);

        Assert.Empty(GameReviewMatrix.BuildCoachEye(rows, StatGroup.Defence));
    }

    [Fact]
    public void CoachEye_TiesGoToTheLowerSlot()
    {
        var fixture = Fixture(Player(202, 16, ("Tackles", 8)), Player(101, 1, ("Tackles", 8)));
        var rows = GameReviewMatrix.BuildRows(fixture, Capture(), StatGroup.Defence);

        var standout = GameReviewMatrix.BuildCoachEye(rows, StatGroup.Defence)[0];

        Assert.Equal(101, standout.PlayerId);
        Assert.Equal(8, standout.Value);
    }

    /// <summary>The locked per-tab metric map: the Squad page design map's choices
    /// (the S3 decision, #22), extended to the tab the prototype left out (Other).
    /// Null is honest — D4's field map holds no adverse metric in Attack and no
    /// headline metric in Handling or Discipline, so those tabs open with the half
    /// they have.</summary>
    [Theory]
    [InlineData(StatGroup.Attack, "tries", null)]
    [InlineData(StatGroup.Defence, "tackles", "missed_tackles")]
    [InlineData(StatGroup.Kicking, "good_kicks", "bad_kicks")]
    [InlineData(StatGroup.Handling, null, "knockons")]
    [InlineData(StatGroup.Discipline, null, "penalties_conceded")]
    [InlineData(StatGroup.Lineout, "lineouts_secured", "lineouts_conceded")]
    [InlineData(StatGroup.Other, "ball_time", "injuries")]
    public void CoachEyeMetrics_AreTheLockedPerTabPair(StatGroup group, string? standout, string? adverse)
    {
        Assert.Equal(standout, GameReviewMatrix.CoachEyeMetricFor(group, adverse: false));
        Assert.Equal(adverse, GameReviewMatrix.CoachEyeMetricFor(group, adverse: true));
    }
}
