using BlackoutRugbyDashboard.Data;
using BlackoutRugbyDashboard.Models;
using BlackoutRugbyDashboard.Services;
using Microsoft.AspNetCore.Mvc;

namespace BlackoutRugbyDashboard.Pages.Players;

/// <summary>
/// Player History at /Players/{id:int} (D2 route, D7). The header (name + CSR chip
/// from the latest Squad Snapshot) → the season card (the cached season-scoped `ps`
/// row's league totals by Stat Group, caps and the one `ps` average, with a selector
/// over the seasons the cache holds) → the seven Stat-Group trend tabs, each a
/// per-fixture table (newest first, context columns then the tab's fields) with a
/// per-field header sparkline. Strictly cache-first for the trends (zero `fs` calls);
/// `ps` is fetched once per Player+season on first view and cached. A foreign or
/// unknown id makes zero API calls and opens with an empty-trends state.
/// </summary>
public class PlayerDetailModel(
    ClubLinkService clubLinks,
    MatchCacheService cache,
    SnapshotStore snapshots,
    ILogger<PlayerDetailModel> logger) : ClubLinkedPageModel(clubLinks)
{
    public int PlayerId { get; private set; }

    /// <summary>The Player's name: the latest capture's, else "Player {id}" — the D7
    /// generic header for a foreign or transferred-out id.</summary>
    public string PlayerName { get; private set; } = string.Empty;

    /// <summary>The captured CSR, or null when the Player is absent from the latest
    /// Squad Snapshot (a transferred-out Player's chip blanks).</summary>
    public int? PlayerCsr { get; private set; }

    /// <summary>True when the Player is in the latest Squad Snapshot — the `ps`
    /// refresh and the CSR chip are ours-only; a foreign id gets neither.</summary>
    public bool IsKnown { get; private set; }

    /// <summary>The season the card shows: a valid `?season=` choice, else the newest
    /// season the cache holds a completed Fixture for (0 when nothing is cached).</summary>
    public int Season { get; private set; }

    /// <summary>The seasons the selector offers — the cache's completed-Fixture seasons,
    /// newest first. A single season leaves the selector off (nothing to page through).</summary>
    public IReadOnlyList<int> SeasonOptions { get; private set; } = Array.Empty<int>();

    /// <summary>The Player's league totals for <see cref="Season"/>, from the cached
    /// season-scoped `ps` row, or null when none is cached.</summary>
    public SeasonTotals? SeasonTotals { get; private set; }

    /// <summary>True when the season card renders: a season is known and the Player is
    /// ours — in the latest snapshot, or cached while ours (a transferred-out Player).
    /// A foreign or unknown id gets no card and no `ps` read (D7 §5).</summary>
    public bool ShowSeasonCard { get; private set; }

    /// <summary>The open Stat-Group tab (default Attack), its field set, and the sort.</summary>
    public StatGroup SelectedGroup { get; private set; } = StatGroup.Attack;
    public IReadOnlyList<GameReviewField> GroupFields { get; private set; } = GameReviewMatrix.FieldsFor(StatGroup.Attack);
    public string? SortKey { get; private set; }
    public bool SortDescending { get; private set; }
    public IReadOnlyList<TrendRow> Rows { get; private set; } = Array.Empty<TrendRow>();

    /// <summary>One inline-SVG sparkline per group field, in field order (D7).</summary>
    public IReadOnlyList<string> Sparklines { get; private set; } = Array.Empty<string>();

    /// <summary>True when the cache holds no Fixture for this Player — the trends
    /// empty state (a new signing, a foreign id, or a Player with nothing cached).</summary>
    public bool EmptyTrends => Rows.Count == 0;

    public async Task<IActionResult> OnGetAsync(
        int id, string? tab = null, string? sort = null, string? dir = null, int? season = null)
    {
        PlayerId = id;
        var teamId = ClubLinks.GetLinkState()?.TeamId ?? 0;

        SelectedGroup = GameReviewMatrix.ParseGroup(tab);
        GroupFields = GameReviewMatrix.FieldsFor(SelectedGroup);
        SortKey = sort;
        SortDescending = string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase);
        // The trends default to newest-first unless a column was clicked.
        if (SortKey is null)
        {
            SortKey = PlayerHistory.DateKey;
            SortDescending = true;
        }

        var capture = await snapshots.GetLatestAsync(teamId);
        var player = capture?.Players.FirstOrDefault(entry => entry.Id == id);
        IsKnown = player is not null;
        PlayerName = player?.Name is { Length: > 0 } name ? name : $"Player {id}";
        PlayerCsr = player?.Csr;

        // The season card's options are the seasons the cache holds a Fixture for, newest
        // first; a valid `?season=` choice wins, else the newest. One season leaves no
        // selector to draw.
        var seasonOptions = await cache.GetCachedSeasonsAsync(teamId);
        SeasonOptions = seasonOptions;
        Season = season is int requested && seasonOptions.Contains(requested)
            ? requested
            : seasonOptions.Count > 0 ? seasonOptions[0] : 0;

        // The Player's cached appearances drive both the trends and whether the season
        // card belongs: it is for a Player in the latest snapshot, or one we cached while
        // ours (a transferred-out Player) — never a foreign id, which makes no `ps` read.
        var history = await cache.GetPlayerFixtureHistoryAsync(id);
        ShowSeasonCard = Season > 0 && (IsKnown || history.Count > 0);

        // `ps` is fetched once per Player+season (1 call) on first view and cached.
        if (ShowSeasonCard)
        {
            var latest = await cache.GetLatestCachedFixtureAsync(teamId);
            try
            {
                await cache.GetOrRefreshPlayerSeasonsAsync([id], Season, latest?.FinishUtc);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "The season refresh failed for Player {PlayerId}", id);
            }

            SeasonTotals = PlayerHistory.BuildSeasonTotals(await cache.GetPlayerSeasonAsync(id, Season));
        }

        if (history.Count == 0)
        {
            return Page();
        }

        var teamFacts = await cache.GetTeamFactsAsync(
            history.SelectMany(entry => new[] { entry.Fixture.HomeTeamId, entry.Fixture.GuestTeamId }));
        var teamNames = teamFacts.ToDictionary(pair => pair.Key, pair => pair.Value.Name);
        var teamBots = teamFacts.ToDictionary(pair => pair.Key, pair => pair.Value.Bot);

        var rows = PlayerHistory.BuildRows(history, teamId, teamNames, teamBots, SelectedGroup);
        Rows = PlayerHistory.Sort(rows, SelectedGroup, SortKey, SortDescending);
        Sparklines = PlayerHistory.Sparklines(rows, SelectedGroup);

        return Page();
    }
}
