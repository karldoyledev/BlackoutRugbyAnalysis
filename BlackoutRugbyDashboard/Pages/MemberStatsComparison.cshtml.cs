using BlackoutRugbyDashboard.Models;
using BlackoutRugbyDashboard.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace BlackoutRugbyDashboard.Pages;

public class MemberStatsComparisonModel : PageModel
{
    private readonly TeamDashboardService _dashboardService;
    private readonly DashboardDefaultsOptions _dashboardDefaults;
    private readonly ILogger<MemberStatsComparisonModel> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly DeveloperOptions _developerOptions;
    private readonly ApiLogger _apiLogger;
    private readonly BlackoutRugbyResponseAdapter _responseAdapter;

    public MemberStatsComparisonModel(
        TeamDashboardService dashboardService,
        ILogger<MemberStatsComparisonModel> logger,
        IOptions<DashboardDefaultsOptions> dashboardDefaults,
        IOptions<DeveloperOptions> developerOptions,
        IHttpClientFactory httpClientFactory,
        ApiLogger apiLogger,
        BlackoutRugbyResponseAdapter responseAdapter)
    {
        _dashboardService = dashboardService;
        _logger = logger;
        _dashboardDefaults = dashboardDefaults.Value;
        _developerOptions = developerOptions.Value;
        _httpClientFactory = httpClientFactory;
        _apiLogger = apiLogger;
        _responseAdapter = responseAdapter;
        Input = CreateRequestFromDefaults();
    }

    [BindProperty]
    public TeamDashboardRequest Input { get; set; }

    public TeamDashboardViewModel? Dashboard { get; private set; }
    public List<GameStats> GameStatsList { get; private set; } = new();
    public List<FixtureBreakdown> FixtureBreakdowns { get; private set; } = new();
    public string? StatusMessage { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string ApiLogsJson { get; private set; } = "[]";

    public void OnGet()
    {
        Input = CreateRequestFromDefaults();
    }

    public async Task<IActionResult> OnPostLoadAsync()
    {
        ApplyDefaultsIfMissing();
        ModelState.Clear();

        if (!TryValidateModel(Input, nameof(Input)))
        {
            return Page();
        }

        try
        {
            _apiLogger.Clear();
            Dashboard = await _dashboardService.BuildDashboardAsync(Input, _apiLogger);
            StatusMessage = "Live dashboard loaded.";

            FixtureBreakdowns = await LoadFixtureBreakdownsAsync(Input, Dashboard.Players);
            GameStatsList = FixtureBreakdowns
                .Select(item => item.TeamStats)
                .OrderByDescending(item => item.Date)
                .ToList();
            ApiLogsJson = _apiLogger.GetLogsJson();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to load dashboard for team {TeamId}", Input.TeamId);
            ErrorMessage = exception.Message;
            ApiLogsJson = _apiLogger.GetLogsJson();
        }

        return Page();
    }

    private async Task<List<FixtureBreakdown>> LoadFixtureBreakdownsAsync(TeamDashboardRequest request, IReadOnlyList<PlayerDashboardItem> players)
    {
        var fixtureBreakdowns = new List<FixtureBreakdown>();
        var playerNames = players
            .GroupBy(player => player.Id)
            .ToDictionary(group => group.Key, group => group.First().Name);
        
        try
        {
            var credentials = new BlackoutRugby.Api.BlackoutRugbyApiCredentials(null, null)
            {
                DeveloperId = _developerOptions.DeveloperId,
                DeveloperKey = _developerOptions.DeveloperKey,
                DeveloperIV = _developerOptions.DeveloperIV
            };

            var client = new BlackoutRugby.Api.BlackoutRugbyApiClient(_httpClientFactory.CreateClient(), request.BaseEndpoint, credentials);
            
            // Get the most recent fixtures for the team in the selected season.
            _apiLogger.LogRequest("GET", $"{request.BaseEndpoint}/fixtures?teamId={request.TeamId}&last=20&season={request.Season}");
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var fixturesXml = await client.GetFixturesAsync(teamId: request.TeamId, last: 20, season: request.Season);
            _apiLogger.LogResponse($"{request.BaseEndpoint}/fixtures?teamId={request.TeamId}&last=20&season={request.Season}", 200, fixturesXml?.Length > 3000 ? fixturesXml[..3000] + "\n... (truncated)" : fixturesXml, sw.ElapsedMilliseconds);

            // Fallback: if no fixtures are found for the selected season, retry without season filter.
            var fixturesError = _responseAdapter.ExtractResponseError(fixturesXml);
            if (!string.IsNullOrWhiteSpace(fixturesError) && fixturesError.Contains("No fixtures found", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("No fixtures found for team {TeamId} season {Season}. Retrying without season filter.", request.TeamId, request.Season);
                _apiLogger.LogRequest("GET", $"{request.BaseEndpoint}/fixtures?teamId={request.TeamId}&last=20");
                sw.Restart();
                fixturesXml = await client.GetFixturesAsync(teamId: request.TeamId, last: 20);
                _apiLogger.LogResponse($"{request.BaseEndpoint}/fixtures?teamId={request.TeamId}&last=20", 200, fixturesXml?.Length > 3000 ? fixturesXml[..3000] + "\n... (truncated)" : fixturesXml, sw.ElapsedMilliseconds);
            }

            var fixtures = _responseAdapter.ParseFixtures(fixturesXml ?? string.Empty);

            // Get statistics for each fixture.
            foreach (var fixture in fixtures.OrderByDescending(f => f.Date))
            {
                try
                {
                    _apiLogger.LogRequest("GET", $"{request.BaseEndpoint}/fixturestats?fixtureId={fixture.Id}&teamPlayersStats={request.TeamId}");
                    sw.Restart();
                    var statsXml = await client.GetFixtureStatisticsAsync(fixtureId: fixture.Id, teamPlayersStats: request.TeamId);
                    _apiLogger.LogResponse($"{request.BaseEndpoint}/fixturestats?fixtureId={fixture.Id}&teamPlayersStats={request.TeamId}", 200, statsXml?.Length > 3000 ? statsXml[..3000] + "\n... (truncated)" : statsXml, sw.ElapsedMilliseconds);
                    var playerStats = _responseAdapter.ParseFixturePlayerStatistics(statsXml ?? string.Empty, request.TeamId, playerNames);
                    if (playerStats.Count > 0)
                    {
                        fixtureBreakdowns.Add(BuildFixtureBreakdown(fixture, request.TeamId, playerStats));
                    }
                }
                catch (Exception ex)
                {
                    _apiLogger.LogError($"{request.BaseEndpoint}/fixturestats", ex.Message, sw.ElapsedMilliseconds);
                    _logger.LogWarning(ex, "Failed to load stats for fixture {FixtureId}", fixture.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _apiLogger.LogError($"{request.BaseEndpoint}/fixtures", ex.Message);
            _logger.LogWarning(ex, "Failed to load fixture breakdowns");
        }

        return fixtureBreakdowns;
    }

    private static FixtureBreakdown BuildFixtureBreakdown(Fixture fixture, int teamId, IReadOnlyList<FixturePlayerStatistics> playerStats)
    {
        return new FixtureBreakdown
        {
            FixtureId = fixture.Id,
            Label = BuildFixtureLabel(fixture),
            TeamStats = BuildTeamGameStats(fixture, teamId, playerStats),
            PlayerStats = playerStats
        };
    }

    private static string BuildFixtureLabel(Fixture fixture)
    {
        var seasonLabel = fixture.Season > 0 ? $"Season {fixture.Season}" : "Season";
        var roundLabel = fixture.Round > 0 ? $"Week {fixture.Round}" : "Fixture";
        var competitionLabel = string.IsNullOrWhiteSpace(fixture.Competition) ? string.Empty : $" {fixture.Competition}";
        return $"{seasonLabel}, {roundLabel}{competitionLabel} - {fixture.Date:MMM d}";
    }

    private static GameStats BuildTeamGameStats(Fixture fixture, int teamId, IReadOnlyCollection<FixturePlayerStatistics> playerStats)
    {
        var isHome = fixture.HomeTeamId == teamId;
        var opponent = isHome ? fixture.AwayTeamName : fixture.HomeTeamName;
        if (string.IsNullOrWhiteSpace(opponent) || opponent == "Unknown")
        {
            var opponentId = isHome ? fixture.AwayTeamId : fixture.HomeTeamId;
            opponent = opponentId > 0 ? $"Team {opponentId}" : "Opponent";
        }

        var score = isHome ? fixture.HomeScore : fixture.AwayScore;
        var oppositionScore = isHome ? fixture.AwayScore : fixture.HomeScore;

        return new GameStats
        {
            FixtureId = fixture.Id,
            Season = fixture.Season,
            Round = fixture.Round,
            Competition = fixture.Competition,
            Date = fixture.Date,
            Opponent = opponent,
            Score = score,
            OppositionScore = oppositionScore,
            Result = score > oppositionScore ? "W" : (score < oppositionScore ? "L" : "D"),
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

    private TeamDashboardRequest CreateRequestFromDefaults()
    {
        return new TeamDashboardRequest
        {
            BaseEndpoint = _dashboardDefaults.BaseEndpoint,
            TeamId = _dashboardDefaults.TeamId,
            MemberId = _dashboardDefaults.MemberId,
            MemberKey = _dashboardDefaults.MemberKey,
            Season = 80
        };
    }

    private void ApplyDefaultsIfMissing()
    {
        if (string.IsNullOrEmpty(Input.BaseEndpoint))
            Input.BaseEndpoint = _dashboardDefaults.BaseEndpoint;
        if (Input.TeamId == 0)
            Input.TeamId = _dashboardDefaults.TeamId;
    }
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
