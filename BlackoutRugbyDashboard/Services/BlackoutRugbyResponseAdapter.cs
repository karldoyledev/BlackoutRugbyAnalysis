using System.Globalization;
using System.Xml.Linq;

namespace BlackoutRugbyDashboard.Services;

/// <summary>
/// Single deep XML-to-domain adapter for Blackout Rugby API responses. The adapter
/// owns XML parsing, text decoding, integer normalization, alternate-field
/// resolution, and response-error extraction, so the team dashboard and the fixture
/// comparison workflows interpret Team, Player, Player-statistics, Fixture, and
/// Fixture-statistics responses through one consistent rule set.
/// 
/// Blank or malformed responses are treated as non-results (null or an empty list)
/// rather than thrown exceptions. HTTP retrieval, credentials, dashboard assembly,
/// fixture aggregation, and rendering stay outside this adapter.
/// </summary>
public class BlackoutRugbyResponseAdapter
{
    /// <summary>
    /// Parses a Team response. Returns null when no team element can be read.
    /// </summary>
    public Team? ParseTeam(string? xml)
    {
        var document = TryParse(xml);
        var element = document?.Descendants("team").FirstOrDefault();
        if (element is null)
        {
            return null;
        }

        return new Team(
            ReadInt(element, "id"),
            Decode(ReadString(element, "name")) ?? string.Empty,
            Decode(ReadString(element, "country_iso")) ?? string.Empty);
    }

    /// <summary>
    /// Parses a Players response into roster entries in response order. Players
    /// without a valid id are skipped.
    /// </summary>
    public IReadOnlyList<Player> ParsePlayers(string? xml)
    {
        var document = TryParse(xml);
        if (document is null)
        {
            return [];
        }

        return document
            .Descendants("player")
            .Select(element => new Player(
                ReadInt(element, "id"),
                BuildPlayerName(element),
                ReadInt(element, "age"),
                ReadInt(element, "csr"),
                ReadInt(element, "salary"),
                ReadInt(element, "form"),
                ReadInt(element, "energy"),
                element
                    .Descendants("pops")
                    .Descendants("skill")
                    .Select(skill => Decode(skill.Value) ?? skill.Value)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .ToList()))
            .Where(player => player.Id > 0)
            .ToList();
    }

    /// <summary>
    /// Parses a Player-statistics response for one roster player. The displayed
    /// caps total is derived by summing the per-competition cap counts. Returns
    /// null when no statistics element can be read.
    /// </summary>
    public PlayerStatistics? ParsePlayerStatistics(int playerId, string? xml)
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

        return new PlayerStatistics(
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

    /// <summary>
    /// Parses a Fixtures response into fixtures in response order. Alternate field
    /// names are resolved, dates accept text or Unix-seconds forms, and fixtures
    /// with unparseable dates are skipped.
    /// </summary>
    public IReadOnlyList<Fixture> ParseFixtures(string? xml)
    {
        var document = TryParse(xml);
        if (document is null)
        {
            return [];
        }

        var fixtures = new List<Fixture>();
        foreach (var element in document.Descendants("fixture"))
        {
            var id = ReadInt(element, "id");
            var dateText = ReadString(element, "date");
            var matchStartUnix = ReadString(element, "matchstart");
            var season = ReadIntAny(element, "season");
            var round = ReadIntAny(element, "round");
            var competition = Decode(ReadString(element, "competition")) ?? string.Empty;

            var homeTeamId = ReadIntAny(element, "home_team_id");
            if (homeTeamId == 0)
            {
                homeTeamId = ReadIntAny(element, "hometeamid");
            }

            var awayTeamId = ReadIntAny(element, "away_team_id");
            if (awayTeamId == 0)
            {
                awayTeamId = ReadIntAny(element, "guestteamid");
            }

            var homeTeamNameRaw = ReadString(element, "home_team_name")
                ?? ReadString(element, "hometeam")
                ?? ReadString(element, "hometeamname");
            var awayTeamNameRaw = ReadString(element, "away_team_name")
                ?? ReadString(element, "guestteam")
                ?? ReadString(element, "guestteamname");

            var homeScore = ReadIntAny(element, "home_score");
            if (homeScore == 0)
            {
                homeScore = ReadIntAny(element, "homescore");
            }

            var awayScore = ReadIntAny(element, "away_score");
            if (awayScore == 0)
            {
                awayScore = ReadIntAny(element, "guestscore");
            }

            if (!TryReadFixtureDate(dateText, matchStartUnix, out var date))
            {
                continue;
            }

            fixtures.Add(new Fixture(
                id,
                season,
                round,
                competition,
                date,
                homeTeamId,
                awayTeamId,
                ResolveTeamName(homeTeamNameRaw, homeTeamId),
                ResolveTeamName(awayTeamNameRaw, awayTeamId),
                homeScore,
                awayScore));
        }

        return fixtures;
    }

    /// <summary>
    /// Parses a Fixture-statistics response into per-Player statistics for one
    /// team, ranked by total points, then tackles, then name. Player names are
    /// correlated with the roster supplied by the caller.
    /// </summary>
    public IReadOnlyList<FixturePlayerStatistics> ParseFixturePlayerStatistics(
        string? xml,
        int teamId,
        IReadOnlyDictionary<int, string>? rosterNames = null)
    {
        var document = TryParse(xml);
        if (document is null)
        {
            return [];
        }

        var teamIdText = teamId.ToString(CultureInfo.InvariantCulture);
        return document
            .Descendants()
            .Where(element => string.Equals(element.Attribute("teamid")?.Value, teamIdText, StringComparison.Ordinal))
            .Where(element => element.Name.LocalName.Contains("player", StringComparison.OrdinalIgnoreCase))
            .Select(element => BuildFixturePlayerStatistics(element, rosterNames))
            .Where(item => item is not null)
            .Cast<FixturePlayerStatistics>()
            .OrderByDescending(item => item.TotalPoints)
            .ThenByDescending(item => item.Tackles)
            .ThenBy(item => item.Name)
            .ToList();
    }

    /// <summary>
    /// Extracts the response-level error text from an error response. Returns null
    /// when the content is blank, malformed, or carries no error element.
    /// </summary>
    public string? ExtractResponseError(string? xml)
    {
        var document = TryParse(xml);
        return document?.Descendants("error").FirstOrDefault()?.Value;
    }

    private static FixturePlayerStatistics? BuildFixturePlayerStatistics(XElement element, IReadOnlyDictionary<int, string>? rosterNames)
    {
        var playerId = ReadIntAny(element, "id");
        if (playerId <= 0)
        {
            return null;
        }

        string? playerName = null;
        rosterNames?.TryGetValue(playerId, out playerName);

        return new FixturePlayerStatistics(
            playerId,
            string.IsNullOrWhiteSpace(playerName) ? $"Player {playerId}" : playerName,
            ReadIntAny(element, "tackles"),
            ReadIntAny(element, "metresgained", "metres_gained"),
            ReadIntAny(element, "tries"),
            ReadIntAny(element, "conversions"),
            ReadIntAny(element, "dropgoals", "drop_goals"),
            ReadIntAny(element, "penalties"),
            ReadIntAny(element, "totalpoints", "total_points"),
            ReadIntAny(element, "yellowcards", "yellow_cards"),
            ReadIntAny(element, "redcards", "red_cards"),
            ReadIntAny(element, "linebreaks"),
            ReadIntAny(element, "intercepts"),
            ReadIntAny(element, "kicks"),
            ReadIntAny(element, "knockons", "knock_ons"),
            ReadIntAny(element, "forwardpasses", "forward_passes"),
            ReadIntAny(element, "tryassists", "try_assists"),
            ReadIntAny(element, "beatendefenders", "beaten_defenders"),
            ReadIntAny(element, "injuries"),
            ReadIntAny(element, "handlingerrors", "handling_errors"),
            ReadIntAny(element, "missedtackles", "missed_tackles"),
            ReadIntAny(element, "fights"),
            ReadIntAny(element, "kickingmetres", "kicking_metres"),
            ReadIntAny(element, "penaltiesconceded", "penalties_conceded"),
            ReadIntAny(element, "kicksoutonthefull", "kicks_out_on_the_full"),
            ReadIntAny(element, "lineoutswon", "lineouts_won"),
            ReadIntAny(element, "lineoutslost", "lineouts_lost"),
            ReadIntAny(element, "scrumswon", "scrums_won"),
            ReadIntAny(element, "scrumslost", "scrums_lost"));
    }

    private static string ResolveTeamName(string? rawName, int teamId)
    {
        return rawName is null ? $"Team {teamId}" : Decode(rawName) ?? $"Team {teamId}";
    }

    private static bool TryReadFixtureDate(string? dateText, string? matchStartUnix, out DateTime date)
    {
        if (DateTime.TryParse(dateText, out var parsedDate))
        {
            date = parsedDate;
            return true;
        }

        if (long.TryParse(matchStartUnix, NumberStyles.Any, CultureInfo.InvariantCulture, out var unixSeconds))
        {
            date = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).LocalDateTime;
            return true;
        }

        date = default;
        return false;
    }

    /// <summary>
    /// Parses response XML, returning null for blank input or malformed XML rather
    /// than throwing.
    /// </summary>
    private static XDocument? TryParse(string? xml)
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

    private static string? ReadString(XElement element, string name)
    {
        return element.Element(name)?.Value?.Trim();
    }

    /// <summary>
    /// Normalizes an integer from the first resolvable field name. Grouped values
    /// such as "1,250,000" or "1.250.000" are read without losing digits; missing
    /// or invalid values normalize to zero.
    /// </summary>
    private static int ReadInt(XElement element, string name)
    {
        return ReadIntAny(element, name);
    }

    private static int ReadIntAny(XElement element, params string[] names)
    {
        foreach (var name in names)
        {
            var rawValue = element.Element(name)?.Value?.Trim();
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                continue;
            }

            if (int.TryParse(rawValue, NumberStyles.AllowThousands | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var groupedResult))
            {
                return groupedResult;
            }

            var cleanedValue = rawValue.Replace(",", string.Empty).Replace(".", string.Empty);
            if (int.TryParse(cleanedValue, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var cleanedResult))
            {
                return cleanedResult;
            }
        }

        return 0;
    }

    /// <summary>
    /// Decodes URL-encoded API text ("Wellington+Rugby", "O%27Brien") so names read
    /// consistently in every workflow.
    /// </summary>
    private static string? Decode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return Uri.UnescapeDataString(value.Replace('+', ' '));
    }

    private static string BuildPlayerName(XElement element)
    {
        var firstName = Decode(ReadString(element, "fname"));
        var lastName = Decode(ReadString(element, "lname"));
        return string.Join(' ', new[] { firstName, lastName }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }
}

/// <summary>Normalized Team details from a Team response.</summary>
public sealed record Team(int Id, string Name, string CountryIso);

/// <summary>Normalized Player roster entry from a Players response.</summary>
public sealed record Player(
    int Id,
    string Name,
    int Age,
    int Csr,
    int Salary,
    int Form,
    int Energy,
    IReadOnlyList<string> RecentPops);

/// <summary>
/// Normalized season Player statistics from a Player-statistics response.
/// <see cref="TotalCaps"/> is derived by summing the per-competition cap counts.
/// </summary>
public sealed record PlayerStatistics(
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

/// <summary>Normalized Fixture details from a Fixtures response, in response order.</summary>
public sealed record Fixture(
    int Id,
    int Season,
    int Round,
    string Competition,
    DateTime Date,
    int HomeTeamId,
    int AwayTeamId,
    string HomeTeamName,
    string AwayTeamName,
    int HomeScore,
    int AwayScore);

/// <summary>
/// Normalized per-Player Fixture statistics for one team, ranked by total points,
/// then tackles, then name. Player names are correlated with the roster supplied
/// by the caller.
/// </summary>
public sealed record FixturePlayerStatistics(
    int PlayerId,
    string Name,
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
    int PenaltiesConceded,
    int KicksOutOnTheFull,
    int LineoutsWon,
    int LineoutsLost,
    int ScrumWins,
    int ScrumLosses);