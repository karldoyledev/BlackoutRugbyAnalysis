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
                countryIso: query.GetValueOrDefault("country_iso"),
                countryIsos: query.GetValueOrDefault("country_isos"),
                real: query.GetValueOrDefault("real") == "1"),
            
            "divisions" => await client.GetDivisionsAsync(
                divisionId: ParseInt(query.GetValueOrDefault("divisionid")),
                divisionIds: query.GetValueOrDefault("divisionids"),
                season: ParseInt(query.GetValueOrDefault("season")),
                countryIso: query.GetValueOrDefault("country_iso"),
                division: ParseInt(query.GetValueOrDefault("division"))),
            
            "finances" => await client.GetFinancesAsync(
                season: ParseInt(query.GetValueOrDefault("season")) ?? DateTime.Now.Year,
                round: ParseInt(query.GetValueOrDefault("round")) ?? 1),
            
            "fixtures" => await client.GetFixturesAsync(
                fixtureId: ParseInt(query.GetValueOrDefault("fixtureid")),
                fixtureIds: query.GetValueOrDefault("fixtureids"),
                teamId: ParseInt(query.GetValueOrDefault("teamid")),
                last: ParseInt(query.GetValueOrDefault("last")),
                future: ParseInt(query.GetValueOrDefault("future")),
                past: ParseInt(query.GetValueOrDefault("past")),
                latest: ParseInt(query.GetValueOrDefault("latest")),
                leagueId: ParseInt(query.GetValueOrDefault("leagueid")),
                season: ParseInt(query.GetValueOrDefault("season")),
                round: ParseInt(query.GetValueOrDefault("round")),
                roundRobin: query.GetValueOrDefault("roundrobin") == "1",
                friendlyCompId: ParseInt(query.GetValueOrDefault("friendlycompid")),
                youth: query.GetValueOrDefault("youth") == "1",
                nat: query.GetValueOrDefault("nat") == "1",
                u20: query.GetValueOrDefault("u20") == "1"),
            
            "fixturestats" => await client.GetFixtureStatisticsAsync(
                fixtureId: ParseInt(query.GetValueOrDefault("fixtureid")),
                fixtureIds: query.GetValueOrDefault("fixtureids"),
                teamStats: ParseInt(query.GetValueOrDefault("teamstats")),
                teamPlayersStats: ParseInt(query.GetValueOrDefault("teamplayersstats")),
                playerStats: ParseInt(query.GetValueOrDefault("playerstats"))),
            
            "leagues" => await client.GetLeaguesAsync(
                leagueId: ParseInt(query.GetValueOrDefault("leagueid")),
                leagueIds: query.GetValueOrDefault("leagueids"),
                season: ParseInt(query.GetValueOrDefault("season")),
                countryIso: query.GetValueOrDefault("country_iso"),
                division: ParseInt(query.GetValueOrDefault("division")),
                league: ParseInt(query.GetValueOrDefault("league")),
                divisionId: ParseInt(query.GetValueOrDefault("divisionid"))),
            
            "lineups" => await client.GetLineupsAsync(
                teamId: ParseInt(query.GetValueOrDefault("teamid")) ?? _dashboardDefaults.TeamId,
                fixtureId: ParseInt(query.GetValueOrDefault("fixtureid")),
                fixtureIds: query.GetValueOrDefault("fixtureids"),
                youth: query.GetValueOrDefault("youth") == "1",
                nat: query.GetValueOrDefault("nat") == "1",
                u20: query.GetValueOrDefault("u20") == "1"),
            
            "mail" => await client.GetMailAsync(
                folderIds: query.GetValueOrDefault("folderid"),
                subfolderIds: query.GetValueOrDefault("subfolderid"),
                messageId: ParseInt(query.GetValueOrDefault("messageid"))),
            
            "matchcommentary" => await client.GetMatchCommentaryAsync(
                fixtureId: ParseInt(query.GetValueOrDefault("fixtureid")) ?? 0,
                youth: query.GetValueOrDefault("youth") == "1",
                nat: query.GetValueOrDefault("nat") == "1",
                u20: query.GetValueOrDefault("u20") == "1"),
            
            "matchscore" => await client.GetMatchScoreAsync(
                fixtureId: ParseInt(query.GetValueOrDefault("fixtureid")) ?? 0,
                youth: query.GetValueOrDefault("youth") == "1",
                nat: query.GetValueOrDefault("nat") == "1",
                u20: query.GetValueOrDefault("u20") == "1"),
            
            "matchsummary" => await client.GetMatchSummaryAsync(
                fixtureId: ParseInt(query.GetValueOrDefault("fixtureid")),
                fixtureIds: query.GetValueOrDefault("fixtureids"),
                youth: query.GetValueOrDefault("youth") == "1",
                nat: query.GetValueOrDefault("nat") == "1",
                u20: query.GetValueOrDefault("u20") == "1"),
            
            "members" => await client.GetMembersAsync(
                memberId: ParseInt(query.GetValueOrDefault("memberid")),
                memberIds: query.GetValueOrDefault("memberids")),
            
            "playerhistory" => await client.GetPlayerHistoryAsync(
                playerId: ParseInt(query.GetValueOrDefault("playerid")) ?? 1),
            
            "players" => await client.GetPlayersAsync(
                playerId: ParseInt(query.GetValueOrDefault("playerid")),
                playerIds: query.GetValueOrDefault("playerids"),
                teamId: ParseInt(query.GetValueOrDefault("teamid")),
                teamIds: query.GetValueOrDefault("teamids"),
                youth: query.GetValueOrDefault("youth") == "1",
                nat: query.GetValueOrDefault("nat") == "1",
                u20: query.GetValueOrDefault("u20") == "1"),
            
            "playerstats" => await client.GetPlayerStatisticsAsync(
                playerId: ParseInt(query.GetValueOrDefault("playerid")),
                playerIds: query.GetValueOrDefault("playerids"),
                league: query.GetValueOrDefault("league") == "1",
                season: ParseInt(query.GetValueOrDefault("season")),
                nat: query.GetValueOrDefault("nat") == "1",
                wc: query.GetValueOrDefault("wc") == "1",
                u20: query.GetValueOrDefault("u20") == "1"),
            
            "rankings" => await client.GetRankingsAsync(
                iso: query.GetValueOrDefault("iso"),
                regionId: ParseInt(query.GetValueOrDefault("regionid")),
                leagueId: ParseInt(query.GetValueOrDefault("leagueid")),
                nat: query.GetValueOrDefault("nat") == "1",
                u20: query.GetValueOrDefault("u20") == "1",
                start: ParseInt(query.GetValueOrDefault("start")),
                limit: ParseInt(query.GetValueOrDefault("limit"))),
            
            "teams" => await client.GetTeamsAsync(
                teamId: ParseInt(query.GetValueOrDefault("teamid")),
                teamIds: query.GetValueOrDefault("teamids"),
                regionId: ParseInt(query.GetValueOrDefault("regionid")),
                leagueId: ParseInt(query.GetValueOrDefault("leagueid")),
                nat: query.GetValueOrDefault("nat") == "1",
                u20: query.GetValueOrDefault("u20") == "1",
                country: query.GetValueOrDefault("country")),
            
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
