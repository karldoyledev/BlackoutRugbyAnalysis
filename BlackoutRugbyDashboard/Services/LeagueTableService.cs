using System.Diagnostics;
using BlackoutRugbyDashboard.Models;
using Microsoft.Extensions.Options;

namespace BlackoutRugbyDashboard.Services;

/// <summary>One row of the league table: the standing plus the team's name.</summary>
public sealed record LeagueTableRow(
    int Position,
    int TeamId,
    string TeamName,
    int Played,
    int Won,
    int Drawn,
    int Lost,
    int PointsFor,
    int PointsAgainst,
    int BonusPoints,
    int Points);

/// <summary>
/// The club's league table: the league and season it belongs to, the club's own
/// team id (so the card can mark its row), and the rows in standing order.
/// </summary>
public sealed record LeagueTable(
    int LeagueId,
    int Season,
    int OwnTeamId,
    IReadOnlyList<LeagueTableRow> Rows);

/// <summary>
/// The league-table read (r=s + r=t&amp;leagueid, probed live 2026-10-06): the
/// club's league standings with the member teams' names — two calls. Standings are
/// point-in-time and are not part of the Match Cache, so the Home page reads them
/// on its bare visit; a failure simply leaves the card off. The standings response
/// carries no team name, so the second read names the rows.
/// </summary>
public class LeagueTableService(
    IBlackoutRugbyApiClient api,
    BlackoutRugbyResponseAdapter adapter,
    ApiLogger apiLogger,
    IOptions<DashboardDefaultsOptions> defaults)
{
    public async Task<LeagueTable?> GetAsync(int leagueId, int ownTeamId, CancellationToken cancellationToken = default)
    {
        if (leagueId <= 0)
        {
            return null;
        }

        var standingsXml = await ReadLiveAsync(
            () => api.GetStandingsAsync(leagueId: leagueId),
            $"{Endpoint()}/standings?leagueId={leagueId}");

        var standings = adapter.ParseStandings(standingsXml);
        if (standings.Count == 0)
        {
            return null;
        }

        var teamsXml = await ReadLiveAsync(
            () => api.GetTeamsAsync(leagueId: leagueId),
            $"{Endpoint()}/teams?leagueId={leagueId}");
        var names = adapter.ParseTeams(teamsXml)
            .GroupBy(team => team.Id)
            .ToDictionary(group => group.Key, group => group.First().Name);

        var rows = standings
            .OrderBy(row => row.Position)
            .Select(row => new LeagueTableRow(
                row.Position,
                row.TeamId,
                names.TryGetValue(row.TeamId, out var name) && !string.IsNullOrWhiteSpace(name)
                    ? name
                    : $"Team {row.TeamId}",
                row.Played,
                row.Won,
                row.Drawn,
                row.Lost,
                row.PointsFor,
                row.PointsAgainst,
                row.BonusPoints,
                row.Points))
            .ToList();

        return new LeagueTable(leagueId, standings[0].Season, ownTeamId, rows);
    }

    /// <summary>One live read with the page's request/response logging shape, so the
    /// debug panel shows the league table's two calls and their cost.</summary>
    private async Task<string> ReadLiveAsync(Func<Task<string>> read, string url)
    {
        apiLogger.LogRequest("GET", url);
        var stopwatch = Stopwatch.StartNew();
        var xml = await read();
        apiLogger.LogResponse(url, 200, Truncate(xml), stopwatch.ElapsedMilliseconds);
        return xml;
    }

    private static string? Truncate(string? value) =>
        value?.Length > 3000 ? value[..3000] + "\n... (truncated)" : value;

    private string Endpoint() =>
        string.IsNullOrWhiteSpace(defaults.Value.BaseEndpoint)
            ? "http://classic-api.blackoutrugby.com"
            : defaults.Value.BaseEndpoint;
}