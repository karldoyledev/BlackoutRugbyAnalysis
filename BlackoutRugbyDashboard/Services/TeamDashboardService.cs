using System.Globalization;
using System.Xml.Linq;
using BlackoutRugby.Api;
using BlackoutRugbyDashboard.Models;

namespace BlackoutRugbyDashboard.Services;

public class TeamDashboardService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TeamDashboardService> _logger;

    public TeamDashboardService(IHttpClientFactory httpClientFactory, ILogger<TeamDashboardService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<TeamDashboardViewModel> BuildDashboardAsync(TeamDashboardRequest request)
    {
        var credentials = request.HasCredentials
            ? new BlackoutRugbyApiCredentials(request.MemberId, request.MemberKey)
            : null;

        var client = new BlackoutRugbyApiClient(_httpClientFactory.CreateClient(), request.BaseEndpoint, credentials);
        var teamTask = client.GetTeamsAsync(teamId: request.TeamId);
        var playersXml = await client.GetPlayersAsync(teamId: request.TeamId).ConfigureAwait(false);
        var players = ParsePlayers(playersXml);

        if (players.Count == 0)
        {
            var team = ParseTeam(await teamTask.ConfigureAwait(false));
            return new TeamDashboardViewModel
            {
                TeamId = request.TeamId,
                TeamName = team?.Name ?? $"Team {request.TeamId}",
                CountryIso = team?.CountryIso ?? string.Empty
            };
        }

        var statsByPlayerId = await LoadPlayerStatisticsAsync(client, players.Select(player => player.Id)).ConfigureAwait(false);
        var teamInfo = ParseTeam(await teamTask.ConfigureAwait(false));

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
                    TotalPoints = stats?.TotalPoints ?? 0,
                    TotalCaps = stats?.TotalCaps ?? 0,
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

    private async Task<Dictionary<int, PlayerStats>> LoadPlayerStatisticsAsync(BlackoutRugbyApiClient client, IEnumerable<int> playerIds)
    {
        var semaphore = new SemaphoreSlim(6);
        var tasks = playerIds.Select(async playerId =>
        {
            await semaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                var xml = await client.GetPlayerStatisticsAsync(playerId).ConfigureAwait(false);
                return ParsePlayerStatistics(playerId, xml);
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

    private static TeamInfo? ParseTeam(string xml)
    {
        var document = TryParse(xml);
        var element = document?.Descendants("team").FirstOrDefault();
        if (element is null)
        {
            return null;
        }

        return new TeamInfo(
            ReadInt(element, "id"),
            Decode(ReadString(element, "name")) ?? string.Empty,
            Decode(ReadString(element, "country_iso")) ?? string.Empty);
    }

    private static List<PlayerInfo> ParsePlayers(string xml)
    {
        var document = TryParse(xml);
        if (document is null)
        {
            return new List<PlayerInfo>();
        }

        return document
            .Descendants("player")
            .Select(element => new PlayerInfo(
                ReadInt(element, "id"),
                BuildPlayerName(element),
                ReadInt(element, "age"),
                ReadInt(element, "csr"),
                ReadInt(element, "salary"),
                ReadInt(element, "form"),
                ReadInt(element, "energy"),
                element.Descendants("pops").Descendants("skill").Select(skill => Decode(skill.Value) ?? skill.Value).Where(value => !string.IsNullOrWhiteSpace(value)).ToList()))
            .Where(player => player.Id > 0)
            .ToList();
    }

    private static PlayerStats? ParsePlayerStatistics(int playerId, string xml)
    {
        var document = TryParse(xml);
        var element = document?.Descendants("player_statistics").FirstOrDefault();
        if (element is null)
        {
            return null;
        }

        return new PlayerStats(
            playerId,
            ReadInt(element, "tackles"),
            ReadInt(element, "metresgained"),
            ReadInt(element, "tries"),
            ReadInt(element, "totalpoints"),
            ReadInt(element, "leaguecaps")
                + ReadInt(element, "friendlycaps")
                + ReadInt(element, "cupcaps")
                + ReadInt(element, "undertwentycaps")
                + ReadInt(element, "nationalcaps")
                + ReadInt(element, "worldcupcaps")
                + ReadInt(element, "undertwentyworldcupcaps")
                + ReadInt(element, "othercaps"));
    }

    private static XDocument? TryParse(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return null;
        }

        try
        {
            return XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        }
        catch
        {
            return null;
        }
    }

    private static string BuildPlayerName(XElement element)
    {
        var firstName = Decode(ReadString(element, "fname"));
        var lastName = Decode(ReadString(element, "lname"));
        return string.Join(' ', new[] { firstName, lastName }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static string? ReadString(XElement element, string name)
    {
        return element.Element(name)?.Value?.Trim();
    }

    private static int ReadInt(XElement element, string name)
    {
        var rawValue = ReadString(element, name);
        if (int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }

        return 0;
    }

    private static string? Decode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return Uri.UnescapeDataString(value.Replace('+', ' '));
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

    private sealed record TeamInfo(int Id, string Name, string CountryIso);

    private sealed record PlayerInfo(int Id, string Name, int Age, int Csr, int Salary, int Form, int Energy, IReadOnlyList<string> RecentPops);

    private sealed record PlayerStats(int PlayerId, int Tackles, int MetresGained, int Tries, int TotalPoints, int TotalCaps);
}
