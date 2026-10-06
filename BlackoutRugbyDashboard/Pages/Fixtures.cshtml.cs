using BlackoutRugbyDashboard.Data;
using BlackoutRugbyDashboard.Models;
using BlackoutRugbyDashboard.Services;
using Microsoft.AspNetCore.Mvc;

namespace BlackoutRugbyDashboard.Pages;

/// <summary>
/// Match Analysis at /Fixtures/{id:int} (D2 route, D6): one cached Fixture as a
/// masthead, the twelve-pair team compare row, the locked seven Stat-Group tabs
/// over our squad's per-Player rows (each carrying its movement against the previous
/// cached game) and the Match Summary's scoring notes. Cache-first — a Fixture the
/// cache already holds costs no API call; the first view fills the D1 entry scope
/// (5 calls) and caches it permanently. A foreign Fixture (neither side is ours)
/// renders the masthead and compare row with our team sheet and tabs suppressed.
/// </summary>
public class FixtureModel(
    ClubLinkService clubLinks,
    MatchCacheService cache,
    SnapshotStore snapshots,
    ILogger<FixtureModel> logger) : ClubLinkedPageModel(clubLinks)
{
    public int FixtureId { get; private set; }

    /// <summary>True when the cache holds no such completed Fixture even after the
    /// first-view fill — an unknown id, or a Fixture that has not been played.</summary>
    public bool NotFound { get; private set; }

    /// <summary>True when one side of the Fixture is our club — our team sheet and
    /// the Stat-Group tabs render. A foreign Fixture shows masthead + compare only.</summary>
    public bool IsOurFixture { get; private set; }

    public string HomeName { get; private set; } = string.Empty;
    public string GuestName { get; private set; } = string.Empty;
    public string Competition { get; private set; } = string.Empty;
    public DateTime Date { get; private set; }
    public string Stadium { get; private set; } = string.Empty;
    public int? HomeScore { get; private set; }
    public int? GuestScore { get; private set; }
    public int AttendanceTotal { get; private set; }
    public string AttendanceBreakdown { get; private set; } = "—";
    public IReadOnlyList<CompareRow> Compare { get; private set; } = Array.Empty<CompareRow>();
    public IReadOnlyList<string> ScoringLines { get; private set; } = Array.Empty<string>();

    /// <summary>The tactics panel's team sheet (D6 §2): XV slots 1–15 then Bench slots
    /// 16–23, with captain / kicker badges, from the cached `lu` archive. Our Fixtures
    /// only.</summary>
    public IReadOnlyList<MatchAnalysis.TeamSheetSlot> TeamSheet { get; private set; } = Array.Empty<MatchAnalysis.TeamSheetSlot>();

    /// <summary>The tactics panel's intensity chip, or null when our side's intensity
    /// is not cached.</summary>
    public string? IntensityChip { get; private set; }

    /// <summary>True when the Recommendations button renders (D8 §6): our Fixture, and
    /// both full-time team rows cached.</summary>
    public bool CanRecommend { get; private set; }

    /// <summary>True when the deterministic rules have been run for this render (the
    /// user asked), so the output card shows — even when the list is empty (its empty
    /// state).</summary>
    public bool ShowRecommendations { get; private set; }

    public IReadOnlyList<RecommendationItem> Recommendations { get; private set; } = Array.Empty<RecommendationItem>();

    /// <summary>The open Stat-Group tab and its locked field set, and the column sort.</summary>
    public StatGroup SelectedGroup { get; private set; } = GameReviewMatrix.ParseGroup(null);
    public IReadOnlyList<GameReviewField> GroupFields { get; private set; } = GameReviewMatrix.FieldsFor(GameReviewMatrix.ParseGroup(null));
    public string? SortKey { get; private set; }
    public bool SortDescending { get; private set; }
    public IReadOnlyList<GameReviewRow> Rows { get; private set; } = Array.Empty<GameReviewRow>();

    public async Task<IActionResult> OnGetAsync(
        int id, string? tab = null, string? sort = null, string? dir = null, string? recs = null)
    {
        FixtureId = id;
        var teamId = ClubLinks.GetLinkState()?.TeamId ?? 0;

        // Cache-first: the first view brings the Fixture's Match Analysis scope into
        // the cache (the full D1 entry scope on a cold link; only the bare-`fs` + `lu`
        // the analysis adds when Home already holds the Fixture); every later view
        // reads the cache alone.
        try
        {
            await cache.FillFixtureAnalysisAsync(id, teamId);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "The Match Analysis fill failed for Fixture {FixtureId}", id);
        }

        var current = await cache.GetSquadWindowRowsAsync([id], teamId);
        if (!current.TryGetValue(id, out var fixtureRows))
        {
            NotFound = true;
            return Page();
        }

        var fixture = fixtureRows.Fixture;
        IsOurFixture = fixture.HomeTeamId == teamId || fixture.GuestTeamId == teamId;

        var teamFacts = await cache.GetTeamFactsAsync([fixture.HomeTeamId, fixture.GuestTeamId]);
        HomeName = NameFor(fixture.HomeTeamId, teamFacts);
        GuestName = NameFor(fixture.GuestTeamId, teamFacts);

        Competition = fixture.Round > 0 ? $"{fixture.Competition} R{fixture.Round}" : fixture.Competition;
        Date = DateTimeOffset.FromUnixTimeSeconds(fixture.MatchStartUnix).LocalDateTime;
        Stadium = string.IsNullOrWhiteSpace(fixture.Stadium) || fixture.Stadium == "0"
            ? string.Empty
            : fixture.Stadium;
        HomeScore = fixtureRows.Summary?.HomePoints;
        GuestScore = fixtureRows.Summary?.GuestPoints;
        AttendanceTotal = MatchAnalysis.AttendanceTotal(fixtureRows.Summary);
        AttendanceBreakdown = MatchAnalysis.AttendanceBreakdown(fixtureRows.Summary);
        Compare = MatchAnalysis.BuildCompare(fixtureRows.TeamStats, fixtureRows.OpponentStats);

        SelectedGroup = GameReviewMatrix.ParseGroup(tab);
        GroupFields = GameReviewMatrix.FieldsFor(SelectedGroup);
        SortKey = sort;
        SortDescending = string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase);

        var capture = await snapshots.GetLatestAsync(teamId);
        var names = capture?.Players.ToDictionary(player => player.Id, player => player.Name)
            ?? new Dictionary<int, string>();

        // Recommendations (D8 §6): the button and card only exist for our Fixtures with
        // both full-time team rows cached. The engine is pure and deterministic, so the
        // card is computed on demand from the same rows the compare row reads.
        CanRecommend = IsOurFixture
            && fixtureRows.TeamStats is not null
            && fixtureRows.OpponentStats is not null;
        if (CanRecommend && recs is not null)
        {
            Recommendations = RecommendationEngine.Build(fixtureRows.TeamStats!, fixtureRows.OpponentStats!);
            ShowRecommendations = true;
        }

        if (!IsOurFixture)
        {
            return Page();
        }

        // Tactics panel (D6 §2): the team sheet from the cached `lu` archive plus the
        // intensity chip from the Match Summary. Read-only — no controls.
        TeamSheet = MatchAnalysis.BuildTeamSheet(cache.GetCachedLineup(id, teamId), names);
        var intensity = fixture.HomeTeamId == teamId
            ? fixtureRows.Summary?.HomeIntensity
            : fixture.GuestTeamId == teamId
                ? fixtureRows.Summary?.GuestIntensity
                : null;
        IntensityChip = MatchAnalysis.IntensityText(intensity);

        var previousRows = await ReadPreviousAsync(teamId, fixture);
        var built = GameReviewMatrix.BuildRows(fixtureRows, capture, SelectedGroup, previousRows);
        Rows = GameReviewMatrix.Sort(built, SelectedGroup, SortKey, SortDescending);

        ScoringLines = MatchAnalysis.ScoringLines(await cache.GetSummaryScorersAsync(id), names);

        return Page();
    }

    /// <summary>Our club's next-older cached game, so each cell can carry its
    /// movement — null when this Fixture is the oldest in the window.</summary>
    private async Task<SquadFixtureRows?> ReadPreviousAsync(int teamId, FixtureRow fixture)
    {
        var window = await cache.GetCachedWindowAsync(teamId, season: 0, last: 20);
        var previous = window.FirstOrDefault(row =>
            row.FixtureId != fixture.FixtureId && row.MatchStartUnix < fixture.MatchStartUnix);
        if (previous is null)
        {
            return null;
        }

        var rows = await cache.GetSquadWindowRowsAsync([previous.FixtureId], teamId);
        return rows.GetValueOrDefault(previous.FixtureId);
    }

    private static string NameFor(int teamId, IReadOnlyDictionary<int, TeamFactRow> facts) =>
        facts.TryGetValue(teamId, out var fact) && !string.IsNullOrWhiteSpace(fact.Name)
            ? fact.Name
            : $"Team {teamId}";
}