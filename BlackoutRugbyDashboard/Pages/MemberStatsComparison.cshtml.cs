using BlackoutRugbyDashboard.Models;
using BlackoutRugbyDashboard.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System.Xml.Linq;
using System.Globalization;

namespace BlackoutRugbyDashboard.Pages;

public class MemberStatsComparisonModel : PageModel
{
    private readonly TeamDashboardService _dashboardService;
    private readonly DashboardDefaultsOptions _dashboardDefaults;
    private readonly ILogger<MemberStatsComparisonModel> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly DeveloperOptions _developerOptions;
    private readonly ApiLogger _apiLogger;

    public MemberStatsComparisonModel(
        TeamDashboardService dashboardService,
        ILogger<MemberStatsComparisonModel> logger,
        IOptions<DashboardDefaultsOptions> dashboardDefaults,
        IOptions<DeveloperOptions> developerOptions,
        IHttpClientFactory httpClientFactory,
        ApiLogger apiLogger)
    {
        _dashboardService = dashboardService;
        _logger = logger;
        _dashboardDefaults = dashboardDefaults.Value;
        _developerOptions = developerOptions.Value;
        _httpClientFactory = httpClientFactory;
        _apiLogger = apiLogger;
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
            var fixturesError = ReadApiError(fixturesXml);
            if (!string.IsNullOrWhiteSpace(fixturesError) && fixturesError.Contains("No fixtures found", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("No fixtures found for team {TeamId} season {Season}. Retrying without season filter.", request.TeamId, request.Season);
                _apiLogger.LogRequest("GET", $"{request.BaseEndpoint}/fixtures?teamId={request.TeamId}&last=20");
                sw.Restart();
                fixturesXml = await client.GetFixturesAsync(teamId: request.TeamId, last: 20);
                _apiLogger.LogResponse($"{request.BaseEndpoint}/fixtures?teamId={request.TeamId}&last=20", 200, fixturesXml?.Length > 3000 ? fixturesXml[..3000] + "\n... (truncated)" : fixturesXml, sw.ElapsedMilliseconds);
            }

            var fixtures = ParseFixtures(fixturesXml ?? string.Empty);

            // Get statistics for each fixture.
            foreach (var fixture in fixtures.OrderByDescending(f => f.Date))
            {
                try
                {
                    _apiLogger.LogRequest("GET", $"{request.BaseEndpoint}/fixturestats?fixtureId={fixture.Id}&teamPlayersStats={request.TeamId}");
                    sw.Restart();
                    var statsXml = await client.GetFixtureStatisticsAsync(fixtureId: fixture.Id, teamPlayersStats: request.TeamId);
                    _apiLogger.LogResponse($"{request.BaseEndpoint}/fixturestats?fixtureId={fixture.Id}&teamPlayersStats={request.TeamId}", 200, statsXml?.Length > 3000 ? statsXml[..3000] + "\n... (truncated)" : statsXml, sw.ElapsedMilliseconds);
                    var breakdown = ParseFixtureBreakdown(statsXml ?? string.Empty, fixture, request.TeamId, playerNames);
                    if (breakdown is not null)
                    {
                        fixtureBreakdowns.Add(breakdown);
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

    private List<FixtureInfo> ParseFixtures(string xml)
    {
        var fixtures = new List<FixtureInfo>();
        try
        {
            var doc = XDocument.Parse(xml);
            foreach (var fixture in doc.Descendants("fixture"))
            {
                var id = ReadInt(fixture, "id");
                var dateStr = ReadString(fixture, "date");
                var matchStartUnix = ReadString(fixture, "matchstart");
                var season = ReadIntAny(fixture, "season");
                var round = ReadIntAny(fixture, "round");
                var competition = ReadString(fixture, "competition") ?? string.Empty;

                var homeTeamId = ReadInt(fixture, "home_team_id");
                if (homeTeamId == 0)
                {
                    homeTeamId = ReadInt(fixture, "hometeamid");
                }

                var awayTeamId = ReadInt(fixture, "away_team_id");
                if (awayTeamId == 0)
                {
                    awayTeamId = ReadInt(fixture, "guestteamid");
                }

                var homeTeamName = ReadString(fixture, "home_team_name")
                    ?? ReadString(fixture, "hometeam")
                    ?? ReadString(fixture, "hometeamname")
                    ?? $"Team {homeTeamId}";
                var awayTeamName = ReadString(fixture, "away_team_name")
                    ?? ReadString(fixture, "guestteam")
                    ?? ReadString(fixture, "guestteamname")
                    ?? $"Team {awayTeamId}";

                var homeScore = ReadInt(fixture, "home_score");
                if (homeScore == 0)
                {
                    homeScore = ReadInt(fixture, "homescore");
                }

                var awayScore = ReadInt(fixture, "away_score");
                if (awayScore == 0)
                {
                    awayScore = ReadInt(fixture, "guestscore");
                }

                DateTime date;
                if (DateTime.TryParse(dateStr, out var parsedDate))
                {
                    date = parsedDate;
                }
                else if (long.TryParse(matchStartUnix, NumberStyles.Any, CultureInfo.InvariantCulture, out var unixSeconds))
                {
                    date = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).LocalDateTime;
                }
                else
                {
                    continue;
                }

                fixtures.Add(new FixtureInfo
                {
                    Id = id,
                    Season = season,
                    Round = round,
                    Competition = competition,
                    Date = date,
                    HomeTeamId = homeTeamId,
                    AwayTeamId = awayTeamId,
                    HomeTeamName = homeTeamName,
                    AwayTeamName = awayTeamName,
                    HomeScore = homeScore,
                    AwayScore = awayScore
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse fixtures XML");
        }

        return fixtures;
    }

    private FixtureBreakdown? ParseFixtureBreakdown(string xml, FixtureInfo fixture, int teamId, IReadOnlyDictionary<int, string> playerNames)
    {
        try
        {
            var doc = XDocument.Parse(xml);
            var playerStats = doc
                .Descendants()
                .Where(element => string.Equals(element.Attribute("teamid")?.Value, teamId.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
                .Where(element => element.Name.LocalName.Contains("player", StringComparison.OrdinalIgnoreCase))
                .Select(element => ParseFixturePlayerStats(element, playerNames))
                .Where(item => item is not null)
                .Cast<PlayerGameStats>()
                .OrderByDescending(item => item.TotalPoints)
                .ThenByDescending(item => item.Tackles)
                .ThenBy(item => item.Name)
                .ToList();

            if (playerStats.Count == 0)
            {
                return null;
            }

            return new FixtureBreakdown
            {
                FixtureId = fixture.Id,
                Label = fixture.BuildLabel(),
                TeamStats = BuildTeamGameStats(fixture, teamId, playerStats),
                PlayerStats = playerStats
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse fixture breakdown for fixture {FixtureId}", fixture.Id);
        }

        return null;
    }

    private static PlayerGameStats? ParseFixturePlayerStats(XElement element, IReadOnlyDictionary<int, string> playerNames)
    {
        var playerId = ReadIntAny(element, "id");
        if (playerId <= 0)
        {
            return null;
        }

        playerNames.TryGetValue(playerId, out var playerName);

        return new PlayerGameStats
        {
            PlayerId = playerId,
            Name = string.IsNullOrWhiteSpace(playerName) ? $"Player {playerId}" : playerName,
            Tackles = ReadIntAny(element, "tackles"),
            MetresGained = ReadIntAny(element, "metresgained", "metres_gained"),
            Tries = ReadIntAny(element, "tries"),
            Conversions = ReadIntAny(element, "conversions"),
            DropGoals = ReadIntAny(element, "dropgoals", "drop_goals"),
            Penalties = ReadIntAny(element, "penalties"),
            TotalPoints = ReadIntAny(element, "totalpoints", "total_points"),
            YellowCards = ReadIntAny(element, "yellowcards", "yellow_cards"),
            RedCards = ReadIntAny(element, "redcards", "red_cards"),
            Linebreaks = ReadIntAny(element, "linebreaks"),
            Intercepts = ReadIntAny(element, "intercepts"),
            Kicks = ReadIntAny(element, "kicks"),
            KnockOns = ReadIntAny(element, "knockons", "knock_ons"),
            ForwardPasses = ReadIntAny(element, "forwardpasses", "forward_passes"),
            TryAssists = ReadIntAny(element, "tryassists", "try_assists"),
            BeatenDefenders = ReadIntAny(element, "beatendefenders", "beaten_defenders"),
            Injuries = ReadIntAny(element, "injuries"),
            HandlingErrors = ReadIntAny(element, "handlingerrors", "handling_errors"),
            MissedTackles = ReadIntAny(element, "missedtackles", "missed_tackles"),
            Fights = ReadIntAny(element, "fights"),
            KickingMetres = ReadIntAny(element, "kickingmetres", "kicking_metres"),
            PenaltiesConceded = ReadIntAny(element, "penaltiesconceded", "penalties_conceded"),
            KicksOutOnTheFull = ReadIntAny(element, "kicksoutonthefull", "kicks_out_on_the_full"),
            LineoutsWon = ReadIntAny(element, "lineoutswon", "lineouts_won"),
            LineoutsLost = ReadIntAny(element, "lineoutslost", "lineouts_lost"),
            ScrumWins = ReadIntAny(element, "scrumswon", "scrums_won"),
            ScrumLosses = ReadIntAny(element, "scrumslost", "scrums_lost")
        };
    }

    private static GameStats BuildTeamGameStats(FixtureInfo fixture, int teamId, IReadOnlyCollection<PlayerGameStats> playerStats)
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

    private static int ReadInt(XElement element, string name)
    {
        var value = element.Element(name)?.Value;
        return int.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result) ? result : 0;
    }

    private static int ReadIntAny(XElement element, params string[] names)
    {
        foreach (var name in names)
        {
            var value = element.Element(name)?.Value;
            if (int.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
            {
                return result;
            }
        }

        return 0;
    }

    private static string? ReadString(XElement element, string name)
    {
        return element.Element(name)?.Value;
    }

    private static string? ReadApiError(string? xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return null;
        }

        try
        {
            var doc = XDocument.Parse(xml);
            return doc.Descendants("error").FirstOrDefault()?.Value;
        }
        catch
        {
            return null;
        }
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

public class FixtureInfo
{
    public int Id { get; set; }
    public int Season { get; set; }
    public int Round { get; set; }
    public string Competition { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public int HomeTeamId { get; set; }
    public int AwayTeamId { get; set; }
    public string HomeTeamName { get; set; } = string.Empty;
    public string AwayTeamName { get; set; } = string.Empty;
    public int HomeScore { get; set; }
    public int AwayScore { get; set; }

    public string BuildLabel()
    {
        var seasonLabel = Season > 0 ? $"Season {Season}" : "Season";
        var roundLabel = Round > 0 ? $"Week {Round}" : "Fixture";
        var competitionLabel = string.IsNullOrWhiteSpace(Competition) ? string.Empty : $" {Competition}";
        return $"{seasonLabel}, {roundLabel}{competitionLabel} - {Date:MMM d}";
    }
}

public class FixtureBreakdown
{
    public int FixtureId { get; set; }
    public string Label { get; set; } = string.Empty;
    public GameStats TeamStats { get; set; } = new();
    public List<PlayerGameStats> PlayerStats { get; set; } = new();
}

public class PlayerGameStats
{
    public int PlayerId { get; set; }
    public string Name { get; set; } = string.Empty;
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
