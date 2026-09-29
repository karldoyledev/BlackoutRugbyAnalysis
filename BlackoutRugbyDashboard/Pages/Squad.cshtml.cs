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
    /// The Game review card (C3, #35): the cached Fixtures as choices (newest
    /// first), the chosen Fixture, its Stat-Group tab and the column sort. The
    /// whole selection rides the query string, so the card is server-rendered and
    /// every tab and column is a link — no script needed to sort.
    /// </summary>
    public IReadOnlyList<GameChoice> GameChoices { get; private set; } = Array.Empty<GameChoice>();
    public int? SelectedFixtureId { get; private set; }
    public StatGroup SelectedGroup { get; private set; } = StatGroup.Attack;
    public IReadOnlyList<GameReviewField> GroupFields { get; private set; } = GameReviewMatrix.FieldsFor(StatGroup.Attack);
    public IReadOnlyList<GameReviewRow> GameRows { get; private set; } = Array.Empty<GameReviewRow>();
    public string? SortKey { get; private set; }
    public bool SortDescending { get; private set; }

    /// <summary>The chosen Fixture in words: when, what competition, against whom, and how it went.</summary>
    public string? GameContext { get; private set; }

    public string? StatusMessage { get; private set; }
    public string? WarningMessage { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string ApiLogsJson { get; private set; } = "[]";

    /// <summary>
    /// The cache-first render — what a plain GET does (no capture), and what every
    /// Capture press re-runs (with that capture's outcome). The Game review
    /// selection arrives as query-string state on both.
    /// </summary>
    public Task OnGetAsync(int? game = null, string? tab = null, string? sort = null, string? dir = null) =>
        RenderAsync(capture: null, game, tab, sort, dir);

    /// <summary>
    /// Capture squad (S2, #34): the page's one live action — the roster read
    /// (2 calls), the Squad Snapshot it saves, and the club's own TeamFact — and
    /// then the same cache-first render, so the page shows the values just read.
    /// It bootstraps the cold start with no other step. D3 owns the failures: a
    /// rejection panels and deep-links to Settings, a transport failure replays
    /// the last capture as a warning, and in both cases the cached cards stand.
    /// </summary>
    public async Task<IActionResult> OnPostCaptureAsync(int? game = null, string? tab = null, string? sort = null, string? dir = null)
    {
        var teamId = ClubLinks.GetLinkState()?.TeamId ?? 0;
        var capture = await _captures.CaptureAsync(teamId);

        await RenderAsync(capture, game, tab, sort, dir);

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
        SquadCaptureResult? capture, int? game = null, string? tab = null, string? sort = null, string? dir = null)
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
        SelectedFixtureId = null;
        SelectedGroup = GameReviewMatrix.ParseGroup(tab);
        GroupFields = GameReviewMatrix.FieldsFor(SelectedGroup);
        SortKey = sort;
        SortDescending = string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase);
        GameContext = null;

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
    /// The Game review card's data (C3, #35): the cached Fixtures as choices
    /// (newest first, as the window arrives), the chosen Fixture's matrix rows for
    /// the chosen Stat Group, sorted by the chosen column. Cache-only — it reads
    /// the page's cached window, rows and capture and nothing else.
    /// </summary>
    private void BuildGameReview(SquadPageData page, int? requestedFixtureId)
    {
        var choices = new List<GameChoice>();
        var rowsById = new Dictionary<int, SquadFixtureRows>();
        foreach (var entry in page.Window)
        {
            rowsById[entry.Fixture.FixtureId] = entry;
            choices.Add(new GameChoice
            {
                FixtureId = entry.Fixture.FixtureId,
                Label = BuildGameChoiceLabel(page, entry)
            });
        }

        GameChoices = choices;

        // The requested Fixture may be gone from the cached window (or the request
        // may name none): the newest cached game answers instead of an empty card.
        var selected = requestedFixtureId is { } requested && rowsById.TryGetValue(requested, out var match)
            ? match
            : rowsById.Values.OrderByDescending(entry => entry.Fixture.MatchStartUnix).FirstOrDefault();

        if (selected is null)
        {
            GameRows = Array.Empty<GameReviewRow>();
            return;
        }

        SelectedFixtureId = selected.Fixture.FixtureId;
        GameContext = BuildGameContext(page, selected);
        GameRows = GameReviewMatrix.Sort(
            GameReviewMatrix.BuildRows(selected, page.Capture, SelectedGroup),
            SelectedGroup,
            SortKey,
            SortDescending);
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

    public static string FormatDelta(int value) =>
        value > 0 ? $"+{value}" : value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static decimal RoundAverage(IEnumerable<int> values)
    {
        var list = values.ToList();
        return list.Count == 0
            ? 0
            : Math.Round((decimal)list.Average(), 1, MidpointRounding.AwayFromZero);
    }
}

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