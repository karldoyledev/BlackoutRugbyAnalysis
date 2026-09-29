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

    private static SquadFixtureRows Fixture(params PlayerFixtureRow[] players) => new(
        new FixtureRow
        {
            FixtureId = 21416928,
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
}
