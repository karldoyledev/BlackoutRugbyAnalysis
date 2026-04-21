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

        public async Task<string> GetCountriesAsync(string iso = null, IEnumerable<string> isos = null)
        {
            return await SendRequestAsync("c", new Dictionary<string, string>
            {
                ["iso"] = iso,
                ["isos"] = isos != null ? string.Join(",", isos) : null
            });
        }

        // Date and time are returned in every API response root via the BRT attributes.
        // There is no dedicated standalone request type for Date and Time.

        public async Task<string> GetDivisionsAsync(int? divisionId = null, IEnumerable<int> divisionIds = null, int? season = null, string countryIso = null, int? division = null)
        {
            return await SendRequestAsync("d", new Dictionary<string, string>
            {
                ["divisionid"] = divisionId?.ToString(),
                ["divisionids"] = divisionIds != null ? string.Join(",", divisionIds) : null,
                ["season"] = season?.ToString(),
                ["country_iso"] = countryIso,
                ["division"] = division?.ToString()
            });
        }

        public async Task<string> GetFinancesAsync(int season, int round)
        {
            return await SendRequestAsync("fi", new Dictionary<string, string>
            {
                ["season"] = season.ToString(),
                ["round"] = round.ToString()
            });
        }

        public async Task<string> GetFixturesAsync(int? fixtureId = null, IEnumerable<int> fixtureIds = null, int? teamId = null, int? last = null, int? futureTeamId = null, int? pastTeamId = null, bool youth = false, bool nat = false, bool u20 = false, int? latestLeagueId = null, int? leagueId = null, int? season = null, int? round = null, bool roundRobin = false, int? friendlyCompId = null)
        {
            return await SendRequestAsync("f", new Dictionary<string, string>
            {
                ["fixtureid"] = fixtureId?.ToString(),
                ["fixtureids"] = fixtureIds != null ? string.Join(",", fixtureIds) : null,
                ["teamid"] = teamId?.ToString(),
                ["last"] = last?.ToString(),
                ["future"] = futureTeamId?.ToString(),
                ["past"] = pastTeamId?.ToString(),
                ["youth"] = youth ? "1" : null,
                ["nat"] = nat ? "1" : null,
                ["u20"] = u20 ? "1" : null,
                ["latest"] = latestLeagueId?.ToString(),
                ["leagueid"] = leagueId?.ToString(),
                ["season"] = season?.ToString(),
                ["round"] = round?.ToString(),
                ["roundrobin"] = roundRobin ? "1" : null,
                ["friendlycompid"] = friendlyCompId?.ToString()
            });
        }

        public async Task<string> GetFixtureStatisticsAsync(int? fixtureId = null, IEnumerable<int> fixtureIds = null, int? teamStats = null, int? teamPlayersStats = null, int? playerStats = null)
        {
            return await SendRequestAsync("fs", new Dictionary<string, string>
            {
                ["fixtureid"] = fixtureId?.ToString(),
                ["fixtureids"] = fixtureIds != null ? string.Join(",", fixtureIds) : null,
                ["teamstats"] = teamStats?.ToString(),
                ["teamplayersstats"] = teamPlayersStats?.ToString(),
                ["playerstats"] = playerStats?.ToString()
            });
        }

        public async Task<string> GetLeaguesAsync(int? leagueId = null, IEnumerable<int> leagueIds = null, int? season = null, string countryIso = null, int? division = null, int? league = null, int? divisionId = null)
        {
            return await SendRequestAsync("l", new Dictionary<string, string>
            {
                ["leagueid"] = leagueId?.ToString(),
                ["leagueids"] = leagueIds != null ? string.Join(",", leagueIds) : null,
                ["season"] = season?.ToString(),
                ["country_iso"] = countryIso,
                ["division"] = division?.ToString(),
                ["league"] = league?.ToString(),
                ["divisionid"] = divisionId?.ToString()
            });
        }

        public async Task<string> GetLineupsAsync(int teamId, int? fixtureId = null, IEnumerable<int> fixtureIds = null, bool youth = false, bool nat = false, bool u20 = false)
        {
            return await SendRequestAsync("lu", new Dictionary<string, string>
            {
                ["teamid"] = teamId.ToString(),
                ["fixtureid"] = fixtureId?.ToString(),
                ["fixtureids"] = fixtureIds != null ? string.Join(",", fixtureIds) : null,
                ["youth"] = youth ? "1" : null,
                ["nat"] = nat ? "1" : null,
                ["u20"] = u20 ? "1" : null
            });
        }

        public async Task<string> GetMailAsync(IEnumerable<int> folderIds = null, IEnumerable<int> subfolderIds = null, int? messageId = null)
        {
            return await SendRequestAsync("ma", new Dictionary<string, string>
            {
                ["folderid"] = folderIds != null ? string.Join(",", folderIds) : null,
                ["subfolderid"] = subfolderIds != null ? string.Join(",", subfolderIds) : null,
                ["messageid"] = messageId?.ToString()
            });
        }

        public async Task<string> GetMatchCommentaryAsync(int fixtureId, bool youth = false, bool nat = false, bool u20 = false)
        {
            return await SendRequestAsync("me", new Dictionary<string, string>
            {
                ["fixtureid"] = fixtureId.ToString(),
                ["youth"] = youth ? "1" : null,
                ["nat"] = nat ? "1" : null,
                ["u20"] = u20 ? "1" : null
            });
        }

        public async Task<string> GetMatchScoreAsync(int fixtureId, bool youth = false, bool nat = false, bool u20 = false)
        {
            return await SendRequestAsync("ls", new Dictionary<string, string>
            {
                ["fixtureid"] = fixtureId.ToString(),
                ["youth"] = youth ? "1" : null,
                ["nat"] = nat ? "1" : null,
                ["u20"] = u20 ? "1" : null
            });
        }

        public async Task<string> GetMatchSummaryAsync(int fixtureId, IEnumerable<int> fixtureIds = null, bool youth = false, bool nat = false, bool u20 = false)
        {
            return await SendRequestAsync("ms", new Dictionary<string, string>
            {
                ["fixtureid"] = fixtureId.ToString(),
                ["fixtureids"] = fixtureIds != null ? string.Join(",", fixtureIds) : null,
                ["youth"] = youth ? "1" : null,
                ["nat"] = nat ? "1" : null,
                ["u20"] = u20 ? "1" : null
            });
        }

        public async Task<string> GetMembersAsync(int? memberId = null, IEnumerable<int> memberIds = null)
        {
            return await SendRequestAsync("m", new Dictionary<string, string>
            {
                ["memberid"] = memberId?.ToString(),
                ["memberids"] = memberIds != null ? string.Join(",", memberIds) : null
            });
        }

        public async Task<string> GetPlayerHistoryAsync(int playerId)
        {
            return await SendRequestAsync("ph", new Dictionary<string, string>
            {
                ["playerid"] = playerId.ToString()
            });
        }

        public async Task<string> GetPlayersAsync(int? playerId = null, IEnumerable<int> playerIds = null, int? teamId = null, bool youth = false, bool nat = false, bool u20 = false)
        {
            return await SendRequestAsync("p", new Dictionary<string, string>
            {
                ["playerid"] = playerId?.ToString(),
                ["playerids"] = playerIds != null ? string.Join(",", playerIds) : null,
                ["teamid"] = teamId?.ToString(),
                ["youth"] = youth ? "1" : null,
                ["nat"] = nat ? "1" : null,
                ["u20"] = u20 ? "1" : null
            });
        }

        public async Task<string> GetPlayerStatisticsAsync(int playerId, bool league = false, bool nat = false, bool wc = false, bool u20 = false)
        {
            return await SendRequestAsync("ps", new Dictionary<string, string>
            {
                ["playerid"] = playerId.ToString(),
                ["league"] = league ? "1" : null,
                ["nat"] = nat ? "1" : null,
                ["wc"] = wc ? "1" : null,
                ["u20"] = u20 ? "1" : null
            });
        }

        public async Task<string> GetRankingHistoryAsync(int? teamId = null, bool nat = false, bool u20 = false, string countryIso = null)
        {
            return await SendRequestAsync("rkh", new Dictionary<string, string>
            {
                ["teamid"] = teamId?.ToString(),
                ["nat"] = nat ? "1" : null,
                ["u20"] = u20 ? "1" : null,
                ["iso"] = countryIso
            });
        }

        public async Task<string> GetRankingsAsync(string iso = null, int? regionId = null, int? leagueId = null, bool nat = false, bool u20 = false, int? start = null, int? limit = null)
        {
            return await SendRequestAsync("rk", new Dictionary<string, string>
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

        public async Task<string> GetRegionsAsync(int? regionId = null, IEnumerable<int> regionIds = null, string countryIso = null)
        {
            return await SendRequestAsync("r", new Dictionary<string, string>
            {
                ["regionid"] = regionId?.ToString(),
                ["regionids"] = regionIds != null ? string.Join(",", regionIds) : null,
                ["country"] = countryIso
            });
        }

        public async Task<string> GetReportersSummaryAsync(int fixtureId, IEnumerable<int> fixtureIds = null, bool youth = false, bool nat = false, bool u20 = false)
        {
            return await SendRequestAsync("rs", new Dictionary<string, string>
            {
                ["fixtureid"] = fixtureId.ToString(),
                ["fixtureids"] = fixtureIds != null ? string.Join(",", fixtureIds) : null,
                ["youth"] = youth ? "1" : null,
                ["nat"] = nat ? "1" : null,
                ["u20"] = u20 ? "1" : null
            });
        }

        public async Task<string> GetStandingsAsync(int? leagueId = null, bool youth = false, bool nat = false, bool u20 = false, int? season = null)
        {
            return await SendRequestAsync("s", new Dictionary<string, string>
            {
                ["leagueid"] = leagueId?.ToString(),
                ["youth"] = youth ? "1" : null,
                ["nat"] = nat ? "1" : null,
                ["u20"] = u20 ? "1" : null,
                ["season"] = season?.ToString()
            });
        }

        public async Task<string> GetTeamHistoryAsync(int teamId)
        {
            return await SendRequestAsync("th", new Dictionary<string, string>
            {
                ["teamid"] = teamId.ToString()
            });
        }

        public async Task<string> GetTeamsAsync(int? teamId = null, IEnumerable<int> teamIds = null, int? regionId = null, int? leagueId = null, bool nat = false, bool u20 = false, string countryIso = null)
        {
            return await SendRequestAsync("t", new Dictionary<string, string>
            {
                ["teamid"] = teamId?.ToString(),
                ["teamids"] = teamIds != null ? string.Join(",", teamIds) : null,
                ["regionid"] = regionId?.ToString(),
                ["leagueid"] = leagueId?.ToString(),
                ["nat"] = nat ? "1" : null,
                ["u20"] = u20 ? "1" : null,
                ["country"] = countryIso
            });
        }

        public async Task<string> GetTeamStatisticsAsync(int? teamId = null, IEnumerable<int> teamIds = null, bool nat = false, bool wc = false, bool u20 = false)
        {
            return await SendRequestAsync("ts", new Dictionary<string, string>
            {
                ["teamid"] = teamId?.ToString(),
                ["teamids"] = teamIds != null ? string.Join(",", teamIds) : null,
                ["nat"] = nat ? "1" : null,
                ["wc"] = wc ? "1" : null,
                ["u20"] = u20 ? "1" : null
            });
        }

        public async Task<string> GetTrainingInstructionsAsync(int memberId, string memberKey)
        {
            return await SendRequestAsync("ti", new Dictionary<string, string>
            {
                ["memberid"] = memberId.ToString(),
                ["memberkey"] = memberKey
            });
        }

        public async Task<string> GetTrainingReportsAsync(int memberId, string memberKey, int? season = null, int? round = null)
        {
            return await SendRequestAsync("tr", new Dictionary<string, string>
            {
                ["memberid"] = memberId.ToString(),
                ["memberkey"] = memberKey,
                ["season"] = season?.ToString(),
                ["round"] = round?.ToString()
            });
        }

        public async Task<string> GetTransferMarketAsync(string sortBy = null, string sortOrder = null, int? offset = null, int? limit = null, int? teamId = null, int? priceMin = null, int? priceMax = null, int? ageMin = null, int? ageMax = null, int? csrMin = null, int? csrMax = null, string nationality = null, IDictionary<int, string> skillFilters = null)
        {
            var parameters = new Dictionary<string, string>
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

        private async Task<string> SendRequestAsync(string requestCode, IDictionary<string, string> parameters)
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
