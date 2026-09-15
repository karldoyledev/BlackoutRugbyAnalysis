using System.Globalization;
using System.Text.Json;
using BlackoutRugbyDashboard.Services;
using Microsoft.EntityFrameworkCore;

namespace BlackoutRugbyDashboard.Data;

/// <summary>Outcome of one Home window fill: what the window held and what it cost.</summary>
public record HomeWindowFillResult(int WindowSize, int Completed, int NewlyCached, int ApiCalls);

/// <summary>
/// The Match Cache fill service (spec §Match Cache / D1): fetch → archive raw →
/// parse → persist. Append-only: completed Fixtures are never re-fetched,
/// unplayed Fixtures are never cached, and Home's ms/t batches run only for
/// what is missing. Team facts are captured when seen, on every window refresh.
/// </summary>
public class MatchCacheService(
    IBlackoutRugbyApiClient api,
    BlackoutRugbyResponseAdapter adapter,
    DashboardDbContext db,
    MatchCacheRawStore rawStore)
{
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

        var fixturesXml = await api.GetFixturesAsync(fixtureId: fixtureId);
        rawStore.Save("f", $"fixture{fixtureId}", fixturesXml);
        var record = adapter.ParseFixtureRecords(fixturesXml).FirstOrDefault(row => row.FixtureId == fixtureId);
        if (record is null || record.MatchFinishUnix == 0)
        {
            return false;
        }

        db.Fixtures.Add(record);

        var msXml = await api.GetMatchSummaryAsync(fixtureId: fixtureId);
        rawStore.Save("ms", fixtureId.ToString(CultureInfo.InvariantCulture), msXml);
        foreach (var summary in adapter.ParseMatchSummaries(msXml))
        {
            AddSummary(summary);
        }

        var squadFsXml = await api.GetFixtureStatisticsAsync(fixtureId, teamPlayersStats: teamId);
        rawStore.Save("fs-teamplayers", fixtureId.ToString(CultureInfo.InvariantCulture), squadFsXml);
        db.PlayerFixtures.AddRange(adapter.ParsePlayerFixtureStats(squadFsXml));

        var bareFsXml = await api.GetFixtureStatisticsAsync(fixtureId);
        rawStore.Save("fs-bare", fixtureId.ToString(CultureInfo.InvariantCulture), bareFsXml);
        db.TeamFixtureStats.AddRange(adapter.ParseTeamFixtureStats(bareFsXml));

        var lineupXml = await api.GetLineupsAsync(teamId, fixtureId: fixtureId);
        rawStore.Save("lu", fixtureId.ToString(CultureInfo.InvariantCulture), lineupXml);
        // D1 caches the lineup read as raw XML; a parsed lineup row type joins
        // with the Match Analysis slice (D6), which owns the team-sheet rendering.

        await db.SaveChangesAsync(cancellationToken);
        return true;
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
}
