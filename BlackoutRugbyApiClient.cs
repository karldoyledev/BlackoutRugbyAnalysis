using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace BlackoutRugby.Api
{
    public class BlackoutRugbyApiCredentials
    {
        public BlackoutRugbyApiCredentials(int? memberId, string? memberKey)
        {
            MemberId = memberId;
            MemberKey = memberKey;
        }

        public int? MemberId { get; }

        public string? MemberKey { get; }

        public int? DeveloperId { get; set; }

        public string? DeveloperKey { get; set; }

        public string? DeveloperIV { get; set; }
    }

    public class BlackoutRugbyApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseEndpoint;
        private BlackoutRugbyApiCredentials? _credentials;

        public BlackoutRugbyApiClient(string baseEndpoint, BlackoutRugbyApiCredentials? credentials = null)
            : this(new HttpClient(), baseEndpoint, credentials)
        {
        }

        public BlackoutRugbyApiClient(HttpClient httpClient, string baseEndpoint, BlackoutRugbyApiCredentials? credentials = null)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _baseEndpoint = baseEndpoint?.TrimEnd('/') ?? throw new ArgumentNullException(nameof(baseEndpoint));
            _credentials = credentials;
        }

        public void SetCredentials(BlackoutRugbyApiCredentials? credentials)
        {
            _credentials = credentials;
        }

        /// <summary>
        /// Get country information from the API.
        /// </summary>
        /// <param name="countryIso">Optional: Single country ISO code (e.g., "NZ")</param>
        /// <param name="countryIsos">Optional: Multiple country ISO codes separated by comma</param>
        /// <param name="real">Optional: Return real countries (true) vs game countries (false)</param>
        /// <returns>XML response containing country data</returns>
        public async Task<string> GetCountriesAsync(string? countryIso = null, string? countryIsos = null, bool real = false)
        {
            return await SendRequestAsync("c", new Dictionary<string, string?>
            {
                ["country_iso"] = countryIso,
                ["country_isos"] = countryIsos,
                ["real"] = real ? "1" : null
            });
        }

        // Date and time are returned in every API response root via the BRT attributes.
        // There is no dedicated standalone request type for Date and Time.

        /// <summary>
        /// Get division information. Divisions represent the different levels of competition within a country.
        /// </summary>
        /// <param name="divisionId">Optional: Single division ID</param>
        /// <param name="divisionIds">Optional: Multiple division IDs (comma-separated)</param>
        /// <param name="season">Optional: Season year to filter by</param>
        /// <param name="countryIso">Optional: Country ISO code to filter by</param>
        /// <param name="division">Optional: Division number/level</param>
        /// <returns>XML response containing division data</returns>
        public async Task<string> GetDivisionsAsync(int? divisionId = null, string? divisionIds = null, int? season = null, string? countryIso = null, int? division = null)
        {
            return await SendRequestAsync("d", new Dictionary<string, string?>
            {
                ["divisionid"] = divisionId?.ToString(),
                ["divisionids"] = divisionIds,
                ["season"] = season?.ToString(),
                ["country_iso"] = countryIso,
                ["division"] = division?.ToString()
            });
        }

        /// <summary>
        /// Get team financial data for a specific season and round. Shows income, expenses, and bank balance.
        /// </summary>
        /// <param name="season">Required: Season year (e.g., 2024)</param>
        /// <param name="round">Required: Round number within the season (e.g., 1)</param>
        /// <returns>XML response containing financial transactions and balance</returns>
        public async Task<string> GetFinancesAsync(int season, int round)
        {
            return await SendRequestAsync("fi", new Dictionary<string, string?>
            {
                ["season"] = season.ToString(),
                ["round"] = round.ToString()
            });
        }

        /// <summary>
        /// Get fixture/match information including team matchups and scheduling details.
        /// </summary>
        /// <param name="fixtureId">Optional: Single fixture ID</param>
        /// <param name="fixtureIds">Optional: Multiple fixture IDs (comma-separated)</param>
        /// <param name="teamId">Optional: Get fixtures for a specific team</param>
        /// <param name="last">Optional: Get last N fixtures</param>
        /// <param name="future">Optional: Get upcoming fixtures for a team</param>
        /// <param name="past">Optional: Get past fixtures for a team</param>
        /// <param name="youth">Optional: Include youth fixtures</param>
        /// <param name="nat">Optional: Include national team fixtures</param>
        /// <param name="u20">Optional: Include Under-20 team fixtures</param>
        /// <param name="latest">Optional: Get latest fixtures for a league</param>
        /// <param name="leagueId">Optional: Get fixtures for a specific league</param>
        /// <param name="season">Optional: Filter by season</param>
        /// <param name="round">Optional: Filter by round number</param>
        /// <param name="roundRobin">Optional: Get round-robin tournament fixtures</param>
        /// <param name="friendlyCompId">Optional: Get friendly competition fixtures</param>
        /// <returns>XML response containing fixture data</returns>
        public async Task<string> GetFixturesAsync(int? fixtureId = null, string? fixtureIds = null, int? teamId = null, int? last = null, int? future = null, int? past = null, int? latest = null, int? leagueId = null, int? season = null, int? round = null, bool roundRobin = false, int? friendlyCompId = null, bool youth = false, bool nat = false, bool u20 = false)
        {
            return await SendRequestAsync("fix", new Dictionary<string, string?>
            {
                ["fixtureid"] = fixtureId?.ToString(),
                ["fixtureids"] = fixtureIds,
                ["teamid"] = teamId?.ToString(),
                ["last"] = last?.ToString(),
                ["future"] = future?.ToString(),
                ["past"] = past?.ToString(),
                ["latest"] = latest?.ToString(),
                ["leagueid"] = leagueId?.ToString(),
                ["season"] = season?.ToString(),
                ["round"] = round?.ToString(),
                ["roundrobin"] = roundRobin ? "1" : null,
                ["friendlycompid"] = friendlyCompId?.ToString(),
                ["youth"] = youth ? "1" : null,
                ["nat"] = nat ? "1" : null,
                ["u20"] = u20 ? "1" : null
            });
        }

        /// <summary>
        /// Get detailed statistics for a fixture including team and player performance metrics.
        /// </summary>
        /// <param name="fixtureId">Optional: Single fixture ID for statistics</param>
        /// <param name="fixtureIds">Optional: Multiple fixture IDs for statistics (comma-separated)</param>
        /// <param name="teamStats">Optional: Get team-level statistics for a specific team</param>
        /// <param name="teamPlayersStats">Optional: Get player-level statistics for a team's players</param>
        /// <param name="playerStats">Optional: Get statistics for a specific player</param>
        /// <returns>XML response containing fixture statistics</returns>
        public async Task<string> GetFixtureStatisticsAsync(int? fixtureId = null, string? fixtureIds = null, int? teamStats = null, int? teamPlayersStats = null, int? playerStats = null)
        {
            return await SendRequestAsync("fs", new Dictionary<string, string?>
            {
                ["fixtureid"] = fixtureId?.ToString(),
                ["fixtureids"] = fixtureIds,
                ["teamstats"] = teamStats?.ToString(),
                ["teamplayersstats"] = teamPlayersStats?.ToString(),
                ["playerstats"] = playerStats?.ToString()
            });
        }

        /// <summary>
        /// Get league information including standings and scheduling details.
        /// </summary>
        /// <param name="leagueId">Optional: Single league ID</param>
        /// <param name="leagueIds">Optional: Multiple league IDs (comma-separated)</param>
        /// <param name="season">Optional: Filter by season year</param>
        /// <param name="countryIso">Optional: Filter by country ISO code</param>
        /// <param name="division">Optional: Filter by division number</param>
        /// <param name="league">Optional: Filter by league number</param>
        /// <param name="divisionId">Optional: Filter by division ID</param>
        /// <returns>XML response containing league data</returns>
        public async Task<string> GetLeaguesAsync(int? leagueId = null, string? leagueIds = null, int? season = null, string? countryIso = null, int? division = null, int? league = null, int? divisionId = null)
        {
            return await SendRequestAsync("l", new Dictionary<string, string?>
            {
                ["leagueid"] = leagueId?.ToString(),
                ["leagueids"] = leagueIds,
                ["season"] = season?.ToString(),
                ["country_iso"] = countryIso,
                ["division"] = division?.ToString(),
                ["league"] = league?.ToString(),
                ["divisionid"] = divisionId?.ToString()
            });
        }

        /// <summary>
        /// Get team lineup/squad information including starting players and substitutes.
        /// </summary>
        /// <param name="teamId">Required: The team ID to get lineup for</param>
        /// <param name="fixtureId">Optional: Get lineup for a specific fixture (match)</param>
        /// <param name="fixtureIds">Optional: Get lineups for multiple fixtures (comma-separated)</param>
        /// <param name="youth">Optional: Include youth team lineup</param>
        /// <param name="nat">Optional: Include national team lineup</param>
        /// <param name="u20">Optional: Include Under-20 team lineup</param>
        /// <returns>XML response containing lineup data with player positions and numbers</returns>
        public async Task<string> GetLineupsAsync(int teamId, int? fixtureId = null, string? fixtureIds = null, bool youth = false, bool nat = false, bool u20 = false)
        {
            return await SendRequestAsync("li", new Dictionary<string, string?>
            {
                ["teamid"] = teamId.ToString(),
                ["fixtureid"] = fixtureId?.ToString(),
                ["fixtureids"] = fixtureIds,
                ["youth"] = youth ? "1" : null,
                ["nat"] = nat ? "1" : null,
                ["u20"] = u20 ? "1" : null
            });
        }

        /// <summary>
        /// Get in-game mail/messages for the authenticated member.
        /// </summary>
        /// <param name="folderIds">Optional: Get messages from specific folders (comma-separated)</param>
        /// <param name="subfolderIds">Optional: Get messages from specific subfolders (comma-separated)</param>
        /// <param name="messageId">Optional: Get details for a specific message</param>
        /// <returns>XML response containing mail messages and folder information</returns>
        public async Task<string> GetMailAsync(string? folderIds = null, string? subfolderIds = null, int? messageId = null)
        {
            return await SendRequestAsync("m", new Dictionary<string, string?>
            {
                ["folderid"] = folderIds,
                ["subfolderid"] = subfolderIds,
                ["messageid"] = messageId?.ToString()
            });
        }

        /// <summary>
        /// Get live match commentary/play-by-play events for a fixture.
        /// </summary>
        /// <param name="fixtureId">Required: The fixture ID to get commentary for</param>
        /// <param name="youth">Optional: Get commentary for youth match</param>
        /// <param name="nat">Optional: Get commentary for national team match</param>
        /// <param name="u20">Optional: Get commentary for Under-20 match</param>
        /// <returns>XML response containing match events with commentary and scoring details</returns>
        public async Task<string> GetMatchCommentaryAsync(int fixtureId, bool youth = false, bool nat = false, bool u20 = false)
        {
            return await SendRequestAsync("me", new Dictionary<string, string?>
            {
                ["fixtureid"] = fixtureId.ToString(),
                ["youth"] = youth ? "1" : null,
                ["nat"] = nat ? "1" : null,
                ["u20"] = u20 ? "1" : null
            });
        }

        /// <summary>
        /// Get current/live match score for a fixture including game status and time.
        /// </summary>
        /// <param name="fixtureId">Required: The fixture ID to get score for</param>
        /// <param name="youth">Optional: Get score for youth match</param>
        /// <param name="nat">Optional: Get score for national team match</param>
        /// <param name="u20">Optional: Get score for Under-20 match</param>
        /// <returns>XML response containing match status, scores, and game time</returns>
        public async Task<string> GetMatchScoreAsync(int fixtureId, bool youth = false, bool nat = false, bool u20 = false)
        {
            return await SendRequestAsync("ls", new Dictionary<string, string?>
            {
                ["fixtureid"] = fixtureId.ToString(),
                ["youth"] = youth ? "1" : null,
                ["nat"] = nat ? "1" : null,
                ["u20"] = u20 ? "1" : null
            });
        }

        /// <summary>
        /// Get comprehensive match summary including final score, try scorers, and match details.
        /// </summary>
        /// <param name="fixtureId">Optional: Single fixture ID for summary</param>
        /// <param name="fixtureIds">Optional: Get summaries for multiple fixtures (comma-separated)</param>
        /// <param name="youth">Optional: Get summary for youth match</param>
        /// <param name="nat">Optional: Get summary for national team match</param>
        /// <param name="u20">Optional: Get summary for Under-20 match</param>
        /// <returns>XML response containing final match summary with all scoring details</returns>
        public async Task<string> GetMatchSummaryAsync(int? fixtureId = null, string? fixtureIds = null, bool youth = false, bool nat = false, bool u20 = false)
        {
            return await SendRequestAsync("msum", new Dictionary<string, string?>
            {
                ["fixtureid"] = fixtureId?.ToString(),
                ["fixtureids"] = fixtureIds,
                ["youth"] = youth ? "1" : null,
                ["nat"] = nat ? "1" : null,
                ["u20"] = u20 ? "1" : null
            });
        }

        /// <summary>
        /// Get member/player information. Returns different data based on permissions and membership level.
        /// </summary>
        /// <param name="memberId">Optional: Single member ID to retrieve</param>
        /// <param name="memberIds">Optional: Multiple member IDs to retrieve (comma-separated)</param>
        /// <returns>XML response containing member profile data (public or extended based on permissions)</returns>
        public async Task<string> GetMembersAsync(int? memberId = null, string? memberIds = null)
        {
            return await SendRequestAsync("mem", new Dictionary<string, string?>
            {
                ["memberid"] = memberId?.ToString(),
                ["memberids"] = memberIds
            });
        }

        /// <summary>
        /// Get the career history of a player including all teams, transfers, and achievements.
        /// </summary>
        /// <param name="playerId">Required: The player ID to get history for</param>
        /// <returns>XML response containing player's historical records and transactions</returns>
        public async Task<string> GetPlayerHistoryAsync(int playerId)
        {
            return await SendRequestAsync("ph", new Dictionary<string, string?>
            {
                ["playerid"] = playerId.ToString()
            });
        }

        /// <summary>
        /// Get player information including attributes, skills, contract details, and current team.
        /// </summary>
        /// <param name="playerId">Optional: Single player ID</param>
        /// <param name="playerIds">Optional: Multiple player IDs (comma-separated)</param>
        /// <param name="teamId">Optional: Get all players for a specific team</param>
        /// <param name="teamIds">Optional: Get players from multiple teams (comma-separated)</param>
        /// <param name="youth">Optional: Get youth team players</param>
        /// <param name="nat">Optional: Get national team players</param>
        /// <param name="u20">Optional: Get Under-20 team players</param>
        /// <returns>XML response containing player data (public or extended based on permissions)</returns>
        public async Task<string> GetPlayersAsync(int? playerId = null, string? playerIds = null, int? teamId = null, string? teamIds = null, bool youth = false, bool nat = false, bool u20 = false)
        {
            return await SendRequestAsync("p", new Dictionary<string, string?>
            {
                ["playerid"] = playerId?.ToString(),
                ["playerids"] = playerIds,
                ["teamid"] = teamId?.ToString(),
                ["teamids"] = teamIds,
                ["youth"] = youth ? "1" : null,
                ["nat"] = nat ? "1" : null,
                ["u20"] = u20 ? "1" : null
            });
        }

        /// <summary>
        /// Get player statistics optionally filtered by competition type and season.
        /// </summary>
        /// <param name="playerId">Optional: Single player ID to get statistics for</param>
        /// <param name="playerIds">Optional: Multiple player IDs to get statistics for (comma-separated)</param>
        /// <param name="league">Optional: Get league competition statistics only (default: false)</param>
        /// <param name="season">Optional: Get statistics for a specific season year (only works with league, nat, or u20)</param>
        /// <param name="nat">Optional: Get national team statistics only (default: false)</param>
        /// <param name="wc">Optional: Get world cup statistics only - must be combined with nat or u20 (default: false)</param>
        /// <param name="u20">Optional: Get Under-20 statistics only (default: false)</param>
        /// <returns>XML response containing player statistics</returns>
        public async Task<string> GetPlayerStatisticsAsync(int? playerId = null, string? playerIds = null, bool league = false, int? season = null, bool nat = false, bool wc = false, bool u20 = false)
        {
            return await SendRequestAsync("ps", new Dictionary<string, string?>
            {
                ["playerid"] = playerId?.ToString(),
                ["playerids"] = playerIds,
                ["league"] = league ? "1" : null,
                ["season"] = season?.ToString(),
                ["nat"] = nat ? "1" : null,
                ["wc"] = wc ? "1" : null,
                ["u20"] = u20 ? "1" : null
            });
        }

        /// <summary>
        /// Get historical ranking data for teams showing ranking changes over time.
        /// </summary>
        /// <param name="teamId">Optional: Get ranking history for a specific team</param>
        /// <param name="nat">Optional: Get national team ranking history</param>
        /// <param name="u20">Optional: Get Under-20 team ranking history</param>
        /// <param name="countryIso">Optional: Filter by country ISO code</param>
        /// <returns>XML response containing ranking history data</returns>
        public async Task<string> GetRankingHistoryAsync(int? teamId = null, bool nat = false, bool u20 = false, string? countryIso = null)
        {
            return await SendRequestAsync("rkh", new Dictionary<string, string?>
            {
                ["teamid"] = teamId?.ToString(),
                ["nat"] = nat ? "1" : null,
                ["u20"] = u20 ? "1" : null,
                ["iso"] = countryIso
            });
        }

        /// <summary>
        /// Get current team rankings/standings for leagues and competitions.
        /// </summary>
        /// <param name="iso">Optional: Filter by country ISO code</param>
        /// <param name="regionId">Optional: Filter by region ID</param>
        /// <param name="leagueId">Optional: Filter by league ID</param>
        /// <param name="nat">Optional: Get national team rankings</param>
        /// <param name="u20">Optional: Get Under-20 team rankings</param>
        /// <param name="start">Optional: Start position for pagination</param>
        /// <param name="limit">Optional: Limit number of results</param>
        /// <returns>XML response containing current rankings/standings</returns>
        public async Task<string> GetRankingsAsync(string? iso = null, int? regionId = null, int? leagueId = null, bool nat = false, bool u20 = false, int? start = null, int? limit = null)
        {
            return await SendRequestAsync("rk", new Dictionary<string, string?>
            {
                ["iso"] = iso,
                ["regionid"] = regionId?.ToString(),
                ["leagueid"] = leagueId?.ToString(),
                ["nat"] = nat ? "1" : null,
                ["u20"] = u20 ? "1" : null,
                ["start"] = start?.ToString(),
                ["limit"] = limit?.ToString()
            });
        }

        /// <summary>
        /// Get region/province information and geographic divisions within countries.
        /// </summary>
        /// <param name="regionId">Optional: Single region ID</param>
        /// <param name="regionIds">Optional: Multiple region IDs</param>
        /// <param name="countryIso">Optional: Filter by country ISO code</param>
        /// <returns>XML response containing region data</returns>
        public async Task<string> GetRegionsAsync(int? regionId = null, IEnumerable<int>? regionIds = null, string? countryIso = null)
        {
            return await SendRequestAsync("r", new Dictionary<string, string?>
            {
                ["regionid"] = regionId?.ToString(),
                ["regionids"] = regionIds != null ? string.Join(",", regionIds) : null,
                ["country"] = countryIso
            });
        }

        /// <summary>
        /// Get match reporter summaries/commentary from official reporters.
        /// </summary>
        /// <param name="fixtureId">Required: The fixture ID to get reports for</param>
        /// <param name="fixtureIds">Optional: Get reports for multiple fixtures (comma-separated)</param>
        /// <param name="youth">Optional: Get youth match reports</param>
        /// <param name="nat">Optional: Get national team match reports</param>
        /// <param name="u20">Optional: Get Under-20 match reports</param>
        /// <returns>XML response containing match reports from reporters</returns>
        public async Task<string> GetReportersSummaryAsync(int fixtureId, string? fixtureIds = null, bool youth = false, bool nat = false, bool u20 = false)
        {
            return await SendRequestAsync("rs", new Dictionary<string, string?>
            {
                ["fixtureid"] = fixtureId.ToString(),
                ["fixtureids"] = fixtureIds,
                ["youth"] = youth ? "1" : null,
                ["nat"] = nat ? "1" : null,
                ["u20"] = u20 ? "1" : null
            });
        }

        /// <summary>
        /// Get league standings/table showing team positions, wins, losses, and points.
        /// </summary>
        /// <param name="leagueId">Optional: Get standings for a specific league</param>
        /// <param name="youth">Optional: Get youth league standings</param>
        /// <param name="nat">Optional: Get national team standings</param>
        /// <param name="u20">Optional: Get Under-20 standings</param>
        /// <param name="season">Optional: Filter by season</param>
        /// <returns>XML response containing league standings</returns>
        public async Task<string> GetStandingsAsync(int? leagueId = null, bool youth = false, bool nat = false, bool u20 = false, int? season = null)
        {
            return await SendRequestAsync("s", new Dictionary<string, string?>
            {
                ["leagueid"] = leagueId?.ToString(),
                ["youth"] = youth ? "1" : null,
                ["nat"] = nat ? "1" : null,
                ["u20"] = u20 ? "1" : null,
                ["season"] = season?.ToString()
            });
        }

        /// <summary>
        /// Get historical information about a team including name changes and achievements.
        /// </summary>
        /// <param name="teamId">Required: The team ID to get history for</param>
        /// <returns>XML response containing team historical records</returns>
        public async Task<string> GetTeamHistoryAsync(int teamId)
        {
            return await SendRequestAsync("th", new Dictionary<string, string?>
            {
                ["teamid"] = teamId.ToString()
            });
        }

        /// <summary>
        /// Get team information including squad details, achievements, and current status.
        /// </summary>
        /// <param name="teamId">Optional: Single team ID</param>
        /// <param name="teamIds">Optional: Multiple team IDs (comma-separated, max 10)</param>
        /// <param name="regionId">Optional: Get teams from a specific region</param>
        /// <param name="leagueId">Optional: Get teams in a specific league</param>
        /// <param name="nat">Optional: Get national teams</param>
        /// <param name="u20">Optional: Get Under-20 teams</param>
        /// <param name="country">Optional: Filter by country ISO code</param>
        /// <returns>XML response containing team data</returns>
        public async Task<string> GetTeamsAsync(int? teamId = null, string? teamIds = null, int? regionId = null, int? leagueId = null, bool nat = false, bool u20 = false, string? country = null)
        {
            return await SendRequestAsync("t", new Dictionary<string, string?>
            {
                ["teamid"] = teamId?.ToString(),
                ["teamids"] = teamIds,
                ["regionid"] = regionId?.ToString(),
                ["leagueid"] = leagueId?.ToString(),
                ["nat"] = nat ? "1" : null,
                ["u20"] = u20 ? "1" : null,
                ["country"] = country
            });
        }

        /// <summary>
        /// Get aggregate team statistics including overall performance metrics and records.
        /// </summary>
        /// <param name="teamId">Optional: Single team ID for statistics</param>
        /// <param name="teamIds">Optional: Multiple team IDs for statistics</param>
        /// <param name="nat">Optional: Get national team statistics</param>
        /// <param name="wc">Optional: Get world cup statistics</param>
        /// <param name="u20">Optional: Get Under-20 statistics</param>
        /// <returns>XML response containing team statistics</returns>
        public async Task<string> GetTeamStatisticsAsync(int? teamId = null, IEnumerable<int>? teamIds = null, bool nat = false, bool wc = false, bool u20 = false)
        {
            return await SendRequestAsync("ts", new Dictionary<string, string?>
            {
                ["teamid"] = teamId?.ToString(),
                ["teamids"] = teamIds != null ? string.Join(",", teamIds) : null,
                ["nat"] = nat ? "1" : null,
                ["wc"] = wc ? "1" : null,
                ["u20"] = u20 ? "1" : null
            });
        }

        /// <summary>
        /// Get training instructions for the authenticated team member.
        /// </summary>
        /// <param name="memberId">Required: Member ID for authentication</param>
        /// <param name="memberKey">Required: Member key/password for authentication</param>
        /// <returns>XML response containing team training instructions</returns>
        public async Task<string> GetTrainingInstructionsAsync(int memberId, string? memberKey)
        {
            return await SendRequestAsync("ti", new Dictionary<string, string?>
            {
                ["memberid"] = memberId.ToString(),
                ["memberkey"] = memberKey
            });
        }

        /// <summary>
        /// Get training reports showing player development and skill improvements.
        /// </summary>
        /// <param name="memberId">Required: Member ID for authentication</param>
        /// <param name="memberKey">Required: Member key/password for authentication</param>
        /// <param name="season">Optional: Filter by season</param>
        /// <param name="round">Optional: Filter by round</param>
        /// <returns>XML response containing training reports</returns>
        public async Task<string> GetTrainingReportsAsync(int memberId, string? memberKey, int? season = null, int? round = null)
        {
            return await SendRequestAsync("tr", new Dictionary<string, string?>
            {
                ["memberid"] = memberId.ToString(),
                ["memberkey"] = memberKey,
                ["season"] = season?.ToString(),
                ["round"] = round?.ToString()
            });
        }

        /// <summary>
        /// Get players available on the transfer market with filtering and sorting options.
        /// </summary>
        /// <param name="sortBy">Optional: Field to sort by (e.g., "price", "age", "csr")</param>
        /// <param name="sortOrder">Optional: Sort order ("asc" or "desc")</param>
        /// <param name="offset">Optional: Pagination offset</param>
        /// <param name="limit">Optional: Maximum number of results</param>
        /// <param name="teamId">Optional: Filter to players from specific team</param>
        /// <param name="priceMin">Optional: Minimum asking price filter</param>
        /// <param name="priceMax">Optional: Maximum asking price filter</param>
        /// <param name="ageMin">Optional: Minimum player age filter</param>
        /// <param name="ageMax">Optional: Maximum player age filter</param>
        /// <param name="csrMin">Optional: Minimum CSR (rating) filter</param>
        /// <param name="csrMax">Optional: Maximum CSR (rating) filter</param>
        /// <param name="nationality">Optional: Filter by player nationality</param>
        /// <param name="skillFilters">Optional: Dictionary of skill filters by skill ID</param>
        /// <returns>XML response containing available transfer market players</returns>
        public async Task<string> GetTransferMarketAsync(string? sortBy = null, string? sortOrder = null, int? offset = null, int? limit = null, int? teamId = null, int? priceMin = null, int? priceMax = null, int? ageMin = null, int? ageMax = null, int? csrMin = null, int? csrMax = null, string? nationality = null, IDictionary<int, string>? skillFilters = null)
        {
            var parameters = new Dictionary<string, string?>
            {
                ["sortby"] = sortBy,
                ["sortorder"] = sortOrder,
                ["offset"] = offset?.ToString(),
                ["limit"] = limit?.ToString(),
                ["teamid"] = teamId?.ToString(),
                ["priceMin"] = priceMin?.ToString(),
                ["priceMax"] = priceMax?.ToString(),
                ["ageMin"] = ageMin?.ToString(),
                ["ageMax"] = ageMax?.ToString(),
                ["csrMin"] = csrMin?.ToString(),
                ["csrMax"] = csrMax?.ToString(),
                ["nationality"] = nationality
            };

            if (skillFilters != null)
            {
                foreach (var pair in skillFilters)
                {
                    parameters[$"skill{pair.Key}"] = pair.Value;
                }
            }

            return await SendRequestAsync("tm", parameters);
        }

        private async Task<string> SendRequestAsync(string requestCode, IDictionary<string, string?> parameters)
        {
            var queryParameters = new Dictionary<string, string>
            {
                ["r"] = requestCode
            };

            if (_credentials?.DeveloperId is int developerId)
            {
                queryParameters["d"] = developerId.ToString();
            }

            if (!string.IsNullOrWhiteSpace(_credentials?.DeveloperKey))
            {
                queryParameters["dk"] = _credentials.DeveloperKey;
            }

            if (_credentials?.MemberId is int memberId)
            {
                queryParameters["memberid"] = memberId.ToString();
            }

            if (!string.IsNullOrWhiteSpace(_credentials?.MemberKey))
            {
                queryParameters["memberkey"] = _credentials.MemberKey;
            }

            if (parameters != null)
            {
                foreach (var parameter in parameters)
                {
                    if (!string.IsNullOrWhiteSpace(parameter.Value))
                    {
                        queryParameters[parameter.Key] = parameter.Value;
                    }
                }
            }

            var queryString = BuildQueryString(queryParameters);
            var requestUri = new Uri($"{_baseEndpoint}?{queryString}");
            return await _httpClient.GetStringAsync(requestUri).ConfigureAwait(false);
        }

        private static string BuildQueryString(IDictionary<string, string> parameters)
        {
            var builder = new StringBuilder();
            foreach (var parameter in parameters)
            {
                if (builder.Length > 0)
                {
                    builder.Append('&');
                }

                builder.Append(Uri.EscapeDataString(parameter.Key));
                builder.Append('=');
                builder.Append(Uri.EscapeDataString(parameter.Value ?? string.Empty));
            }

            return builder.ToString();
        }
    }
}
