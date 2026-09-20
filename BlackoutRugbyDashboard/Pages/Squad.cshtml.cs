using System.Net.Http;
using System.Net.Sockets;
using BlackoutRugbyDashboard.Data;
using BlackoutRugbyDashboard.Models;
using BlackoutRugbyDashboard.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace BlackoutRugbyDashboard.Pages;

/// <summary>
/// The Squad page (cache-first, D-Squad): "Load stats comparison" is a
/// write-through Match Cache load — one live fixtures discovery read fills the
/// D1 entry scope for fixtures missing from the cache (FillFixtureAsync's
/// scope), then every completed fixture in the window renders from cached rows.
/// The roster read stays live (2 calls — CSR/form/energy/age are the volatile
/// present) and each successful load saves a SnapshotStore point-in-time
/// snapshot plus renders its comparison with the previous load. Season
/// aggregates (ps) cache point-in-time in PlayerSeasons and refresh only when
/// the window reveals a newer completed fixture. Live-read failures degrade to
/// cache where the data is immutable (cached window, cached season stats);
/// credential/API rejections take D3's decision-2 hard-error panel; transport
/// failures replay the last roster snapshot with a warning banner.
/// </summary>
public class SquadModel : ClubLinkedPageModel
{
    private const int SquadWindowLast = 20;

    private readonly MatchCacheService _cache;
    private readonly SnapshotStore _snapshots;
    private readonly IBlackoutRugbyApiClient _apiClient;
    private readonly BlackoutRugbyResponseAdapter _adapter;
    private readonly ApiLogger _apiLogger;
    private readonly DashboardDefaultsOptions _dashboardDefaults;
    private readonly ILogger<SquadModel> _logger;

    public SquadModel(
        MatchCacheService cache,
        SnapshotStore snapshots,
        IBlackoutRugbyApiClient apiClient,
        BlackoutRugbyResponseAdapter adapter,
        ApiLogger apiLogger,
        IOptions<DashboardDefaultsOptions> dashboardDefaults,
        ClubLinkService clubLinks,
        ILogger<SquadModel> logger) : base(clubLinks)
    {
        _cache = cache;
        _snapshots = snapshots;
        _apiClient = apiClient;
        _adapter = adapter;
        _apiLogger = apiLogger;
        _dashboardDefaults = dashboardDefaults.Value;
        _logger = logger;
        Input = CreateRequestFromDefaults();
    }

    [BindProperty]
    public TeamDashboardRequest Input { get; set; }

    public TeamDashboardViewModel? Dashboard { get; private set; }
    public List<GameStats> GameStatsList { get; private set; } = new();
    public List<FixtureBreakdown> FixtureBreakdowns { get; private set; } = new();
    public string? StatusMessage { get; private set; }
    public string? WarningMessage { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string ApiLogsJson { get; private set; } = "[]";
    public TeamSnapshotComparison? SnapshotComparison { get; private set; }
    public bool SnapshotSaved { get; private set; }

    public void OnGet()
    {
        Input = CreateRequestFromDefaults();
    }

    public async Task<IActionResult> OnPostLoadAsync()
    {
        ApplyDefaultsIfMissing();
        ApplyLinkedMemberCredentials();
        ModelState.Clear();

        if (!TryValidateModel(Input, nameof(Input)))
        {
            return Page();
        }

        _apiLogger.Clear();
        StatusMessage = null;
        WarningMessage = null;
        ErrorMessage = null;
        SnapshotComparison = null;
        SnapshotSaved = false;
        Dashboard = null;
        GameStatsList = new List<GameStats>();
        FixtureBreakdowns = new List<FixtureBreakdown>();

        try
        {
            var warnings = new List<string>();

            var window = await LoadWindowAsync(warnings);
            var roster = await LoadRosterAsync(warnings);
            var seasonStats = await LoadSeasonStatsAsync(roster, window, warnings);

            Dashboard = BuildDashboardViewModel(roster, seasonStats.Stats);
            FixtureBreakdowns = BuildFixtureBreakdowns(window);
            GameStatsList = FixtureBreakdowns
                .Select(item => item.TeamStats)
                .OrderByDescending(item => item.Date)
                .ToList();

            if (!roster.Degraded && Dashboard.Players.Count > 0)
            {
                await _snapshots.SaveSnapshotAsync(Dashboard);
                SnapshotSaved = true;
                SnapshotComparison = await _snapshots.GetLatestComparisonAsync(Input.TeamId);
            }

            StatusMessage = BuildStatusMessage(window, seasonStats, roster);
            if (warnings.Count > 0)
            {
                WarningMessage = string.Join(" ", warnings);
            }
        }
        catch (SquadHardError hardError)
        {
            _logger.LogWarning("Squad load stopped with a hard error: {Message}", hardError.Message);
            ErrorMessage = hardError.Message;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to load squad dashboard for team {TeamId}", Input.TeamId);
            ErrorMessage = $"The Squad load failed unexpectedly: {exception.Message}";
        }

        ApiLogsJson = _apiLogger.GetLogsJson();
        return Page();
    }
    /// <summary>D3: the Member Key rides the Club Link, never the form.</summary>
    private void ApplyLinkedMemberCredentials()
    {
        var credentials = ClubLinks.ResolveCurrentMemberCredentials();
        if (credentials is null)
        {
            return;
        }

        Input.MemberId = credentials.MemberId;
        Input.MemberKey = credentials.MemberKey;
    }

    /// <summary>
    /// The window phase: live discovery read + write-through fill. Transport
    /// failure degrades to the cached window (completed fixtures are immutable
    /// — the cached rows are the same fact live would have returned); an empty
    /// cache plus a failed read is a hard error.
    /// </summary>
    private async Task<SquadWindow> LoadWindowAsync(ICollection<string> warnings)
    {
        try
        {
            var fill = await _cache.FillSquadWindowAsync(Input.TeamId, Input.Season, SquadWindowLast, Input.BaseEndpoint, _apiLogger);
            if (fill.Failed > 0)
            {
                warnings.Add($"{fill.Failed} fixture fill attempt(s) failed — they retry on your next load.");
            }

            var rows = await _cache.GetSquadWindowRowsAsync(fill.CompletedRows.Select(row => row.FixtureId).ToList(), Input.TeamId);
            var newestCompletedUtc = fill.CompletedRows.Count == 0
                ? (DateTime?)null
                : DateTimeOffset.FromUnixTimeSeconds(fill.CompletedRows.Max(row => row.MatchFinishUnix)).UtcDateTime;

            foreach (var entry in rows.Values)
            {
                if (entry.Players.Count > 0)
                {
                    _apiLogger.LogCache(
                        $"match-cache/fixturestats?fixtureId={entry.Fixture.FixtureId}",
                        $"{entry.Players.Count} player rows + team stats rendered from cache");
                }
            }

            return new SquadWindow(
                fill.Fixtures,
                OrderRows(rows.Values),
                newestCompletedUtc,
                fill.Completed,
                fill.NewlyCached,
                fill.AlreadyCached,
                fill.Failed,
                Degraded: false,
                new Dictionary<int, string>());
        }
        catch (Exception exception) when (IsTransportFailure(exception))
        {
            _logger.LogWarning(exception, "Fixtures read failed; degrading to the cached window for team {TeamId}", Input.TeamId);

            var cachedRows = await _cache.GetCachedWindowAsync(Input.TeamId, Input.Season, SquadWindowLast);
            if (cachedRows.Count == 0)
            {
                cachedRows = await _cache.GetCachedWindowAsync(Input.TeamId, 0, SquadWindowLast);
            }

            if (cachedRows.Count == 0)
            {
                throw new SquadHardError(
                    $"The live fixtures read failed ({exception.Message}) and the Match Cache holds no completed fixtures yet — there is nothing to display. Try again once the game API is reachable.");
            }

            warnings.Add("The live fixtures read failed — showing the Match Cache's stored window instead.");
            var rows = await _cache.GetSquadWindowRowsAsync(cachedRows.Select(row => row.FixtureId).ToList(), Input.TeamId);
            var teamNames = await _cache.GetTeamNamesAsync(cachedRows.SelectMany(row => new[] { row.HomeTeamId, row.GuestTeamId }));
            var newestCompletedUtc = DateTimeOffset.FromUnixTimeSeconds(cachedRows.Max(row => row.MatchFinishUnix)).UtcDateTime;

            return new SquadWindow(
                Array.Empty<Fixture>(),
                OrderRows(rows.Values),
                newestCompletedUtc,
                cachedRows.Count,
                0,
                cachedRows.Count,
                0,
                Degraded: true,
                teamNames);
        }
    }

    private static IReadOnlyList<SquadFixtureRows> OrderRows(IEnumerable<SquadFixtureRows> rows) =>
        rows.OrderByDescending(entry => entry.Fixture.MatchStartUnix).ToList();
    /// <summary>
    /// The roster phase: always live (CSR/form/energy/age are the volatile
    /// present, never replayed as such). Credential/API rejections take D3's
    /// hard-error contract; transport failures replay the last snapshot with a
    /// warning — and with no snapshot on record, that is a hard error too.
    /// </summary>
    private async Task<SquadRoster> LoadRosterAsync(ICollection<string> warnings)
    {
        try
        {
            var teamsXml = await ReadLiveAsync(
                () => _apiClient.GetTeamsAsync(teamId: Input.TeamId),
                $"{Input.BaseEndpoint}/teams?teamId={Input.TeamId}");
            var playersXml = await ReadLiveAsync(
                () => _apiClient.GetPlayersAsync(teamId: Input.TeamId),
                $"{Input.BaseEndpoint}/players?teamId={Input.TeamId}");

            var apiError = _adapter.ExtractResponseError(teamsXml) ?? _adapter.ExtractResponseError(playersXml);
            if (!string.IsNullOrWhiteSpace(apiError))
            {
                throw new SquadHardError(ClassifyApiError(apiError));
            }

            var team = _adapter.ParseTeam(teamsXml);
            return new SquadRoster(
                team?.Name,
                team?.CountryIso ?? string.Empty,
                _adapter.ParsePlayers(playersXml),
                Degraded: false,
                null);
        }
        catch (SquadHardError)
        {
            throw;
        }
        catch (Exception exception) when (IsTransportFailure(exception))
        {
            _logger.LogWarning(exception, "Roster read failed; replaying the last snapshot for team {TeamId}", Input.TeamId);
            var snapshot = await _snapshots.GetLatestAsync(Input.TeamId);
            if (snapshot is null || snapshot.Players.Count == 0)
            {
                throw new SquadHardError(
                    $"The live roster read failed ({exception.Message}) and no previous squad snapshot exists to fall back on. Try again once the game API is reachable.");
            }

            warnings.Add(
                $"The live roster read failed — showing the squad as captured {snapshot.CapturedAtUtc.ToLocalTime():MMM d, HH:mm} (age and recent pops unavailable).");
            return new SquadRoster(
                snapshot.TeamName,
                string.Empty,
                snapshot.Players
                    .Select(player => new Player(
                        player.Id,
                        player.Name,
                        0,
                        player.Csr,
                        player.Salary,
                        player.Form,
                        player.Energy,
                        Array.Empty<string>()))
                    .ToList(),
                Degraded: true,
                snapshot);
        }
    }

    /// <summary>One live read with the page's request/response logging shape.</summary>
    private async Task<string> ReadLiveAsync(Func<Task<string>> read, string url)
    {
        _apiLogger.LogRequest("GET", url);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var xml = await read();
        _apiLogger.LogResponse(url, 200, Truncate(xml), sw.ElapsedMilliseconds);
        return xml;
    }

    /// <summary>
    /// The season-stats phase: cache with event-driven refresh. Degraded loads
    /// read the cache only — no ps refresh while the live API is down.
    /// </summary>
    private async Task<SeasonStatsOutcome> LoadSeasonStatsAsync(SquadRoster roster, SquadWindow window, ICollection<string> warnings)
    {
        var playerIds = roster.Players.Select(player => player.Id).ToList();
        if (playerIds.Count == 0)
        {
            return new SeasonStatsOutcome(new Dictionary<int, PlayerStatistics>(), 0, 0, 0, 0);
        }

        if (roster.Degraded)
        {
            var cached = await _cache.GetCachedPlayerSeasonsAsync(playerIds, Input.Season);
            var missing = playerIds.Count - cached.Count;
            if (missing > 0)
            {
                warnings.Add($"{missing} player season read(s) are not cached yet — those stat columns read zero.");
            }

            _apiLogger.LogCache(
                "match-cache/playerstatistics",
                $"{cached.Count}/{playerIds.Count} season reads served from cache (degraded load — no refresh)");
            return new SeasonStatsOutcome(cached, playerIds.Count, cached.Count, 0, 0);
        }

        var result = await _cache.GetOrRefreshPlayerSeasonsAsync(playerIds, Input.Season, window.NewestCompletedUtc, Input.BaseEndpoint, _apiLogger);
        _apiLogger.LogCache(
            "match-cache/playerstatistics",
            $"{result.Stats.Count}/{playerIds.Count} season reads served from cache ({result.Refreshed} refreshed live this load)");
        if (result.Failed > 0)
        {
            warnings.Add(
                $"{result.Failed} player season read(s) failed ({result.FirstError}) — cached values were kept where available.");
        }

        return new SeasonStatsOutcome(result.Stats, playerIds.Count, result.Served, result.Refreshed, result.Failed);
    }
    private TeamDashboardViewModel BuildDashboardViewModel(SquadRoster roster, IReadOnlyDictionary<int, PlayerStatistics> stats)
    {
        var players = roster.Players;
        if (players.Count == 0)
        {
            return new TeamDashboardViewModel
            {
                TeamId = Input.TeamId,
                TeamName = roster.TeamName ?? $"Team {Input.TeamId}",
                CountryIso = roster.CountryIso
            };
        }

        var dashboardPlayers = players
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
                    Tackles = season?.Tackles ?? 0,
                    MetresGained = season?.MetresGained ?? 0,
                    Tries = season?.Tries ?? 0,
                    Conversions = season?.Conversions ?? 0,
                    DropGoals = season?.DropGoals ?? 0,
                    Penalties = season?.Penalties ?? 0,
                    TotalPoints = season?.TotalPoints ?? 0,
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
                    TotalCaps = season?.TotalCaps ?? 0,
                    LeagueCaps = season?.LeagueCaps ?? 0,
                    FriendlyCaps = season?.FriendlyCaps ?? 0,
                    CupCaps = season?.CupCaps ?? 0,
                    UnderTwentyCaps = season?.UnderTwentyCaps ?? 0,
                    NationalCaps = season?.NationalCaps ?? 0,
                    WorldCupCaps = season?.WorldCupCaps ?? 0,
                    UnderTwentyWorldCupCaps = season?.UnderTwentyWorldCupCaps ?? 0,
                    OtherCaps = season?.OtherCaps ?? 0,
                    RecentPops = player.RecentPops
                };
            })
            .OrderByDescending(player => player.TotalPoints)
            .ThenByDescending(player => player.Tackles)
            .ToList();

        return new TeamDashboardViewModel
        {
            TeamId = Input.TeamId,
            TeamName = roster.TeamName ?? $"Team {Input.TeamId}",
            CountryIso = roster.CountryIso,
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
    private List<FixtureBreakdown> BuildFixtureBreakdowns(SquadWindow window)
    {
        var breakdowns = new List<FixtureBreakdown>();
        var playerNames = (Dashboard?.Players ?? Array.Empty<PlayerDashboardItem>())
            .GroupBy(player => player.Id)
            .ToDictionary(group => group.Key, group => group.First().Name);

        foreach (var entry in window.Rows)
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
                TeamStats = BuildGameStats(window, entry, playerStats),
                PlayerStats = playerStats
            });
        }

        return breakdowns;
    }

    /// <summary>
    /// Builds one fixture's GameStats from the cached rows. Normal loads take
    /// names/scores from the discovery read's parsed fixtures; degraded loads
    /// fall back to TeamFacts names and the cached summary's points.
    /// </summary>
    private GameStats BuildGameStats(SquadWindow window, SquadFixtureRows entry, IReadOnlyList<FixturePlayerStatistics> playerStats)
    {
        var row = entry.Fixture;
        var isHome = row.HomeTeamId == Input.TeamId;
        var opponentId = isHome ? row.GuestTeamId : row.HomeTeamId;
        var display = window.DisplayFixture(row.FixtureId);

        var opponent = display is not null
            ? (isHome ? display.AwayTeamName : display.HomeTeamName)
            : window.TeamNames.TryGetValue(opponentId, out var cachedName) && !string.IsNullOrWhiteSpace(cachedName)
                ? cachedName
                : $"Team {opponentId}";

        var score = display is not null
            ? (isHome ? display.HomeScore : display.AwayScore)
            : entry.Summary is null ? 0 : (isHome ? entry.Summary.HomePoints : entry.Summary.GuestPoints);
        var oppositionScore = display is not null
            ? (isHome ? display.AwayScore : display.HomeScore)
            : entry.Summary is null ? 0 : (isHome ? entry.Summary.GuestPoints : entry.Summary.HomePoints);

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
            Result = score > oppositionScore ? "W" : score < oppositionScore ? "L" : "D",
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

    private static DateTime GameDate(FixtureRow row) =>
        DateTimeOffset.FromUnixTimeSeconds(row.MatchStartUnix).LocalDateTime;

    private static string BuildFixtureLabel(int season, int round, string competition, DateTime date)
    {
        var seasonLabel = season > 0 ? $"Season {season}" : "Season";
        var roundLabel = round > 0 ? $"Week {round}" : "Fixture";
        var competitionLabel = string.IsNullOrWhiteSpace(competition) ? string.Empty : $" {competition}";
        return $"{seasonLabel}, {roundLabel}{competitionLabel} - {date:MMM d}";
    }
    private string BuildStatusMessage(SquadWindow window, SeasonStatsOutcome seasonStats, SquadRoster roster)
    {
        var windowPart = window.Degraded
            ? $"{window.Completed} fixtures from the cached window (live read failed)"
            : $"{window.Completed} fixtures in window: {window.AlreadyCached} from cache, {window.NewlyCached} fetched live"
              + (window.Failed > 0 ? $", {window.Failed} fill attempts failed" : string.Empty);
        var seasonPart = seasonStats.Refreshed > 0
            ? $"{seasonStats.Stats.Count}/{seasonStats.Requested} season reads cached ({seasonStats.Refreshed} refreshed)"
            : $"{seasonStats.Stats.Count}/{seasonStats.Requested} season reads cached";
        var rosterPart = roster.Degraded ? "roster replayed from the last snapshot" : "roster live";
        return $"Squad loaded — {windowPart} · {seasonPart} · {rosterPart}";
    }

    /// <summary>D3's failure-class rule, page-flavored: the exact upstream error
    /// strings are not live-verified, so classification keys on the error text
    /// with a defensive fallback (see ClubLinkService for the prior art).</summary>
    private static string ClassifyApiError(string errorText)
    {
        var text = errorText.Trim();
        var normalized = text.ToLowerInvariant();
        if (normalized.Contains("no data requested"))
        {
            return "The game API answered 'No data requested' — a recorded transient state. Wait a moment and load again.";
        }
        if (normalized.Contains("key") || normalized.Contains("credential") || normalized.Contains("password"))
        {
            return $"The API rejected your Member Key ({text}). It changes whenever the in-game password changes — re-link from Settings, then load again.";
        }
        if (normalized.Contains("member") || normalized.Contains("user"))
        {
            return $"The API rejected the member credentials ({text}). Re-link from Settings if your Member ID changed.";
        }
        return $"The API rejected the read ({text}). If your Member Key changed, re-link from Settings.";
    }

    /// <summary>Connectivity-class failures degrade; anything else is a hard error.</summary>
    private static bool IsTransportFailure(Exception exception) =>
        exception is HttpRequestException or TaskCanceledException or SocketException;

    private static string? Truncate(string? value) =>
        value?.Length > 3000 ? value[..3000] + "\n... (truncated)" : value;

    public static string FormatDelta(int value) =>
        value > 0 ? $"+{value}" : value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static decimal RoundAverage(IEnumerable<int> values)
    {
        var list = values.ToList();
        return list.Count == 0
            ? 0
            : Math.Round((decimal)list.Average(), 1, MidpointRounding.AwayFromZero);
    }
    private TeamDashboardRequest CreateRequestFromDefaults()
    {
        var link = ClubLinks.GetLinkState();
        return new TeamDashboardRequest
        {
            BaseEndpoint = _dashboardDefaults.BaseEndpoint,
            TeamId = link?.TeamId ?? 0,
            Season = 80
        };
    }

    private void ApplyDefaultsIfMissing()
    {
        if (string.IsNullOrEmpty(Input.BaseEndpoint))
        {
            Input.BaseEndpoint = _dashboardDefaults.BaseEndpoint;
        }
        if (Input.TeamId == 0)
        {
            Input.TeamId = ClubLinks.GetLinkState()?.TeamId ?? 0;
        }
    }

    /// <summary>The classified hard error: D3's decision-2 panel owns the recovery.</summary>
    private sealed class SquadHardError(string message) : Exception(message);

    private sealed record SquadWindow(
        IReadOnlyList<Fixture> Fixtures,
        IReadOnlyList<SquadFixtureRows> Rows,
        DateTime? NewestCompletedUtc,
        int Completed,
        int NewlyCached,
        int AlreadyCached,
        int Failed,
        bool Degraded,
        IReadOnlyDictionary<int, string> TeamNames)
    {
        public Fixture? DisplayFixture(int fixtureId) =>
            Fixtures.FirstOrDefault(fixture => fixture.Id == fixtureId);
    }

    private sealed record SquadRoster(
        string? TeamName,
        string CountryIso,
        IReadOnlyList<Player> Players,
        bool Degraded,
        TeamSnapshot? Snapshot);

    private sealed record SeasonStatsOutcome(
        IReadOnlyDictionary<int, PlayerStatistics> Stats,
        int Requested,
        int Served,
        int Refreshed,
        int Failed);
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