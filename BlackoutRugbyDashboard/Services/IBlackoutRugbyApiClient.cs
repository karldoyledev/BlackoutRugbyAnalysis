namespace BlackoutRugbyDashboard.Services;

/// <summary>
/// The HTTP-boundary seam (spec Testing Decisions §4): page models and the Match
/// Cache fill service depend on this interface, so tests inject a fake and never
/// call the live API. Implemented by BlackoutRugby.Api.BlackoutRugbyApiClient.
/// </summary>
public interface IBlackoutRugbyApiClient
{
    Task<string> GetFixturesAsync(int? fixtureId = null, string? fixtureIds = null, int? teamId = null, int? last = null, int? future = null, int? past = null, int? latest = null, int? leagueId = null, int? season = null, int? round = null, bool roundRobin = false, int? friendlyCompId = null, bool youth = false, bool nat = false, bool u20 = false);

    Task<string> GetMatchSummaryAsync(int? fixtureId = null, string? fixtureIds = null, bool youth = false, bool nat = false, bool u20 = false);

    Task<string> GetTeamsAsync(int? teamId = null, string? teamIds = null, int? regionId = null, int? leagueId = null, bool nat = false, bool u20 = false, string? country = null);

    Task<string> GetPlayersAsync(int? playerId = null, string? playerIds = null, int? teamId = null, string? teamIds = null, bool youth = false, bool nat = false, bool u20 = false);

    Task<string> GetFixtureStatisticsAsync(int fixtureId, int? playerStats = null, int? teamPlayersStats = null);

    Task<string> GetPlayerStatisticsAsync(int playerId);

    /// <summary>
    /// The season-filtered ps read (r=ps + playerid + season) that the Squad
    /// page's cached season aggregates use. Distinct member from the plain ps
    /// read — the concrete client delegates both to the same endpoint.
    /// </summary>
    Task<string> GetPlayerStatisticsAsync(int playerId, int? season);

    /// <summary>
    /// The verified member probe (r=m + memberid, R2 F2 / D3 §5): reads one
    /// Member record — the Club Link validation read. Candidate member
    /// credentials ride the client, so probes go through
    /// IBlackoutRugbyApiClientFactory.CreateForMember.
    /// </summary>
    Task<string> GetMemberAsync(int memberId);

    Task<string> GetLineupsAsync(int teamId, int? fixtureId = null, string? fixtureIds = null, bool youth = false, bool nat = false, bool u20 = false);
}
