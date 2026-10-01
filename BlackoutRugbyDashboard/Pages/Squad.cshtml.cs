using System.Globalization;
using BlackoutRugbyDashboard.Data;
using BlackoutRugbyDashboard.Models;
using BlackoutRugbyDashboard.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlackoutRugbyDashboard.Pages;

/// <summary>
/// The Squad page (D-Squad, S2 load model): a plain GET renders the club strip
/// (club name, Team CSR, derived season, when the last capture happened) and every
/// card from the Match Cache and the latest Squad Snapshot — zero API calls, because
/// the render path (<see cref="SquadPageReader"/>) holds no API client at all. The
/// legacy control panel (API endpoint field, Team ID field, the hardcoded Season
/// 50–80 list and the Load POST) is deleted; the page's only live read is the
/// explicit Capture squad action (<see cref="SquadCaptureService"/>, reached from
/// the Capture POST handler alone). With nothing cached the page shows the bootstrap
/// empty state instead of fetching anything on load. D3's failure patterns still own
/// the banner and the hard-error panel with one button to Settings.
/// </summary>
public class SquadModel : ClubLinkedPageModel
{
    /// <summary>D3 on a failed capture: the render survives it, so the page says so.</summary>
    private const string CachedDataStillRenders =
        "The cards below still render from the Match Cache and your last capture.";

    private readonly SquadPageReader _reader;
    private readonly SquadCaptureService _captures;
    private readonly BlackoutRugbyResponseAdapter _adapter;
    private readonly ApiLogger _apiLogger;
    private readonly ILogger<SquadModel> _logger;

    public SquadModel(
        SquadPageReader reader,
        SquadCaptureService captures,
        BlackoutRugbyResponseAdapter adapter,
        ApiLogger apiLogger,
        ClubLinkService clubLinks,
        ILogger<SquadModel> logger) : base(clubLinks)
    {
        _reader = reader;
        _captures = captures;
        _adapter = adapter;
        _apiLogger = apiLogger;
        _logger = logger;
    }

    /// <summary>C1 as the club strip: the club's identity and derived season.</summary>
    public ClubStrip Club { get; private set; } = new();

    /// <summary>The season the page's aggregates are scoped to: the newest cached
    /// Fixture's season, 0 when nothing is cached yet.</summary>
    public int Season => Club.Season;

    /// <summary>True when the club has nothing cached at all (no completed Fixture
    /// and no Squad Snapshot): the bootstrap empty state, never a silent fetch.</summary>
    public bool IsColdStart { get; private set; }

    /// <summary>True when a Squad Snapshot exists — the comparison card's source.</summary>
    public bool HasCapture { get; private set; }

    public TeamDashboardViewModel? Dashboard { get; private set; }
    public List<GameStats> GameStatsList { get; private set; } = new();
    public List<FixtureBreakdown> FixtureBreakdowns { get; private set; } = new();

    /// <summary>
    /// The Game review card (C3, #35 + #36): the cached Fixtures as choices (newest
    /// first), the chosen Fixture, its Stat-Group tab, the column sort and how every
    /// stat cell reads (value, value + movement, movement alone). The whole
    /// selection rides the query string, so the card is server-rendered and every
    /// tab, mode and column is a link — no script needed to sort or to switch.
    /// </summary>
    public IReadOnlyList<GameChoice> GameChoices { get; private set; } = Array.Empty<GameChoice>();
    public int? SelectedFixtureId { get; private set; }
    public StatGroup SelectedGroup { get; private set; } = StatGroup.Attack;
    public IReadOnlyList<GameReviewField> GroupFields { get; private set; } = GameReviewMatrix.FieldsFor(StatGroup.Attack);
    public IReadOnlyList<GameReviewRow> GameRows { get; private set; } = Array.Empty<GameReviewRow>();
    public string? SortKey { get; private set; }
    public bool SortDescending { get; private set; }

    /// <summary>How the matrix's cells read (S3, #36).</summary>
    public DeltaMode Mode { get; private set; } = GameReviewMatrix.DefaultMode;

    /// <summary>
    /// The Game review card's view (S3, #37): the per-game matrix or the trend
    /// across the cached window. Query-string state, like the tab and the sort.
    /// </summary>
    public GameReviewView View { get; private set; } = GameReviewMatrix.DefaultView;

    /// <summary>
    /// The Trend view's chosen stat and its rows (S3, #37): the picker's field key
    /// and label, the full option list (every charted field plus the three output
    /// columns) and one row per squad member carrying their window series, latest
    /// reading, movement and total.
    /// </summary>
    public string TrendStatKey { get; private set; } = GameTrend.DefaultStat.Key;
    public string TrendStatLabel { get; private set; } = GameTrend.DefaultStat.Label;
    public IReadOnlyList<GameTrendStat> TrendStatOptions => GameTrend.Stats;
    public IReadOnlyList<GameTrendRow> TrendRows { get; private set; } = Array.Empty<GameTrendRow>();

    /// <summary>The chosen Fixture in words: when, what competition, against whom, and how it went.</summary>
    public string? GameContext { get; private set; }

    /// <summary>
    /// The baseline game the deltas read against (S3, #36): the next completed
    /// Fixture back in the cached window, in the same words as
    /// <see cref="GameContext"/>. Null when the chosen Fixture is the oldest one
    /// cached — then every delta is a dash and the page says why.
    /// </summary>
    public int? PreviousFixtureId { get; private set; }
    public string? PreviousGameContext { get; private set; }

    /// <summary>
    /// The Coach's eye for the chosen game and tab (#36): the standout on the tab's
    /// headline metric, then the worst offender on its adverse metric. Empty when
    /// nothing qualifies — the line then offers the sort hint alone.
    /// </summary>
    public IReadOnlyList<CoachEyeItem> CoachEye { get; private set; } = Array.Empty<CoachEyeItem>();

    public string? StatusMessage { get; private set; }
    public string? WarningMessage { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string ApiLogsJson { get; private set; } = "[]";

    /// <summary>
    /// The Team review card (C6, #38): which of its tabs is open, and that tab's
    /// eight small-multiple charts over the cached window. Query-string state, like
    /// the Game review card, so the tab is a plain link.
    /// </summary>
    public TeamReviewTab ReviewTab { get; private set; } = TeamReviewBuilder.DefaultTab;
    public IReadOnlyList<TeamReviewTab> ReviewTabs => TeamReviewBuilder.Tabs;
    public IReadOnlyList<SvgChart> TeamReviewCharts { get; private set; } = Array.Empty<SvgChart>();

    /// <summary>
    /// The comparison card (C4, unchanged in shape): the chosen stat, the squad
    /// ranked by it (present-state values from the latest capture), and where the
    /// squad median falls. Query-string state — the selector is a form, not script.
    /// </summary>
    public string ComparisonStatKey { get; private set; } = ComparisonStats[0].Key;
    public string ComparisonStatLabel { get; private set; } = ComparisonStats[0].Label;
    public IReadOnlyList<ComparisonStat> ComparisonStatOptions => ComparisonStats;
    public IReadOnlyList<ComparisonBar> ComparisonBars { get; private set; } = Array.Empty<ComparisonBar>();
    public string ComparisonMedianPercent { get; private set; } = "0";

    /// <summary>
    /// The cache-first render — what a plain GET does (no capture), and what every
    /// Capture press re-runs (with that capture's outcome). The Game review and
    /// Team review selections arrive as query-string state on both.
    /// </summary>
    public Task OnGetAsync(int? game = null, string? tab = null, string? sort = null, string? dir = null, string? mode = null, string? view = null, string? stat = null, string? teamtab = null, string? c4stat = null) =>
        RenderAsync(capture: null, game, tab, sort, dir, mode, view, stat, teamtab, c4stat);

    /// <summary>
    /// Capture squad (S2, #34): the page's one live action — the roster read
    /// (2 calls), the Squad Snapshot it saves, and the club's own TeamFact — and
    /// then the same cache-first render, so the page shows the values just read.
    /// It bootstraps the cold start with no other step. D3 owns the failures: a
    /// rejection panels and deep-links to Settings, a transport failure replays
    /// the last capture as a warning, and in both cases the cached cards stand.
    /// </summary>
    public async Task<IActionResult> OnPostCaptureAsync(int? game = null, string? tab = null, string? sort = null, string? dir = null, string? mode = null, string? view = null, string? stat = null, string? teamtab = null, string? c4stat = null)
    {
        var teamId = ClubLinks.GetLinkState()?.TeamId ?? 0;
        var capture = await _captures.CaptureAsync(teamId);

        await RenderAsync(capture, game, tab, sort, dir, mode, view, stat, teamtab, c4stat);

        if (capture.SavedSnapshot)
        {
            StatusMessage = Join(capture.Message, StatusMessage);
        }
        else if (capture.IsHardError)
        {
            ErrorMessage = capture.Message;
            WarningMessage = Join(CachedDataStillRenders, WarningMessage);
        }
        else
        {
            WarningMessage = Join(capture.Message, WarningMessage);
        }

        ApiLogsJson = _apiLogger.GetLogsJson();
        return Page();
    }

    private async Task RenderAsync(
        SquadCaptureResult? capture, int? game = null, string? tab = null, string? sort = null, string? dir = null, string? mode = null, string? view = null, string? stat = null, string? teamtab = null, string? c4stat = null)
    {
        _apiLogger.Clear();
        StatusMessage = null;
        WarningMessage = null;
        ErrorMessage = null;
        Dashboard = null;
        HasCapture = false;
        IsColdStart = false;
        GameStatsList = new List<GameStats>();
        FixtureBreakdowns = new List<FixtureBreakdown>();
        GameChoices = Array.Empty<GameChoice>();
        GameRows = Array.Empty<GameReviewRow>();
        CoachEye = Array.Empty<CoachEyeItem>();
        SelectedFixtureId = null;
        SelectedGroup = GameReviewMatrix.ParseGroup(tab);
        GroupFields = GameReviewMatrix.FieldsFor(SelectedGroup);
        SortKey = sort;
        SortDescending = string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase);
        Mode = GameReviewMatrix.ParseMode(mode);
        View = GameReviewMatrix.ParseView(view);
        var trendStat = GameTrend.Resolve(stat);
        TrendStatKey = trendStat.Key;
        TrendStatLabel = trendStat.Label;
        TrendRows = Array.Empty<GameTrendRow>();
        GameContext = null;
        PreviousFixtureId = null;
        PreviousGameContext = null;
        ReviewTab = TeamReviewBuilder.Parse(teamtab);
        TeamReviewCharts = Array.Empty<SvgChart>();
        ComparisonBars = Array.Empty<ComparisonBar>();
        ComparisonMedianPercent = "0";

        var teamId = ClubLinks.GetLinkState()?.TeamId ?? 0;

        try
        {
            var page = await _reader.ReadAsync(teamId);

            Club = new ClubStrip
            {
                TeamId = teamId,
                Name = page.ClubName,
                TeamCsr = page.TeamCsr,
                IsBot = page.ClubIsBot,
                Season = page.Season,
                CapturedAtUtc = page.Capture?.CapturedAtUtc,
                CapturedPlayerCount = page.Capture?.Players.Count ?? 0
            };
            IsColdStart = page.IsColdStart;
            HasCapture = page.Capture is not null;

            _apiLogger.LogCache(
                "match-cache/fixtures",
                $"{page.Window.Count} completed cached Fixture(s) rendered from the Match Cache — no API call");
            _apiLogger.LogCache(
                "match-cache/playerstatistics",
                $"{page.SeasonStats.Count}/{page.Capture?.Players.Count ?? 0} season read(s) served from cache");

            Dashboard = BuildDashboardViewModel(page);
            FixtureBreakdowns = BuildFixtureBreakdowns(page);
            GameStatsList = FixtureBreakdowns
                .Select(item => item.TeamStats)
                .OrderByDescending(item => item.Date)
                .ToList();
            BuildGameReview(page, game);
            TrendRows = GameTrend.Build(page, TrendStatKey);
            TeamReviewCharts = TeamReviewBuilder.Build(page, ReviewTab);
            BuildComparison(c4stat);

            // The cold start speaks for itself in the bootstrap panel — the banner
            // is for the warm page, where "what came from where" needs saying.
            StatusMessage = page.IsColdStart ? null : BuildStatusMessage(page, capture);

            if (page.Capture is { Players.Count: > 0 } latestCapture
                && page.SeasonStats.Count < latestCapture.Players.Count)
            {
                var missing = latestCapture.Players.Count - page.SeasonStats.Count;
                WarningMessage = page.Season > 0
                    ? $"{missing} of {latestCapture.Players.Count} players have no cached Season {page.Season} statistics yet — those aggregate columns read the captured values instead."
                    : $"{missing} of {latestCapture.Players.Count} players have no cached season statistics yet — the aggregate columns stay at the captured values until season reads are cached.";
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "The Squad page failed to render from cache for team {TeamId}", teamId);
            ErrorMessage = $"The Squad page could not read its cached data: {exception.Message}";
        }

        ApiLogsJson = _apiLogger.GetLogsJson();
    }

    /// <summary>
    /// The comparison card's stat list (C4, unchanged): the same options the legacy
    /// dropdown offered, in the same groups and order, now the single source for
    /// both the rendered selector and the ranked bars. Explicit rather than a
    /// reflection sweep, so the list is greppable and testable.
    /// </summary>
    private static readonly IReadOnlyList<ComparisonStat> ComparisonStats = new ComparisonStat[]
    {
        new("totalPoints", "Total Points", "General", player => player.TotalPoints),
        new("csr", "CSR Rating", "General", player => player.Csr),
        new("energy", "Energy", "General", player => player.Energy),
        new("form", "Form", "General", player => player.Form),
        new("salary", "Salary", "General", player => player.Salary),
        new("age", "Age", "General", player => player.Age),
        new("tackles", "Tackles", "Match Performance", player => player.Tackles),
        new("metresGained", "Metres Gained", "Match Performance", player => player.MetresGained),
        new("tries", "Tries", "Match Performance", player => player.Tries),
        new("conversions", "Conversions", "Match Performance", player => player.Conversions),
        new("dropGoals", "Drop Goals", "Match Performance", player => player.DropGoals),
        new("penalties", "Penalties", "Match Performance", player => player.Penalties),
        new("kickingMetres", "Kicking Metres", "Match Performance", player => player.KickingMetres),
        new("linebreaks", "Linebreaks", "Offensive", player => player.Linebreaks),
        new("beatenDefenders", "Beaten Defenders", "Offensive", player => player.BeatenDefenders),
        new("tryAssists", "Try Assists", "Offensive", player => player.TryAssists),
        new("forwardPasses", "Forward Passes", "Offensive", player => player.ForwardPasses),
        new("kicksOutOnTheFull", "Kicks Out On Full", "Offensive", player => player.KicksOutOnTheFull),
        new("kicks", "Kicks", "Offensive", player => player.Kicks),
        new("missedTackles", "Missed Tackles", "Defensive", player => player.MissedTackles),
        new("intercepts", "Intercepts", "Defensive", player => player.Intercepts),
        new("turnoversWon", "Turnovers Won", "Defensive", player => player.TurnoversWon),
        new("penaltiesConceded", "Penalties Conceded", "Defensive", player => player.PenaltiesConceded),
        new("successfulLineoutThrows", "Successful Lineout Throws", "Set Pieces", player => player.SuccessfulLineoutThrows),
        new("unsuccessfulLineoutThrows", "Unsuccessful Lineout Throws", "Set Pieces", player => player.UnsuccessfulLineoutThrows),
        new("lineoutsSecured", "Lineouts Secured", "Set Pieces", player => player.LineoutsSecured),
        new("lineoutsConceded", "Lineouts Conceded", "Set Pieces", player => player.LineoutsConceded),
        new("lineoutsStolen", "Lineouts Stolen", "Set Pieces", player => player.LineoutsStolen),
        new("yellowCards", "Yellow Cards", "Disciplinary", player => player.YellowCards),
        new("redCards", "Red Cards", "Disciplinary", player => player.RedCards),
        new("fights", "Fights", "Disciplinary", player => player.Fights),
        new("injuries", "Injuries", "Disciplinary", player => player.Injuries),
        new("knockOns", "Knock Ons", "Other Stats", player => player.KnockOns),
        new("handlingErrors", "Handling Errors", "Other Stats", player => player.HandlingErrors),
        new("missedConversions", "Missed Conversions", "Other Stats", player => player.MissedConversions),
        new("missedDropGoals", "Missed Drop Goals", "Other Stats", player => player.MissedDropGoals),
        new("missedPenalties", "Missed Penalties", "Other Stats", player => player.MissedPenalties),
        new("goodUpAndUnders", "Good Up And Unders", "Other Stats", player => player.GoodUpAndUnders),
        new("badUpAndUnders", "Bad Up And Unders", "Other Stats", player => player.BadUpAndUnders),
        new("upAndUnders", "Up And Unders", "Other Stats", player => player.UpAndUnders),
        new("goodKicks", "Good Kicks", "Other Stats", player => player.GoodKicks),
        new("badKicks", "Bad Kicks", "Other Stats", player => player.BadKicks),
        new("ballTime", "Ball Time", "Other Stats", player => player.BallTime),
        new("penaltyTime", "Penalty Time", "Other Stats", player => player.PenaltyTime),
        new("totalCaps", "Total Caps", "Caps", player => player.TotalCaps),
        new("leagueCaps", "League Caps", "Caps", player => player.LeagueCaps),
        new("friendlyCaps", "Friendly Caps", "Caps", player => player.FriendlyCaps),
        new("cupCaps", "Cup Caps", "Caps", player => player.CupCaps),
        new("nationalCaps", "National Caps", "Caps", player => player.NationalCaps),
        new("underTwentyCaps", "Under-20 Caps", "Caps", player => player.UnderTwentyCaps),
        new("worldCupCaps", "World Cup Caps", "Caps", player => player.WorldCupCaps),
        new("underTwentyWorldCupCaps", "U20 World Cup Caps", "Caps", player => player.UnderTwentyWorldCupCaps),
        new("otherCaps", "Other Caps", "Caps", player => player.OtherCaps)
    };

    /// <summary>How many rows the comparison card emphasises as the squad's
    /// strongest; the rest render de-emphasised (the card's long-standing rule).</summary>
    private const int ComparisonTop = 5;

    /// <summary>
    /// The comparison card's bars (C4, unchanged in shape): the captured squad
    /// ranked by the chosen stat, from the latest Squad Snapshot. A stat key the
    /// card does not know falls back to the first option rather than rendering
    /// nothing, so a hand-edited query string can never blank the card.
    /// </summary>
    private void BuildComparison(string? statKey)
    {
        var stat = ComparisonStats.FirstOrDefault(option => option.Key == statKey) ?? ComparisonStats[0];
        ComparisonStatKey = stat.Key;
        ComparisonStatLabel = stat.Label;

        var players = Dashboard?.Players ?? Array.Empty<PlayerDashboardItem>();
        if (players.Count == 0)
        {
            return;
        }

        var ranked = players
            .Select(player => (player.Name, Value: stat.Value(player)))
            .OrderByDescending(entry => entry.Value)
            .ThenBy(entry => entry.Name, StringComparer.Ordinal)
            .ToList();

        var max = ranked.Max(entry => entry.Value);
        var scale = max > 0 ? max * 1.06 : 1;
        var median = Median(ranked.Select(entry => entry.Value).ToList());

        ComparisonMedianPercent = Percent(median, scale);
        ComparisonBars = ranked
            .Select((entry, index) => new ComparisonBar(entry.Name, entry.Value, Percent(entry.Value, scale), index >= ComparisonTop))
            .ToList();
    }

    private static string Percent(int value, double scale) =>
        Math.Round(value / scale * 100, 1, MidpointRounding.AwayFromZero).ToString("0.#", CultureInfo.InvariantCulture);

    private static int Median(IReadOnlyList<int> values)
    {
        var ordered = values.OrderBy(value => value).ToList();
        if (ordered.Count == 0)
        {
            return 0;
        }

        var middle = ordered.Count / 2;
        return ordered.Count % 2 == 1
            ? ordered[middle]
            : (int)Math.Round((ordered[middle - 1] + ordered[middle]) / 2.0, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// The comparison card's source (C4, unchanged in shape): present-state values
    /// (CSR, salary, form, energy, age) come from the latest Squad Snapshot, and the
    /// aggregates from the cached season reads — falling back to the values stored
    /// with the Snapshot where a player's season read is not cached.
    /// </summary>
    private TeamDashboardViewModel BuildDashboardViewModel(SquadPageData page)
    {
        var captured = page.Capture?.Players ?? new List<PlayerSnapshotRecord>();
        var stats = page.SeasonStats;

        if (captured.Count == 0)
        {
            return new TeamDashboardViewModel
            {
                TeamId = page.TeamId,
                TeamName = page.ClubName,
                CountryIso = page.CountryIso
            };
        }

        var dashboardPlayers = captured
            .Select(player =>
            {
                stats.TryGetValue(player.Id, out var season);

                return new PlayerDashboardItem
                {
                    Id = player.Id,
                    Name = player.Name,
                    Age = player.Age,
                    Csr = player.Csr,
                    Salary = player.Salary,
                    Form = player.Form,
                    Energy = player.Energy,
                    Tackles = season?.Tackles ?? player.Tackles,
                    MetresGained = season?.MetresGained ?? player.MetresGained,
                    Tries = season?.Tries ?? player.Tries,
                    TotalPoints = season?.TotalPoints ?? player.TotalPoints,
                    TotalCaps = season?.TotalCaps ?? player.TotalCaps,
                    Conversions = season?.Conversions ?? 0,
                    DropGoals = season?.DropGoals ?? 0,
                    Penalties = season?.Penalties ?? 0,
                    YellowCards = season?.YellowCards ?? 0,
                    RedCards = season?.RedCards ?? 0,
                    Linebreaks = season?.Linebreaks ?? 0,
                    Intercepts = season?.Intercepts ?? 0,
                    Kicks = season?.Kicks ?? 0,
                    KnockOns = season?.KnockOns ?? 0,
                    ForwardPasses = season?.ForwardPasses ?? 0,
                    TryAssists = season?.TryAssists ?? 0,
                    BeatenDefenders = season?.BeatenDefenders ?? 0,
                    Injuries = season?.Injuries ?? 0,
                    HandlingErrors = season?.HandlingErrors ?? 0,
                    MissedTackles = season?.MissedTackles ?? 0,
                    Fights = season?.Fights ?? 0,
                    KickingMetres = season?.KickingMetres ?? 0,
                    MissedConversions = season?.MissedConversions ?? 0,
                    MissedDropGoals = season?.MissedDropGoals ?? 0,
                    MissedPenalties = season?.MissedPenalties ?? 0,
                    GoodUpAndUnders = season?.GoodUpAndUnders ?? 0,
                    BadUpAndUnders = season?.BadUpAndUnders ?? 0,
                    UpAndUnders = season?.UpAndUnders ?? 0,
                    GoodKicks = season?.GoodKicks ?? 0,
                    BadKicks = season?.BadKicks ?? 0,
                    TurnoversWon = season?.TurnoversWon ?? 0,
                    LineoutsSecured = season?.LineoutsSecured ?? 0,
                    LineoutsConceded = season?.LineoutsConceded ?? 0,
                    LineoutsStolen = season?.LineoutsStolen ?? 0,
                    SuccessfulLineoutThrows = season?.SuccessfulLineoutThrows ?? 0,
                    UnsuccessfulLineoutThrows = season?.UnsuccessfulLineoutThrows ?? 0,
                    PenaltiesConceded = season?.PenaltiesConceded ?? 0,
                    KicksOutOnTheFull = season?.KicksOutOnTheFull ?? 0,
                    BallTime = season?.BallTime ?? 0,
                    PenaltyTime = season?.PenaltyTime ?? 0,
                    LeagueCaps = season?.LeagueCaps ?? 0,
                    FriendlyCaps = season?.FriendlyCaps ?? 0,
                    CupCaps = season?.CupCaps ?? 0,
                    UnderTwentyCaps = season?.UnderTwentyCaps ?? 0,
                    NationalCaps = season?.NationalCaps ?? 0,
                    WorldCupCaps = season?.WorldCupCaps ?? 0,
                    UnderTwentyWorldCupCaps = season?.UnderTwentyWorldCupCaps ?? 0,
                    OtherCaps = season?.OtherCaps ?? 0
                };
            })
            .OrderByDescending(player => player.TotalPoints)
            .ThenByDescending(player => player.Tackles)
            .ToList();

        return new TeamDashboardViewModel
        {
            TeamId = page.TeamId,
            TeamName = page.ClubName,
            CountryIso = page.CountryIso,
            PlayerCount = dashboardPlayers.Count,
            AverageAge = RoundAverage(dashboardPlayers.Select(player => player.Age)),
            AverageCsr = RoundAverage(dashboardPlayers.Select(player => player.Csr)),
            AverageForm = RoundAverage(dashboardPlayers.Select(player => player.Form)),
            AverageEnergy = RoundAverage(dashboardPlayers.Select(player => player.Energy)),
            TotalSalary = dashboardPlayers.Sum(player => player.Salary),
            TotalPoints = dashboardPlayers.Sum(player => player.TotalPoints),
            TotalTries = dashboardPlayers.Sum(player => player.Tries),
            TotalTackles = dashboardPlayers.Sum(player => player.Tackles),
            TotalMetres = dashboardPlayers.Sum(player => player.MetresGained),
            Players = dashboardPlayers
        };
    }

    private List<FixtureBreakdown> BuildFixtureBreakdowns(SquadPageData page)
    {
        var breakdowns = new List<FixtureBreakdown>();
        var playerNames = (Dashboard?.Players ?? Array.Empty<PlayerDashboardItem>())
            .GroupBy(player => player.Id)
            .ToDictionary(group => group.Key, group => group.First().Name);

        foreach (var entry in page.Window)
        {
            if (entry.Players.Count == 0)
            {
                continue; // unplayed or not-yet-filled — the same skip rule the live parse had
            }

            var playerStats = _adapter.ToFixturePlayerStatistics(entry.Players, entry.TeamStats, playerNames);
            breakdowns.Add(new FixtureBreakdown
            {
                FixtureId = entry.Fixture.FixtureId,
                Label = BuildFixtureLabel(entry.Fixture.Season, entry.Fixture.Round, entry.Fixture.Competition, GameDate(entry.Fixture)),
                TeamStats = BuildGameStats(page, entry, playerStats),
                PlayerStats = playerStats
            });
        }

        return breakdowns;
    }

    /// <summary>
    /// Builds one fixture's GameStats from the cached rows: the opponent's name
    /// from the captured TeamFacts, the score from the cached Match Summary. A
    /// Fixture whose summary was never cached is left without a result rather
    /// than being reported as a 0–0 draw.
    /// </summary>
    private GameStats BuildGameStats(SquadPageData page, SquadFixtureRows entry, IReadOnlyList<FixturePlayerStatistics> playerStats)
    {
        var row = entry.Fixture;
        var isHome = row.HomeTeamId == page.TeamId;
        var opponentId = isHome ? row.GuestTeamId : row.HomeTeamId;

        var opponent = page.TeamNames.TryGetValue(opponentId, out var cachedName) && !string.IsNullOrWhiteSpace(cachedName)
            ? cachedName
            : $"Team {opponentId}";

        var score = entry.Summary is null ? 0 : isHome ? entry.Summary.HomePoints : entry.Summary.GuestPoints;
        var oppositionScore = entry.Summary is null ? 0 : isHome ? entry.Summary.GuestPoints : entry.Summary.HomePoints;

        return new GameStats
        {
            FixtureId = row.FixtureId,
            Season = row.Season,
            Round = row.Round,
            Competition = row.Competition,
            Date = GameDate(row),
            Opponent = opponent,
            Score = score,
            OppositionScore = oppositionScore,
            Result = entry.Summary is null
                ? string.Empty
                : score > oppositionScore ? "W" : score < oppositionScore ? "L" : "D",
            Tackles = playerStats.Sum(item => item.Tackles),
            MetresGained = playerStats.Sum(item => item.MetresGained),
            Tries = playerStats.Sum(item => item.Tries),
            Conversions = playerStats.Sum(item => item.Conversions),
            DropGoals = playerStats.Sum(item => item.DropGoals),
            Penalties = playerStats.Sum(item => item.Penalties),
            TotalPoints = playerStats.Sum(item => item.TotalPoints),
            YellowCards = playerStats.Sum(item => item.YellowCards),
            RedCards = playerStats.Sum(item => item.RedCards),
            Linebreaks = playerStats.Sum(item => item.Linebreaks),
            Intercepts = playerStats.Sum(item => item.Intercepts),
            Kicks = playerStats.Sum(item => item.Kicks),
            KnockOns = playerStats.Sum(item => item.KnockOns),
            ForwardPasses = playerStats.Sum(item => item.ForwardPasses),
            TryAssists = playerStats.Sum(item => item.TryAssists),
            BeatenDefenders = playerStats.Sum(item => item.BeatenDefenders),
            Injuries = playerStats.Sum(item => item.Injuries),
            HandlingErrors = playerStats.Sum(item => item.HandlingErrors),
            MissedTackles = playerStats.Sum(item => item.MissedTackles),
            Fights = playerStats.Sum(item => item.Fights),
            KickingMetres = playerStats.Sum(item => item.KickingMetres),
            PenaltiesConceded = playerStats.Sum(item => item.PenaltiesConceded),
            KicksOutOnTheFull = playerStats.Sum(item => item.KicksOutOnTheFull),
            LineoutsWon = playerStats.Sum(item => item.LineoutsWon),
            LineoutsLost = playerStats.Sum(item => item.LineoutsLost),
            ScrumWins = playerStats.Sum(item => item.ScrumWins),
            ScrumLosses = playerStats.Sum(item => item.ScrumLosses)
        };
    }

    /// <summary>
    /// The Game review card's data (C3, #35 + #36): the cached Fixtures as choices
    /// (newest first), the chosen Fixture's matrix rows for the chosen Stat Group,
    /// each value carrying its delta against the previous cached game and the tab's
    /// Coach's eye, sorted by the chosen column. Cache-only — it reads the page's
    /// cached window, rows and capture and nothing else.
    /// </summary>
    private void BuildGameReview(SquadPageData page, int? requestedFixtureId)
    {
        // Newest first, the order the selector reads in: the previous cached game is
        // the next one along, whatever competition it belonged to.
        var window = page.Window.OrderByDescending(entry => entry.Fixture.MatchStartUnix).ToList();

        GameChoices = window
            .Select(entry => new GameChoice
            {
                FixtureId = entry.Fixture.FixtureId,
                Label = BuildGameChoiceLabel(page, entry)
            })
            .ToList();

        // The requested Fixture may be gone from the cached window (or the request
        // may name none): the newest cached game answers instead of an empty card.
        var requestedIndex = requestedFixtureId is { } requested
            ? window.FindIndex(entry => entry.Fixture.FixtureId == requested)
            : -1;
        var selectedIndex = requestedIndex >= 0 ? requestedIndex : 0;

        if (window.Count == 0)
        {
            GameRows = Array.Empty<GameReviewRow>();
            return;
        }

        var selected = window[selectedIndex];
        var previous = selectedIndex + 1 < window.Count ? window[selectedIndex + 1] : null;

        SelectedFixtureId = selected.Fixture.FixtureId;
        GameContext = BuildGameContext(page, selected);
        PreviousFixtureId = previous?.Fixture.FixtureId;
        PreviousGameContext = previous is null ? null : BuildGameContext(page, previous);

        GameRows = GameReviewMatrix.Sort(
            GameReviewMatrix.BuildRows(selected, page.Capture, SelectedGroup, previous),
            SelectedGroup,
            SortKey,
            SortDescending);
        CoachEye = GameReviewMatrix.BuildCoachEye(GameRows, SelectedGroup);
    }

    /// <summary>The fixture selector's option: when, what competition and round, and against whom.</summary>
    private static string BuildGameChoiceLabel(SquadPageData page, SquadFixtureRows entry)
    {
        var row = entry.Fixture;
        return $"{GameDate(row):MMM d, yyyy} · {row.Competition} R{row.Round} · vs {OpponentName(page, row)}";
    }

    /// <summary>The card's subtitle: the same context, plus how the game went when the summary is cached.</summary>
    private static string BuildGameContext(SquadPageData page, SquadFixtureRows entry)
    {
        var row = entry.Fixture;
        var isHome = row.HomeTeamId == page.TeamId;
        var outcome = entry.Summary is null
            ? "result not cached"
            : isHome
                ? $"{entry.Summary.HomePoints}-{entry.Summary.GuestPoints}"
                : $"{entry.Summary.GuestPoints}-{entry.Summary.HomePoints}";
        return $"{GameDate(row):MMM d, yyyy} · {row.Competition} R{row.Round} · vs {OpponentName(page, row)} · {outcome}";
    }

    private static string OpponentName(SquadPageData page, FixtureRow row)
    {
        var opponentId = row.HomeTeamId == page.TeamId ? row.GuestTeamId : row.HomeTeamId;
        return page.TeamNames.TryGetValue(opponentId, out var cachedName) && !string.IsNullOrWhiteSpace(cachedName)
            ? cachedName
            : $"Team {opponentId}";
    }

    private static DateTime GameDate(FixtureRow row) =>
        DateTimeOffset.FromUnixTimeSeconds(row.MatchStartUnix).LocalDateTime;

    private static string BuildFixtureLabel(int season, int round, string competition, DateTime date)
    {
        var seasonLabel = season > 0 ? $"Season {season}" : "Season";
        var roundLabel = round > 0 ? $"Week {round}" : "Fixture";
        var competitionLabel = string.IsNullOrWhiteSpace(competition) ? string.Empty : $" {competition}";
        return $"{seasonLabel}, {roundLabel}{competitionLabel} - {date:MMM d}";
    }
    /// <summary>
    /// The page's honesty line (D3's partial-load pattern, cache-first): what came
    /// from where. A plain GET (no capture outcome) also says that arriving made no
    /// API call; a saved capture names the snapshot as the one just made; a failed
    /// one says only that this render fetched nothing — the banner beside it already
    /// owns what went wrong.
    /// </summary>
    private static string BuildStatusMessage(SquadPageData page, SquadCaptureResult? capture)
    {
        var captured = capture?.SavedSnapshot == true;
        var windowPart = page.Window.Count == 0
            ? "no completed Fixture cached yet"
            : $"{page.Window.Count} cached completed Fixture(s)";
        var capturePart = page.Capture is null
            ? "no Squad Snapshot yet"
            : captured
                ? $"squad from the capture just made ({page.Capture.Players.Count} player(s))"
                : $"squad from the latest capture ({page.Capture.Players.Count} player(s))";
        var closing = capture is null
            ? " No API call was made."
            : captured ? string.Empty : " This render fetched nothing.";
        return $"Rendered from the Match Cache — {windowPart} · {capturePart}.{closing}";
    }

    /// <summary>Joins the non-blank parts of one banner line, or null when none survive.</summary>
    private static string? Join(params string?[] parts)
    {
        var kept = parts.Where(part => !string.IsNullOrWhiteSpace(part)).ToList();
        return kept.Count == 0 ? null : string.Join(" ", kept);
    }

    private static decimal RoundAverage(IEnumerable<int> values)
    {
        var list = values.ToList();
        return list.Count == 0
            ? 0
            : Math.Round((decimal)list.Average(), 1, MidpointRounding.AwayFromZero);
    }
}

/// <summary>One option of the comparison card's stat selector: its query-string
/// key, its label, the group it sits under, and the present-state value it ranks
/// the squad by.</summary>
public sealed record ComparisonStat(string Key, string Label, string Group, Func<PlayerDashboardItem, int> Value);

/// <summary>One ranked bar of the comparison card: the Player, the captured value,
/// the bar's width as a percentage, and whether the row sits outside the
/// emphasised top five.</summary>
public sealed record ComparisonBar(string Name, int Value, string Percent, bool BelowTop);

/// <summary>
/// C1 as the club strip: whose page this is (club name, Team CSR, bot flag) and
/// the season the aggregates are scoped to. Every value is cache-derived — the
/// strip makes no API calls; the Capture squad action is the page's only read.
/// </summary>
public class ClubStrip
{
    public int TeamId { get; init; }

    public string Name { get; init; } = string.Empty;

    /// <summary>The captured Team CSR, or null when no team read is cached yet.</summary>
    public int? TeamCsr { get; init; }

    public bool IsBot { get; init; }

    /// <summary>The derived season: the newest cached Fixture's season, 0 when the
    /// Match Cache holds no completed Fixture yet.</summary>
    public int Season { get; init; }

    /// <summary>When the latest Squad Snapshot was made — the freshness answer the
    /// page owes (S2/#34) — or null when nothing has been captured yet.</summary>
    public DateTime? CapturedAtUtc { get; init; }

    /// <summary>How many players that capture held.</summary>
    public int CapturedPlayerCount { get; init; }
}

/// <summary>One option of the Game review fixture selector: the cached Fixture and
/// the words that identify it (date · competition+round · opponent).</summary>
public class GameChoice
{
    public int FixtureId { get; init; }

    public string Label { get; init; } = string.Empty;
}

public class FixtureBreakdown
{
    public int FixtureId { get; set; }
    public string Label { get; set; } = string.Empty;
    public GameStats TeamStats { get; set; } = new();
    public IReadOnlyList<FixturePlayerStatistics> PlayerStats { get; set; } = Array.Empty<FixturePlayerStatistics>();
}

public class GameStats
{
    public int FixtureId { get; set; }
    public int Season { get; set; }
    public int Round { get; set; }
    public string Competition { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Opponent { get; set; } = string.Empty;
    public int Score { get; set; }
    public int OppositionScore { get; set; }
    public string Result { get; set; } = string.Empty;

    // Team stats
    public int Tackles { get; set; }
    public int MetresGained { get; set; }
    public int Tries { get; set; }
    public int Conversions { get; set; }
    public int DropGoals { get; set; }
    public int Penalties { get; set; }
    public int TotalPoints { get; set; }
    public int YellowCards { get; set; }
    public int RedCards { get; set; }
    public int Linebreaks { get; set; }
    public int Intercepts { get; set; }
    public int Kicks { get; set; }
    public int KnockOns { get; set; }
    public int ForwardPasses { get; set; }
    public int TryAssists { get; set; }
    public int BeatenDefenders { get; set; }
    public int Injuries { get; set; }
    public int HandlingErrors { get; set; }
    public int MissedTackles { get; set; }
    public int Fights { get; set; }
    public int KickingMetres { get; set; }
    public int PenaltiesConceded { get; set; }
    public int KicksOutOnTheFull { get; set; }
    public int LineoutsWon { get; set; }
    public int LineoutsLost { get; set; }
    public int ScrumWins { get; set; }
    public int ScrumLosses { get; set; }
}