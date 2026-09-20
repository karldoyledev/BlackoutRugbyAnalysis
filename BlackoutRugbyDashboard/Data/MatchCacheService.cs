using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using BlackoutRugbyDashboard.Services;
using Microsoft.EntityFrameworkCore;

namespace BlackoutRugbyDashboard.Data;

/// <summary>Outcome of one Home window fill: what the window held and what it cost.</summary>
public record HomeWindowFillResult(int WindowSize, int Completed, int NewlyCached, int ApiCalls);

/// <summary>
/// Outcome of one Squad window fill: the discovery window, what it cost, and the
/// parsed display fixtures (names + scores for the page's labels and results).
/// ApiCalls counts discovery reads plus 5 per attempted fixture fill (the D1
/// entry scope); a fill that fails mid-way may have made fewer.
/// </summary>
public sealed record SquadWindowFillResult(
    int WindowSize,
    int Completed,
    int NewlyCached,
    int AlreadyCached,
    int Unplayed,
    int Failed,
    int ApiCalls,
    IReadOnlyList<FixtureRow> CompletedRows,
    IReadOnlyList<Fixture> Fixtures);

/// <summary>
/// Outcome of one season-stats (ps) get-or-refresh: how many requested players
/// were served from cache, refreshed live, or failed (failures keep their
/// cached row — stale beats broken).
/// </summary>
public sealed record PlayerSeasonRefreshResult(
    int Served,
    int Refreshed,
    int Failed,
    string? FirstError,
    IReadOnlyDictionary<int, PlayerStatistics> Stats);

/// <summary>The cached rows one completed Fixture contributes to the Squad page.</summary>
public sealed record SquadFixtureRows(
    FixtureRow Fixture,
    MatchSummaryRow? Summary,
    IReadOnlyList<PlayerFixtureRow> Players,
    TeamFixtureStatRow? TeamStats);

/// <summary>The Match Cache's parsed-row tables; the manual per-table reset targets (D1 §5).</summary>
public enum CacheTable
{
    Fixtures,
    MatchSummaries,
    PlayerFixtures,
    TeamFixtureStats,
    PlayerSeasons,
    TeamFacts
}

/// <summary>
/// The Match Cache fill service (spec §Match Cache / D1): fetch → archive raw →
/// parse → persist. Append-only: completed Fixtures are never re-fetched,
/// unplayed Fixtures are never cached, and Home's ms/t batches run only for
/// what is missing. Team facts are captured when seen, on every window refresh.
/// The Squad window fill (D-Squad) reuses the same entry scope per fixture and
/// splits fetch (concurrent, HTTP + raw archive only) from persist (sequential,
/// the only DbContext-touching phase).
/// </summary>
public class MatchCacheService(
    IBlackoutRugbyApiClient api,
    BlackoutRugbyResponseAdapter adapter,
    DashboardDbContext db,
    MatchCacheRawStore rawStore)
{
    private const int SquadFillConcurrency = 4;
    private const int PlayerSeasonConcurrency = 6;
    private const int CallsPerFixtureFill = 5; // f, ms, squad-fs, bare-fs, lu — the D1 entry scope

    private static readonly JsonSerializerOptions JsonOptions = new();

    /// <summary>
    /// Fills the Home last-8 window: one `f` read (always, the window moves),
    /// one batched `ms` read + one batched `t` read for what is missing —
    /// a fully-cached window costs 1 call (D5).
    /// </summary>
    public async Task<HomeWindowFillResult> FillHomeWindowAsync(int teamId, int last = 8, CancellationToken cancellationToken = default)
    {
        var calls = 0;

        var fixturesXml = await api.GetFixturesAsync(teamId: teamId, last: last);
        calls++;
        rawStore.Save("f", $"team{teamId}-last{last}", fixturesXml);

        var records = adapter.ParseFixtureRecords(fixturesXml);
        var completed = records.Where(row => row.MatchFinishUnix > 0).ToList();

        var cachedIds = await db.Fixtures
            .Select(row => row.FixtureId)
            .ToListAsync(cancellationToken);
        var newRows = completed.Where(row => !cachedIds.Contains(row.FixtureId)).ToList();
        db.Fixtures.AddRange(newRows);

        if (newRows.Count > 0)
        {
            var fixtureIds = string.Join(',', newRows.Select(row => row.FixtureId));
            var msXml = await api.GetMatchSummaryAsync(fixtureIds: fixtureIds);
            calls++;
            rawStore.Save("ms", fixtureIds, msXml);
            foreach (var summary in adapter.ParseMatchSummaries(msXml))
            {
                AddSummary(summary);
            }
        }

        var opponents = completed
            .Select(row => row.HomeTeamId == teamId ? row.GuestTeamId : row.HomeTeamId)
            .Where(id => id > 0 && id != teamId)
            .Distinct()
            .OrderBy(id => id)
            .ToList();
        if (opponents.Count > 0)
        {
            var teamIds = string.Join(',', opponents);
            var teamsXml = await api.GetTeamsAsync(teamIds: teamIds);
            calls++;
            rawStore.Save("t", teamIds, teamsXml);
            var capturedAt = DateTime.UtcNow;
            foreach (var team in adapter.ParseTeams(teamsXml))
            {
                db.TeamFacts.Add(new TeamFactRow
                {
                    TeamId = team.Id,
                    CapturedAt = capturedAt,
                    Name = team.Name,
                    CountryIso = team.CountryIso,
                    Bot = team.Bot,
                    AverageTop15Csr = team.AverageTop15Csr,
                    RankingPoints = team.RankingPoints,
                    LeagueId = team.LeagueId,
                    RegionalRank = team.RegionalRank,
                    NationalRank = team.NationalRank,
                    WorldRank = team.WorldRank
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return new HomeWindowFillResult(last, completed.Count, newRows.Count, calls);
    }

    /// <summary>
    /// Fills the full D1 entry scope for one Fixture (`f`, `ms`, our-squad `fs`,
    /// bare-`fs` both sides, `lu` — 5 calls). Completed Fixtures are never
    /// re-fetched; unplayed Fixtures are never cached. Returns false when
    /// nothing was cached (already cached, unplayed, or unknown).
    /// </summary>
    public async Task<bool> FillFixtureAsync(int fixtureId, int teamId, CancellationToken cancellationToken = default)
    {
        if (await db.Fixtures.AnyAsync(row => row.FixtureId == fixtureId, cancellationToken))
        {
            return false;
        }

        var data = await FetchFixtureDataAsync(fixtureId, teamId);
        if (data is null)
        {
            return false;
        }

        PersistFixtureData(data);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private sealed record FixtureFillData(
        FixtureRow Fixture,
        IReadOnlyList<MatchSummary> Summaries,
        IReadOnlyList<PlayerFixtureRow> PlayerFixtures,
        IReadOnlyList<TeamFixtureStatRow> TeamFixtureStats);

    /// <summary>
    /// The fetch phase of a fixture fill: the 5 D1 entry-scope reads plus their
    /// raw archives, parsed — the DbContext is never touched, so callers may run
    /// this concurrently. Returns null when the fixture is unplayed or unknown.
    /// </summary>
    private async Task<FixtureFillData?> FetchFixtureDataAsync(
        int fixtureId, int teamId, string? baseEndpoint = null, ApiLogger? apiLogger = null)
    {
        var sw = Stopwatch.StartNew();
        var fixturesXml = await api.GetFixturesAsync(fixtureId: fixtureId);
        var fixturesMs = sw.ElapsedMilliseconds;
        rawStore.Save("f", $"fixture{fixtureId}", fixturesXml);
        var record = adapter.ParseFixtureRecords(fixturesXml).FirstOrDefault(row => row.FixtureId == fixtureId);
        if (record is null || record.MatchFinishUnix == 0)
        {
            return null;
        }

        sw.Restart();
        var summariesXml = await api.GetMatchSummaryAsync(fixtureId: fixtureId);
        var summariesMs = sw.ElapsedMilliseconds;
        rawStore.Save("ms", fixtureId.ToString(CultureInfo.InvariantCulture), summariesXml);

        sw.Restart();
        var squadFsXml = await api.GetFixtureStatisticsAsync(fixtureId, teamPlayersStats: teamId);
        var squadFsMs = sw.ElapsedMilliseconds;
        rawStore.Save("fs-teamplayers", fixtureId.ToString(CultureInfo.InvariantCulture), squadFsXml);

        sw.Restart();
        var bareFsXml = await api.GetFixtureStatisticsAsync(fixtureId);
        var bareFsMs = sw.ElapsedMilliseconds;
        rawStore.Save("fs-bare", fixtureId.ToString(CultureInfo.InvariantCulture), bareFsXml);

        sw.Restart();
        var lineupXml = await api.GetLineupsAsync(teamId, fixtureId: fixtureId);
        var lineupMs = sw.ElapsedMilliseconds;
        rawStore.Save("lu", fixtureId.ToString(CultureInfo.InvariantCulture), lineupXml);
        // D1 caches the lineup read as raw XML; a parsed lineup row type joins
        // with the Match Analysis slice (D6), which owns the team-sheet rendering.

        if (apiLogger is not null && !string.IsNullOrWhiteSpace(baseEndpoint))
        {
            apiLogger.LogResponse($"{baseEndpoint}/fixtures?fixtureId={fixtureId}", 200, Truncate(fixturesXml), fixturesMs);
            apiLogger.LogResponse($"{baseEndpoint}/matchsummary?fixtureId={fixtureId}", 200, Truncate(summariesXml), summariesMs);
            apiLogger.LogResponse($"{baseEndpoint}/fixturestats?fixtureId={fixtureId}&teamPlayersStats={teamId}", 200, Truncate(squadFsXml), squadFsMs);
            apiLogger.LogResponse($"{baseEndpoint}/fixturestats?fixtureId={fixtureId}", 200, Truncate(bareFsXml), bareFsMs);
            apiLogger.LogResponse($"{baseEndpoint}/lineups?teamId={teamId}&fixtureId={fixtureId}", 200, Truncate(lineupXml), lineupMs);
        }

        return new FixtureFillData(
            record,
            adapter.ParseMatchSummaries(summariesXml),
            adapter.ParsePlayerFixtureStats(squadFsXml),
            adapter.ParseTeamFixtureStats(bareFsXml));
    }

    /// <summary>The persist phase of a fixture fill — the only DbContext-touching part.</summary>
    private void PersistFixtureData(FixtureFillData data)
    {
        db.Fixtures.Add(data.Fixture);
        foreach (var summary in data.Summaries)
        {
            AddSummary(summary);
        }
        db.PlayerFixtures.AddRange(data.PlayerFixtures);
        db.TeamFixtureStats.AddRange(data.TeamFixtureStats);
    }

    /// <summary>
    /// Fills the Squad window (cache-first, D-Squad): one live `f` discovery read
    /// (always — the window moves, including the page's no-fixtures-for-season
    /// fallback), then the D1 entry scope for every completed fixture missing
    /// from the cache. The fetch phase runs throttled-concurrent (HTTP + raw
    /// archive only); the DbContext is touched in the sequential persist phase.
    /// </summary>
    public async Task<SquadWindowFillResult> FillSquadWindowAsync(
        int teamId,
        int season,
        int last = 20,
        string? baseEndpoint = null,
        ApiLogger? apiLogger = null,
        CancellationToken cancellationToken = default)
    {
        var calls = 0;

        var url = $"{baseEndpoint}/fixtures?teamId={teamId}&last={last}&season={season}";
        apiLogger?.LogRequest("GET", url);
        var sw = Stopwatch.StartNew();
        var fixturesXml = await api.GetFixturesAsync(teamId: teamId, last: last, season: season);
        calls++;
        rawStore.Save("f", $"team{teamId}-s{season}-last{last}", fixturesXml);
        apiLogger?.LogResponse(url, 200, Truncate(fixturesXml), sw.ElapsedMilliseconds);

        var discoveryError = adapter.ExtractResponseError(fixturesXml);
        if (!string.IsNullOrWhiteSpace(discoveryError)
            && discoveryError.Contains("No fixtures found", StringComparison.OrdinalIgnoreCase))
        {
            url = $"{baseEndpoint}/fixtures?teamId={teamId}&last={last}";
            apiLogger?.LogRequest("GET", url);
            sw.Restart();
            fixturesXml = await api.GetFixturesAsync(teamId: teamId, last: last);
            calls++;
            rawStore.Save("f", $"team{teamId}-last{last}", fixturesXml);
            apiLogger?.LogResponse(url, 200, Truncate(fixturesXml), sw.ElapsedMilliseconds);
        }

        var records = adapter.ParseFixtureRecords(fixturesXml);
        var displayFixtures = adapter.ParseFixtures(fixturesXml);
        var completed = records.Where(row => row.MatchFinishUnix > 0).ToList();
        var unplayed = records.Count - completed.Count;

        var cachedIds = await db.Fixtures
            .Select(row => row.FixtureId)
            .ToListAsync(cancellationToken);
        var missing = completed.Where(row => !cachedIds.Contains(row.FixtureId)).ToList();

        var fetched = new ConcurrentDictionary<int, FixtureFillData>();
        var failedFills = 0;
        string? firstFillError = null;
        if (missing.Count > 0)
        {
            using var gate = new SemaphoreSlim(SquadFillConcurrency);
            var attempts = missing.Select(async row =>
            {
                await gate.WaitAsync(cancellationToken);
                try
                {
                    var data = await FetchFixtureDataAsync(row.FixtureId, teamId, baseEndpoint, apiLogger);
                    if (data is not null)
                    {
                        fetched[row.FixtureId] = data;
                    }
                    else
                    {
                        Interlocked.Increment(ref failedFills);
                        firstFillError ??= $"fixture {row.FixtureId} unexpectedly returned no completed record";
                    }
                }
                catch (Exception exception)
                {
                    Interlocked.Increment(ref failedFills);
                    firstFillError ??= exception.Message;
                    apiLogger?.LogError($"{baseEndpoint}/fixturestats?fixtureId={row.FixtureId}", exception.Message);
                }
                finally
                {
                    gate.Release();
                }
            });
            await Task.WhenAll(attempts);
        }

        var newlyCached = 0;
        foreach (var row in missing)
        {
            if (fetched.TryGetValue(row.FixtureId, out var data))
            {
                PersistFixtureData(data);
                newlyCached++;
            }
        }
        await db.SaveChangesAsync(cancellationToken);

        return new SquadWindowFillResult(
            records.Count,
            completed.Count,
            newlyCached,
            completed.Count - missing.Count,
            unplayed,
            failedFills,
            calls + missing.Count * CallsPerFixtureFill,
            completed,
            displayFixtures);
    }

    /// <summary>
    /// Season aggregates (ps) for the dashboard squad table: point-in-time
    /// PlayerSeasons rows (D1), refreshed only when a newer completed fixture
    /// exists than a row's FetchedAt (the event-driven staleness rule) or the
    /// row is missing. A failed fetch keeps the cached row and is counted for
    /// the caller's warning — a stale season read beats a broken page.
    /// </summary>
    public async Task<PlayerSeasonRefreshResult> GetOrRefreshPlayerSeasonsAsync(
        IReadOnlyList<int> playerIds,
        int season,
        DateTime? newestCompletedUtc,
        string? baseEndpoint = null,
        ApiLogger? apiLogger = null,
        CancellationToken cancellationToken = default)
    {
        var ids = playerIds.Distinct().ToList();
        var existing = await db.PlayerSeasons
            .Where(row => ids.Contains(row.PlayerId) && row.Season == season)
            .ToDictionaryAsync(row => row.PlayerId, cancellationToken);

        var staleIds = ids
            .Where(id => !existing.ContainsKey(id)
                         || (newestCompletedUtc.HasValue && existing[id].FetchedAt < newestCompletedUtc.Value))
            .ToList();

        var refreshed = new ConcurrentDictionary<int, PlayerStatistics>();
        var failed = 0;
        string? firstError = null;
        if (staleIds.Count > 0)
        {
            using var gate = new SemaphoreSlim(PlayerSeasonConcurrency);
            var reads = staleIds.Select(async playerId =>
            {
                await gate.WaitAsync(cancellationToken);
                try
                {
                    var url = $"{baseEndpoint}/playerstatistics?playerid={playerId}&season={season}";
                    apiLogger?.LogRequest("GET", url);
                    var sw = Stopwatch.StartNew();
                    var xml = await api.GetPlayerStatisticsAsync(playerId, season);
                    apiLogger?.LogResponse(url, 200, Truncate(xml), sw.ElapsedMilliseconds);
                    rawStore.Save("ps", $"{playerId}-s{season}", xml);

                    var stats = adapter.ParsePlayerStatistics(playerId, xml);
                    if (stats is null)
                    {
                        var error = adapter.ExtractResponseError(xml) ?? "no statistics element in the response";
                        Interlocked.Increment(ref failed);
                        firstError ??= error;
                        apiLogger?.LogError(url, error);
                    }
                    else
                    {
                        refreshed[playerId] = stats;
                    }
                }
                catch (Exception exception)
                {
                    Interlocked.Increment(ref failed);
                    firstError ??= exception.Message;
                    apiLogger?.LogError($"{baseEndpoint}/playerstatistics?playerid={playerId}", exception.Message);
                }
                finally
                {
                    gate.Release();
                }
            });
            await Task.WhenAll(reads);
        }

        foreach (var (playerId, stats) in refreshed)
        {
            if (existing.TryGetValue(playerId, out var row))
            {
                CopySeasonStatistics(row, stats);
            }
            else
            {
                row = new PlayerSeasonRow { PlayerId = playerId, Season = season };
                CopySeasonStatistics(row, stats);
                db.PlayerSeasons.Add(row);
                existing[playerId] = row;
            }
        }

        if (refreshed.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        var statsById = existing.ToDictionary(pair => pair.Key, pair => ToSeasonStatistics(pair.Value));
        return new PlayerSeasonRefreshResult(ids.Count - staleIds.Count, refreshed.Count, failed, firstError, statsById);
    }

    /// <summary>
    /// The cached rows for a window of completed fixtures, keyed by fixture id:
    /// per-player fs rows (our side), our bare-fs team row, the score-bearing
    /// summary, and the fixture metadata row. The Squad page renders from this —
    /// zero API calls on a warm cache.
    /// </summary>
    public async Task<IReadOnlyDictionary<int, SquadFixtureRows>> GetSquadWindowRowsAsync(
        IReadOnlyCollection<int> fixtureIds, int teamId, CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<int, SquadFixtureRows>();
        var ids = fixtureIds.ToList();
        if (ids.Count == 0)
        {
            return result;
        }

        var fixtures = await db.Fixtures
            .Where(row => ids.Contains(row.FixtureId))
            .ToDictionaryAsync(row => row.FixtureId, cancellationToken);
        var playersByFixture = (await db.PlayerFixtures
                .Where(row => ids.Contains(row.FixtureId) && row.TeamId == teamId)
                .ToListAsync(cancellationToken))
            .GroupBy(row => row.FixtureId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<PlayerFixtureRow>)group.ToList());
        var teamStatsByFixture = (await db.TeamFixtureStats
                .Where(row => ids.Contains(row.FixtureId) && row.TeamId == teamId && row.Half == "full")
                .ToListAsync(cancellationToken))
            .GroupBy(row => row.FixtureId)
            .ToDictionary(group => group.Key, group => group.First());
        var summariesByFixture = await db.MatchSummaries
            .Where(row => ids.Contains(row.FixtureId))
            .ToDictionaryAsync(row => row.FixtureId, cancellationToken);

        foreach (var fixtureId in ids)
        {
            if (!fixtures.TryGetValue(fixtureId, out var fixture))
            {
                continue;
            }

            result[fixtureId] = new SquadFixtureRows(
                fixture,
                summariesByFixture.TryGetValue(fixtureId, out var summary) ? summary : null,
                playersByFixture.TryGetValue(fixtureId, out var players) ? players : Array.Empty<PlayerFixtureRow>(),
                teamStatsByFixture.TryGetValue(fixtureId, out var teamStats) ? teamStats : null);
        }

        return result;
    }

    /// <summary>
    /// The degraded discovery read: the newest completed cached fixtures for a
    /// team (season filter optional — season 0 means any season). Used only when
    /// the live fixtures read failed.
    /// </summary>
    public async Task<IReadOnlyList<FixtureRow>> GetCachedWindowAsync(
        int teamId, int season, int last, CancellationToken cancellationToken = default)
    {
        return await db.Fixtures
            .Where(row => (row.HomeTeamId == teamId || row.GuestTeamId == teamId)
                          && row.MatchFinishUnix > 0
                          && (season == 0 || row.Season == season))
            .OrderByDescending(row => row.MatchFinishUnix)
            .Take(last)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Latest captured name per team id from TeamFacts (degraded-mode labels).</summary>
    public async Task<IReadOnlyDictionary<int, string>> GetTeamNamesAsync(
        IEnumerable<int> teamIds, CancellationToken cancellationToken = default)
    {
        var ids = teamIds.Distinct().ToList();
        var facts = await db.TeamFacts
            .Where(fact => ids.Contains(fact.TeamId))
            .ToListAsync(cancellationToken);
        return facts
            .GroupBy(fact => fact.TeamId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(fact => fact.CapturedAt).First().Name);
    }

    /// <summary>
    /// Cached season statistics only — no refresh (degraded loads never touch
    /// the API for ps).
    /// </summary>
    public async Task<IReadOnlyDictionary<int, PlayerStatistics>> GetCachedPlayerSeasonsAsync(
        IReadOnlyList<int> playerIds, int season, CancellationToken cancellationToken = default)
    {
        var ids = playerIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<int, PlayerStatistics>();
        }

        var rows = await db.PlayerSeasons
            .Where(row => ids.Contains(row.PlayerId) && row.Season == season)
            .ToListAsync(cancellationToken);
        return rows.ToDictionary(row => row.PlayerId, ToSeasonStatistics);
    }

    /// <summary>
    /// The manual per-table reset (D1 §5 — Settings is its only surface). Resetting
    /// Fixtures wipes every Fixture-derived table with it (summaries + scorers,
    /// per-player and per-team stats) plus the raw XML archive, because the
    /// append-only fill keys on Fixture rows alone — clearing just the parents
    /// would duplicate the children on refill. MatchSummaries reset takes its
    /// scorers; PlayerSeasons and TeamFacts reset alone (point-in-time facts).
    /// Returns the number of rows deleted.
    /// </summary>
    public async Task<int> ResetTableAsync(CacheTable table, CancellationToken cancellationToken = default)
    {
        var deleted = table switch
        {
            CacheTable.Fixtures =>
                await db.PlayerFixtures.ExecuteDeleteAsync(cancellationToken)
                + await db.TeamFixtureStats.ExecuteDeleteAsync(cancellationToken)
                + await db.MatchSummaryScorers.ExecuteDeleteAsync(cancellationToken)
                + await db.MatchSummaries.ExecuteDeleteAsync(cancellationToken)
                + await db.Fixtures.ExecuteDeleteAsync(cancellationToken),
            CacheTable.MatchSummaries =>
                await db.MatchSummaryScorers.ExecuteDeleteAsync(cancellationToken)
                + await db.MatchSummaries.ExecuteDeleteAsync(cancellationToken),
            CacheTable.PlayerFixtures => await db.PlayerFixtures.ExecuteDeleteAsync(cancellationToken),
            CacheTable.TeamFixtureStats => await db.TeamFixtureStats.ExecuteDeleteAsync(cancellationToken),
            CacheTable.PlayerSeasons => await db.PlayerSeasons.ExecuteDeleteAsync(cancellationToken),
            CacheTable.TeamFacts => await db.TeamFacts.ExecuteDeleteAsync(cancellationToken),
            _ => 0
        };

        if (table == CacheTable.Fixtures)
        {
            rawStore.Clear();
        }

        // ExecuteDelete bypasses the change tracker; a destructive reset must not
        // leave stale tracked rows behind for the rest of the scope.
        db.ChangeTracker.Clear();

        return deleted;
    }

    private void AddSummary(MatchSummary summary)
    {
        db.MatchSummaries.Add(new MatchSummaryRow
        {
            FixtureId = summary.FixtureId,
            HomePoints = summary.Home.Points,
            GuestPoints = summary.Guest.Points,
            HomeIntensity = summary.Home.Intensity,
            GuestIntensity = summary.Guest.Intensity,
            WeatherId = summary.WeatherId,
            WeatherNight = summary.WeatherNight,
            Standing = summary.Attendance?.Standing ?? 0,
            Uncovered = summary.Attendance?.Uncovered ?? 0,
            Covered = summary.Attendance?.Covered ?? 0,
            Members = summary.Attendance?.Members ?? 0,
            Corporate = summary.Attendance?.Corporate ?? 0,
            InjuriesJson = JsonSerializer.Serialize(new { home = summary.Home.Injuries, guest = summary.Guest.Injuries }, JsonOptions),
            SubstitutionsJson = JsonSerializer.Serialize(new { home = summary.Home.Substitutions, guest = summary.Guest.Substitutions }, JsonOptions),
            FetchedAt = DateTime.UtcNow
        });

        foreach (var scorer in summary.Home.Scorers.Concat(summary.Guest.Scorers))
        {
            db.MatchSummaryScorers.Add(new MatchSummaryScorerRow
            {
                FixtureId = summary.FixtureId,
                ScorerType = scorer.ScorerType,
                PlayerId = scorer.PlayerId,
                Count = scorer.Count
            });
        }
    }

    private static string? Truncate(string? value) =>
        value?.Length > 3000 ? value[..3000] + "\n... (truncated)" : value;

    /// <summary>Copies a parsed ps record into a PlayerSeasonRow (refresh upsert);
    /// FetchedAt stamps the capture — staleness compares against it.</summary>
    private static PlayerSeasonRow CopySeasonStatistics(PlayerSeasonRow row, PlayerStatistics stats)
    {
        row.Tackles = stats.Tackles;
        row.MetresGained = stats.MetresGained;
        row.Tries = stats.Tries;
        row.Conversions = stats.Conversions;
        row.DropGoals = stats.DropGoals;
        row.Penalties = stats.Penalties;
        row.TotalPoints = stats.TotalPoints;
        row.YellowCards = stats.YellowCards;
        row.RedCards = stats.RedCards;
        row.Linebreaks = stats.Linebreaks;
        row.Intercepts = stats.Intercepts;
        row.Kicks = stats.Kicks;
        row.Knockons = stats.KnockOns;
        row.ForwardPasses = stats.ForwardPasses;
        row.TryAssists = stats.TryAssists;
        row.BeatenDefenders = stats.BeatenDefenders;
        row.Injuries = stats.Injuries;
        row.HandlingErrors = stats.HandlingErrors;
        row.MissedTackles = stats.MissedTackles;
        row.Fights = stats.Fights;
        row.KickingMetres = stats.KickingMetres;
        row.LeagueCaps = stats.LeagueCaps;
        row.FriendlyCaps = stats.FriendlyCaps;
        row.CupCaps = stats.CupCaps;
        row.UnderTwentyCaps = stats.UnderTwentyCaps;
        row.NationalCaps = stats.NationalCaps;
        row.OtherCaps = stats.OtherCaps;
        row.PenaltiesConceded = stats.PenaltiesConceded;
        row.KicksOutOnTheFull = stats.KicksOutOnTheFull;
        row.BallTime = stats.BallTime;
        row.PenaltyTime = stats.PenaltyTime;
        row.MissedConversions = stats.MissedConversions;
        row.MissedDropGoals = stats.MissedDropGoals;
        row.MissedPenalties = stats.MissedPenalties;
        row.GoodUpAndUnders = stats.GoodUpAndUnders;
        row.BadUpAndUnders = stats.BadUpAndUnders;
        row.UpAndUnders = stats.UpAndUnders;
        row.GoodKicks = stats.GoodKicks;
        row.BadKicks = stats.BadKicks;
        row.TurnoversWon = stats.TurnoversWon;
        row.LineoutsSecured = stats.LineoutsSecured;
        row.LineoutsConceded = stats.LineoutsConceded;
        row.LineoutsStolen = stats.LineoutsStolen;
        row.SuccessfulLineoutThrows = stats.SuccessfulLineoutThrows;
        row.UnsuccessfulLineoutThrows = stats.UnsuccessfulLineoutThrows;
        row.WorldCupCaps = stats.WorldCupCaps;
        row.UnderTwentyWorldCupCaps = stats.UnderTwentyWorldCupCaps;
        row.FetchedAt = DateTime.UtcNow;
        return row;
    }

    /// <summary>Reads a cached PlayerSeasonRow back as the normalized ps record;
    /// the displayed caps total is derived by summing the per-competition counts.</summary>
    private static PlayerStatistics ToSeasonStatistics(PlayerSeasonRow row) => new(
        row.PlayerId,
        row.Tackles,
        row.MetresGained,
        row.Tries,
        row.Conversions,
        row.DropGoals,
        row.Penalties,
        row.TotalPoints,
        row.YellowCards,
        row.RedCards,
        row.Linebreaks,
        row.Intercepts,
        row.Kicks,
        row.Knockons,
        row.ForwardPasses,
        row.TryAssists,
        row.BeatenDefenders,
        row.Injuries,
        row.HandlingErrors,
        row.MissedTackles,
        row.Fights,
        row.KickingMetres,
        row.MissedConversions,
        row.MissedDropGoals,
        row.MissedPenalties,
        row.GoodUpAndUnders,
        row.BadUpAndUnders,
        row.UpAndUnders,
        row.GoodKicks,
        row.BadKicks,
        row.TurnoversWon,
        row.LineoutsSecured,
        row.LineoutsConceded,
        row.LineoutsStolen,
        row.SuccessfulLineoutThrows,
        row.UnsuccessfulLineoutThrows,
        row.PenaltiesConceded,
        row.KicksOutOnTheFull,
        row.BallTime,
        row.PenaltyTime,
        row.LeagueCaps + row.FriendlyCaps + row.CupCaps + row.UnderTwentyCaps
            + row.NationalCaps + row.WorldCupCaps + row.UnderTwentyWorldCupCaps + row.OtherCaps,
        row.LeagueCaps,
        row.FriendlyCaps,
        row.CupCaps,
        row.UnderTwentyCaps,
        row.NationalCaps,
        row.WorldCupCaps,
        row.UnderTwentyWorldCupCaps,
        row.OtherCaps);
}
