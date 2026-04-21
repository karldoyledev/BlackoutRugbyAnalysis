using Microsoft.AspNetCore.Mvc;
using BlackoutRugby.Api;
using Microsoft.Extensions.Options;
using BlackoutRugbyDashboard.Models;

namespace BlackoutRugbyDashboard.Controllers;

[ApiController]
[Route("api")]
public class ApiTestController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ApiTestController> _logger;
    private readonly DashboardDefaultsOptions _dashboardDefaults;
    private readonly DeveloperOptions _developerOptions;

    public ApiTestController(
        IHttpClientFactory httpClientFactory,
        ILogger<ApiTestController> logger,
        IOptions<DashboardDefaultsOptions> dashboardDefaults,
        IOptions<DeveloperOptions> developerOptions)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _dashboardDefaults = dashboardDefaults.Value;
        _developerOptions = developerOptions.Value;
    }

    [HttpGet("test")]
    public async Task<IActionResult> TestEndpoint([FromQuery] string endpoint, [FromQuery] Dictionary<string, string> query)
    {
        try
        {
            var client = new BlackoutRugbyApiClient(
                _httpClientFactory.CreateClient(),
                _dashboardDefaults.BaseEndpoint,
                new BlackoutRugbyApiCredentials(_dashboardDefaults.MemberId, _dashboardDefaults.MemberKey)
                {
                    DeveloperId = _developerOptions.DeveloperId,
                    DeveloperKey = _developerOptions.DeveloperKey,
                    DeveloperIV = _developerOptions.DeveloperIV
                });

            string response = await ExecuteEndpoint(client, endpoint, query);

            return Ok(new { success = true, response = response });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling API endpoint {Endpoint}", endpoint);
            return Ok(new { success = false, error = ex.Message });
        }
    }

    private async Task<string> ExecuteEndpoint(BlackoutRugbyApiClient client, string endpoint, Dictionary<string, string> query)
    {
        return endpoint switch
        {
            "countries" => await client.GetCountriesAsync(
                iso: query.GetValueOrDefault("iso")),
            
            "divisions" => await client.GetDivisionsAsync(
                divisionId: ParseInt(query.GetValueOrDefault("divisionid"))),
            
            "finances" => await client.GetFinancesAsync(
                season: ParseInt(query.GetValueOrDefault("season")) ?? DateTime.Now.Year,
                round: ParseInt(query.GetValueOrDefault("round")) ?? 1),
            
            "fixtures" => await client.GetFixturesAsync(
                teamId: ParseInt(query.GetValueOrDefault("teamid")),
                fixtureId: ParseInt(query.GetValueOrDefault("fixtureid"))),
            
            "fixturestats" => await client.GetFixtureStatisticsAsync(
                fixtureId: ParseInt(query.GetValueOrDefault("fixtureid")) ?? 0),
            
            "leagues" => await client.GetLeaguesAsync(
                leagueId: ParseInt(query.GetValueOrDefault("leagueid"))),
            
            "lineups" => await client.GetLineupsAsync(
                teamId: ParseInt(query.GetValueOrDefault("teamid")) ?? _dashboardDefaults.TeamId),
            
            "mail" => await client.GetMailAsync(
                folderIds: query.GetValueOrDefault("folderid")?.Split(',').Select(x => ParseInt(x)).OfType<int>()),
            
            "matchcommentary" => await client.GetMatchCommentaryAsync(
                fixtureId: ParseInt(query.GetValueOrDefault("fixtureid")) ?? 0),
            
            "matchscore" => await client.GetMatchScoreAsync(
                fixtureId: ParseInt(query.GetValueOrDefault("fixtureid")) ?? 0),
            
            "matchsummary" => await client.GetMatchSummaryAsync(
                fixtureId: ParseInt(query.GetValueOrDefault("fixtureid")) ?? 0),
            
            "members" => await client.GetMembersAsync(
                memberId: ParseInt(query.GetValueOrDefault("memberid"))),
            
            "playerhistory" => await client.GetPlayerHistoryAsync(
                playerId: ParseInt(query.GetValueOrDefault("playerid")) ?? 1),
            
            "players" => await client.GetPlayersAsync(
                playerId: ParseInt(query.GetValueOrDefault("playerid")),
                teamId: ParseInt(query.GetValueOrDefault("teamid"))),
            
            "playerstats" => await client.GetPlayerStatisticsAsync(
                playerId: ParseInt(query.GetValueOrDefault("playerid")) ?? 1),
            
            "rankings" => await client.GetRankingsAsync(
                leagueId: ParseInt(query.GetValueOrDefault("leagueid"))),
            
            "teams" => await client.GetTeamsAsync(
                teamId: ParseInt(query.GetValueOrDefault("teamid"))),
            
            _ => throw new ArgumentException($"Unknown endpoint: {endpoint}")
        };
    }

    private static int? ParseInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        if (int.TryParse(value, out var result))
            return result;
        return null;
    }
}
