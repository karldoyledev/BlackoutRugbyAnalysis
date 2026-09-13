using BlackoutRugby.Api;
using BlackoutRugbyDashboard.Models;
using Microsoft.Extensions.Options;

namespace BlackoutRugbyDashboard.Services;

public class TeamDashboardService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TeamDashboardService> _logger;
    private readonly DeveloperOptions _developerOptions;
    private readonly BlackoutRugbyResponseAdapter _responseAdapter;

    public TeamDashboardService(
        IHttpClientFactory httpClientFactory,
        ILogger<TeamDashboardService> logger,
        IOptions<DeveloperOptions> developerOptions,
        BlackoutRugbyResponseAdapter responseAdapter)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _developerOptions = developerOptions.Value;
        _responseAdapter = responseAdapter;
    }

    public async Task<TeamDashboardViewModel> BuildDashboardAsync(TeamDashboardRequest request, ApiLogger? apiLogger = null)
    {
        var credentials = request.HasCredentials
            ? new BlackoutRugbyApiCredentials(request.MemberId, request.MemberKey)
            : null;

        if (credentials != null)
        {
            credentials.DeveloperId = _developerOptions.DeveloperId;
            credentials.DeveloperKey = _developerOptions.DeveloperKey;
            credentials.DeveloperIV = _developerOptions.DeveloperIV;
        }
        else
        {
            credentials = new BlackoutRugbyApiCredentials(null, null)
            {
                DeveloperId = _developerOptions.DeveloperId,
                DeveloperKey = _developerOptions.DeveloperKey,
                DeveloperIV = _developerOptions.DeveloperIV
            };
        }

        var client = new BlackoutRugbyApiClient(_httpClientFactory.CreateClient(), request.BaseEndpoint, credentials);
        
        // Log API calls
        var sw = System.Diagnostics.Stopwatch.StartNew();
        apiLogger?.LogRequest("GET", $"{request.BaseEndpoint}/teams?teamId={request.TeamId}");
        var teamTask = client.GetTeamsAsync(teamId: request.TeamId);
        
        apiLogger?.LogRequest("GET", $"{request.BaseEndpoint}/players?teamId={request.TeamId}");
        var playersXml = await client.GetPlayersAsync(teamId: request.TeamId).ConfigureAwait(false);
        apiLogger?.LogResponse($"{request.BaseEndpoint}/players?teamId={request.TeamId}", 200, playersXml?.Length > 3000 ? playersXml[..3000] + "\n... (truncated)" : playersXml, sw.ElapsedMilliseconds);
        
        var players = _responseAdapter.ParsePlayers(playersXml);

        if (players.Count == 0)
        {
            var team = _responseAdapter.ParseTeam(await teamTask.ConfigureAwait(false));
            return new TeamDashboardViewModel
            {
                TeamId = request.TeamId,
                TeamName = team?.Name ?? $"Team {request.TeamId}",
                CountryIso = team?.CountryIso ?? string.Empty
            };
        }

        var statsByPlayerId = await LoadPlayerStatisticsAsync(client, players.Select(player => player.Id), request.Season).ConfigureAwait(false);
        var teamInfo = _responseAdapter.ParseTeam(await teamTask.ConfigureAwait(false));

        var dashboardPlayers = players
            .Select(player =>
            {
                statsByPlayerId.TryGetValue(player.Id, out var stats);

                return new PlayerDashboardItem
                {
                    Id = player.Id,
                    Name = player.Name,
                    Age = player.Age,
                    Csr = player.Csr,
                    Salary = player.Salary,
                    Form = player.Form,
                    Energy = player.Energy,
                    Tackles = stats?.Tackles ?? 0,
                    MetresGained = stats?.MetresGained ?? 0,
                    Tries = stats?.Tries ?? 0,
                    Conversions = stats?.Conversions ?? 0,
                    DropGoals = stats?.DropGoals ?? 0,
                    Penalties = stats?.Penalties ?? 0,
                    TotalPoints = stats?.TotalPoints ?? 0,
                    YellowCards = stats?.YellowCards ?? 0,
                    RedCards = stats?.RedCards ?? 0,
                    Linebreaks = stats?.Linebreaks ?? 0,
                    Intercepts = stats?.Intercepts ?? 0,
                    Kicks = stats?.Kicks ?? 0,
                    KnockOns = stats?.KnockOns ?? 0,
                    ForwardPasses = stats?.ForwardPasses ?? 0,
                    TryAssists = stats?.TryAssists ?? 0,
                    BeatenDefenders = stats?.BeatenDefenders ?? 0,
                    Injuries = stats?.Injuries ?? 0,
                    HandlingErrors = stats?.HandlingErrors ?? 0,
                    MissedTackles = stats?.MissedTackles ?? 0,
                    Fights = stats?.Fights ?? 0,
                    KickingMetres = stats?.KickingMetres ?? 0,
                    MissedConversions = stats?.MissedConversions ?? 0,
                    MissedDropGoals = stats?.MissedDropGoals ?? 0,
                    MissedPenalties = stats?.MissedPenalties ?? 0,
                    GoodUpAndUnders = stats?.GoodUpAndUnders ?? 0,
                    BadUpAndUnders = stats?.BadUpAndUnders ?? 0,
                    UpAndUnders = stats?.UpAndUnders ?? 0,
                    GoodKicks = stats?.GoodKicks ?? 0,
                    BadKicks = stats?.BadKicks ?? 0,
                    TurnoversWon = stats?.TurnoversWon ?? 0,
                    LineoutsSecured = stats?.LineoutsSecured ?? 0,
                    LineoutsConceded = stats?.LineoutsConceded ?? 0,
                    LineoutsStolen = stats?.LineoutsStolen ?? 0,
                    SuccessfulLineoutThrows = stats?.SuccessfulLineoutThrows ?? 0,
                    UnsuccessfulLineoutThrows = stats?.UnsuccessfulLineoutThrows ?? 0,
                    PenaltiesConceded = stats?.PenaltiesConceded ?? 0,
                    KicksOutOnTheFull = stats?.KicksOutOnTheFull ?? 0,
                    BallTime = stats?.BallTime ?? 0,
                    PenaltyTime = stats?.PenaltyTime ?? 0,
                    TotalCaps = stats?.TotalCaps ?? 0,
                    LeagueCaps = stats?.LeagueCaps ?? 0,
                    FriendlyCaps = stats?.FriendlyCaps ?? 0,
                    CupCaps = stats?.CupCaps ?? 0,
                    UnderTwentyCaps = stats?.UnderTwentyCaps ?? 0,
                    NationalCaps = stats?.NationalCaps ?? 0,
                    WorldCupCaps = stats?.WorldCupCaps ?? 0,
                    UnderTwentyWorldCupCaps = stats?.UnderTwentyWorldCupCaps ?? 0,
                    OtherCaps = stats?.OtherCaps ?? 0,
                    RecentPops = player.RecentPops
                };
            })
            .OrderByDescending(player => player.TotalPoints)
            .ThenByDescending(player => player.Tackles)
            .ToList();

        return new TeamDashboardViewModel
        {
            TeamId = request.TeamId,
            TeamName = teamInfo?.Name ?? $"Team {request.TeamId}",
            CountryIso = teamInfo?.CountryIso ?? string.Empty,
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

    private async Task<Dictionary<int, PlayerStatistics>> LoadPlayerStatisticsAsync(BlackoutRugbyApiClient client, IEnumerable<int> playerIds, int season)
    {
        var semaphore = new SemaphoreSlim(6);
        var tasks = playerIds.Select(async playerId =>
        {
            await semaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                var xml = await client.GetPlayerStatisticsAsync(playerId, season: season).ConfigureAwait(false);
                return _responseAdapter.ParsePlayerStatistics(playerId, xml);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Unable to load player statistics for player {PlayerId}", playerId);
                return null;
            }
            finally
            {
                semaphore.Release();
            }
        });

        var stats = await Task.WhenAll(tasks).ConfigureAwait(false);
        return stats
            .Where(item => item is not null)
            .ToDictionary(item => item!.PlayerId, item => item!);
    }

    private static decimal RoundAverage(IEnumerable<int> values)
    {
        var list = values.ToList();
        if (list.Count == 0)
        {
            return 0;
        }

        return Math.Round((decimal)list.Average(), 1, MidpointRounding.AwayFromZero);
    }
}
