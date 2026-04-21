using System.Globalization;
using System.Xml.Linq;
using BlackoutRugby.Api;
using BlackoutRugbyDashboard.Models;
using Microsoft.Extensions.Options;

namespace BlackoutRugbyDashboard.Services;

public class TeamDashboardService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TeamDashboardService> _logger;
    private readonly DeveloperOptions _developerOptions;

    public TeamDashboardService(IHttpClientFactory httpClientFactory, ILogger<TeamDashboardService> logger, IOptions<DeveloperOptions> developerOptions)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _developerOptions = developerOptions.Value;
    }

    public async Task<TeamDashboardViewModel> BuildDashboardAsync(TeamDashboardRequest request)
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

        var statsByPlayerId = await LoadPlayerStatisticsAsync(client, players.Select(player => player.Id), request.Season).ConfigureAwait(false);
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

    private async Task<Dictionary<int, PlayerStats>> LoadPlayerStatisticsAsync(BlackoutRugbyApiClient client, IEnumerable<int> playerIds, int season)
    {
        var semaphore = new SemaphoreSlim(6);
        var tasks = playerIds.Select(async playerId =>
        {
            await semaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                var xml = await client.GetPlayerStatisticsAsync(playerId, season: season).ConfigureAwait(false);
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

        var leagueCaps = ReadInt(element, "leaguecaps");
        var friendlyCaps = ReadInt(element, "friendlycaps");
        var cupCaps = ReadInt(element, "cupcaps");
        var underTwentyCaps = ReadInt(element, "undertwentycaps");
        var nationalCaps = ReadInt(element, "nationalcaps");
        var worldCupCaps = ReadInt(element, "worldcupcaps");
        var underTwentyWorldCupCaps = ReadInt(element, "undertwentyworldcupcaps");
        var otherCaps = ReadInt(element, "othercaps");
        var totalCaps = leagueCaps + friendlyCaps + cupCaps + underTwentyCaps + nationalCaps + worldCupCaps + underTwentyWorldCupCaps + otherCaps;

        return new PlayerStats(
            playerId,
            ReadInt(element, "tackles"),
            ReadInt(element, "metresgained"),
            ReadInt(element, "tries"),
            ReadInt(element, "conversions"),
            ReadInt(element, "dropgoals"),
            ReadInt(element, "penalties"),
            ReadInt(element, "totalpoints"),
            ReadInt(element, "yellowcards"),
            ReadInt(element, "redcards"),
            ReadInt(element, "linebreaks"),
            ReadInt(element, "intercepts"),
            ReadInt(element, "kicks"),
            ReadInt(element, "knockons"),
            ReadInt(element, "forwardpasses"),
            ReadInt(element, "tryassists"),
            ReadInt(element, "beatendefenders"),
            ReadInt(element, "injuries"),
            ReadInt(element, "handlingerrors"),
            ReadInt(element, "missedtackles"),
            ReadInt(element, "fights"),
            ReadInt(element, "kickingmetres"),
            ReadInt(element, "missedconversions"),
            ReadInt(element, "misseddropgoals"),
            ReadInt(element, "missedpenalties"),
            ReadInt(element, "goodupandunders"),
            ReadInt(element, "badupandunders"),
            ReadInt(element, "upandunders"),
            ReadInt(element, "goodkicks"),
            ReadInt(element, "badkicks"),
            ReadInt(element, "turnoverswon"),
            ReadInt(element, "lineoutssecured"),
            ReadInt(element, "lineoutsconceded"),
            ReadInt(element, "lineoutsstolen"),
            ReadInt(element, "successfullineoutthrows"),
            ReadInt(element, "unsuccessfullineoutthrows"),
            ReadInt(element, "penaltiesconceded"),
            ReadInt(element, "kicksoutonthefull"),
            ReadInt(element, "balltime"),
            ReadInt(element, "penaltytime"),
            totalCaps,
            leagueCaps,
            friendlyCaps,
            cupCaps,
            underTwentyCaps,
            nationalCaps,
            worldCupCaps,
            underTwentyWorldCupCaps,
            otherCaps);
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
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return 0;
        }

        // Try parsing with thousands separators (both . and , formats)
        if (int.TryParse(rawValue, NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }

        // Try parsing with comma as thousands separator (European format)
        var cleanedValue = rawValue.Replace(",", "").Replace(".", "");
        if (int.TryParse(cleanedValue, out var cleanedResult))
        {
            return cleanedResult;
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

    private sealed record PlayerStats(
        int PlayerId,
        int Tackles,
        int MetresGained,
        int Tries,
        int Conversions,
        int DropGoals,
        int Penalties,
        int TotalPoints,
        int YellowCards,
        int RedCards,
        int Linebreaks,
        int Intercepts,
        int Kicks,
        int KnockOns,
        int ForwardPasses,
        int TryAssists,
        int BeatenDefenders,
        int Injuries,
        int HandlingErrors,
        int MissedTackles,
        int Fights,
        int KickingMetres,
        int MissedConversions,
        int MissedDropGoals,
        int MissedPenalties,
        int GoodUpAndUnders,
        int BadUpAndUnders,
        int UpAndUnders,
        int GoodKicks,
        int BadKicks,
        int TurnoversWon,
        int LineoutsSecured,
        int LineoutsConceded,
        int LineoutsStolen,
        int SuccessfulLineoutThrows,
        int UnsuccessfulLineoutThrows,
        int PenaltiesConceded,
        int KicksOutOnTheFull,
        int BallTime,
        int PenaltyTime,
        int TotalCaps,
        int LeagueCaps,
        int FriendlyCaps,
        int CupCaps,
        int UnderTwentyCaps,
        int NationalCaps,
        int WorldCupCaps,
        int UnderTwentyWorldCupCaps,
        int OtherCaps);
}
