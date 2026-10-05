using System.Diagnostics;
using System.Net.Http;
using System.Net.Sockets;
using BlackoutRugbyDashboard.Data;
using BlackoutRugbyDashboard.Models;
using Microsoft.Extensions.Options;

namespace BlackoutRugbyDashboard.Services;

/// <summary>
/// The outcomes of one Capture squad attempt (D3's failure classes): the roster
/// was captured; the live read failed and the last capture stands in; the read
/// answered with nothing to capture; the API rejected the read; or there was no
/// capture to fall back on and no club to read.
/// </summary>
public enum SquadCaptureStatus
{
    Captured,
    ReplayedLastSnapshot,
    EmptyRoster,
    ApiRejected,
    NoSnapshotToReplay,
    NoLinkedClub,
    CaptureFailed
}

/// <summary>What one Capture squad attempt did, and the copy that says it.</summary>
public sealed record SquadCaptureResult(
    SquadCaptureStatus Status,
    string Message,
    DateTime? CapturedAtUtc = null,
    int PlayerCount = 0)
{
    /// <summary>A fresh Squad Snapshot is on disk.</summary>
    public bool SavedSnapshot => Status == SquadCaptureStatus.Captured;

    /// <summary>The page renders, but not from a fresh capture — warn, don't panel.</summary>
    public bool IsDegraded => Status is SquadCaptureStatus.ReplayedLastSnapshot or SquadCaptureStatus.EmptyRoster;

    /// <summary>D3 decision 2: the hard-error panel with the one button to Settings.</summary>
    public bool IsHardError => Status is SquadCaptureStatus.ApiRejected
        or SquadCaptureStatus.NoSnapshotToReplay
        or SquadCaptureStatus.NoLinkedClub
        or SquadCaptureStatus.CaptureFailed;
}

/// <summary>
/// The roster capture (S2 load model, build slice #34): the Squad page's automatic
/// load runs it on every visit — there is no Capture button. It performs the
/// roster read (2 calls — the club's `t` record and its `p` roster; CSR, form,
/// energy, salary and age are the volatile present), saves a Squad Snapshot from
/// it, and persists the club's own TeamFact — which no window fill captures, so it
/// is the writer that keeps the club strip's Team CSR alive. It is also the
/// cold-start bootstrap. Failures follow D3: a rejection is the hard error the page
/// panels; a transport failure replays the last capture with a loud warning rather
/// than losing the page. The write seam mirrors <see cref="SquadPageReader"/> on
/// the read side: the reader holds no API client, and this class is the only live
/// roster read the page makes.
/// </summary>
public class SquadCaptureService(
    IBlackoutRugbyApiClient api,
    BlackoutRugbyResponseAdapter adapter,
    MatchCacheService cache,
    SnapshotStore snapshots,
    ApiLogger apiLogger,
    IOptions<DashboardDefaultsOptions> defaults)
{
    public async Task<SquadCaptureResult> CaptureAsync(int teamId, CancellationToken cancellationToken = default)
    {
        if (teamId <= 0)
        {
            return new SquadCaptureResult(
                SquadCaptureStatus.NoLinkedClub,
                "No club is linked to this account — link your club from Settings, then capture.");
        }

        string teamsXml;
        try
        {
            teamsXml = await ReadLiveAsync(
                () => api.GetTeamsAsync(teamId: teamId),
                $"{Endpoint()}/teams?teamId={teamId}");
        }
        catch (Exception exception) when (IsTransportFailure(exception))
        {
            return await ReplayLastCaptureAsync(teamId, exception);
        }

        // Fail fast on a rejection: the roster read is 2 calls against a rate-limited
        // API, and credentials the first read rejects will reject the second too.
        if (adapter.ExtractResponseError(teamsXml) is { } teamsError)
        {
            return Rejected(teamsError);
        }

        string playersXml;
        try
        {
            playersXml = await ReadLiveAsync(
                () => api.GetPlayersAsync(teamId: teamId),
                $"{Endpoint()}/players?teamId={teamId}");
        }
        catch (Exception exception) when (IsTransportFailure(exception))
        {
            return await ReplayLastCaptureAsync(teamId, exception);
        }

        if (adapter.ExtractResponseError(playersXml) is { } playersError)
        {
            return Rejected(playersError);
        }

        var roster = adapter.ParsePlayers(playersXml);
        if (roster.Count == 0)
        {
            return new SquadCaptureResult(
                SquadCaptureStatus.EmptyRoster,
                "The roster read returned no players — nothing was captured, and the page still renders your last capture.");
        }

        var club = adapter.ParseTeams(teamsXml).FirstOrDefault();
        var previous = await snapshots.GetLatestAsync(teamId);

        try
        {
            var dashboard = await BuildCaptureAsync(teamId, club, roster, previous, cancellationToken);
            var snapshot = await snapshots.SaveSnapshotAsync(dashboard);
            if (club is not null)
            {
                await cache.SaveTeamFactAsync(club, cancellationToken);
            }

            apiLogger.LogCache(
                "squad-snapshot",
                $"{snapshot.Players.Count} player(s) captured at {snapshot.CapturedAtUtc.ToLocalTime():HH:mm} — the present-state cards render these values");

            return new SquadCaptureResult(
                SquadCaptureStatus.Captured,
                $"Captured {snapshot.Players.Count} player(s) at {snapshot.CapturedAtUtc.ToLocalTime():MMM d, HH:mm} — the roster values below are the ones just read.",
                snapshot.CapturedAtUtc,
                snapshot.Players.Count);
        }
        catch (Exception exception)
        {
            // The read succeeded but the capture could not be stored: the seam never
            // throws, so the page's failure surface stays one place (D3's panel).
            return new SquadCaptureResult(
                SquadCaptureStatus.CaptureFailed,
                $"The roster read succeeded, but saving the capture failed ({exception.Message}). The page still renders your last capture.");
        }
    }

    /// <summary>The rejected read: D3's hard error, classified by the error text.</summary>
    private static SquadCaptureResult Rejected(string errorText) =>
        new(SquadCaptureStatus.ApiRejected, ClassifyApiError(errorText));

    /// <summary>
    /// The degraded capture: the read failed, so the page keeps rendering the last
    /// Squad Snapshot. With nothing captured yet there is nothing to render, which
    /// is D3's hard error rather than a silent empty page.
    /// </summary>
    private async Task<SquadCaptureResult> ReplayLastCaptureAsync(int teamId, Exception exception)
    {
        var snapshot = await snapshots.GetLatestAsync(teamId);
        if (snapshot is null || snapshot.Players.Count == 0)
        {
            return new SquadCaptureResult(
                SquadCaptureStatus.NoSnapshotToReplay,
                $"The live roster read failed ({exception.Message}) and no Squad Snapshot exists to fall back on. Try again once the game API is reachable.");
        }

        return new SquadCaptureResult(
            SquadCaptureStatus.ReplayedLastSnapshot,
            $"The live roster read failed ({exception.Message}) — the page still renders the squad as captured {snapshot.CapturedAtUtc.ToLocalTime():MMM d, HH:mm} ({snapshot.Players.Count} player(s)).",
            snapshot.CapturedAtUtc,
            snapshot.Players.Count);
    }

    /// <summary>
    /// The Squad Snapshot to save from the roster read: the captured present (name,
    /// CSR, form, energy, salary, age) plus the season-to-date totals the page's
    /// comparison columns show. Those totals come from the cached reads for the
    /// derived season, and otherwise carry over from the previous capture — the
    /// roster read knows nothing about them, and the page renders the captured
    /// values where no season read is cached. Only the fields the snapshot stores
    /// are set: the aggregates are the page's business, built from this snapshot.
    /// </summary>
    private async Task<TeamDashboardViewModel> BuildCaptureAsync(
        int teamId,
        TeamFact? club,
        IReadOnlyList<Player> roster,
        TeamSnapshot? previous,
        CancellationToken cancellationToken)
    {
        var derivedSeason = await DeriveSeasonAsync(teamId, cancellationToken);
        var seasonStats = derivedSeason == 0
            ? (IReadOnlyDictionary<int, PlayerStatistics>)new Dictionary<int, PlayerStatistics>()
            : await cache.GetCachedPlayerSeasonsAsync(
                roster.Select(player => player.Id).ToList(), derivedSeason, cancellationToken);
        var carriedOver = previous?.Players.ToDictionary(player => player.Id)
            ?? new Dictionary<int, PlayerSnapshotRecord>();

        var players = roster.Select(player =>
        {
            seasonStats.TryGetValue(player.Id, out var season);
            carriedOver.TryGetValue(player.Id, out var carried);

            return new PlayerDashboardItem
            {
                Id = player.Id,
                Name = player.Name,
                Age = player.Age,
                Csr = player.Csr,
                Salary = player.Salary,
                Form = player.Form,
                Energy = player.Energy,
                Tackles = season?.Tackles ?? carried?.Tackles ?? 0,
                MetresGained = season?.MetresGained ?? carried?.MetresGained ?? 0,
                Tries = season?.Tries ?? carried?.Tries ?? 0,
                TotalPoints = season?.TotalPoints ?? carried?.TotalPoints ?? 0,
                TotalCaps = season?.TotalCaps ?? carried?.TotalCaps ?? 0
            };
        }).ToList();

        return new TeamDashboardViewModel
        {
            TeamId = teamId,
            TeamName = string.IsNullOrWhiteSpace(club?.Name) ? $"Team {teamId}" : club!.Name,
            CountryIso = club?.CountryIso ?? string.Empty,
            PlayerCount = players.Count,
            Players = players
        };
    }

    /// <summary>The season the capture's totals are scoped to: the newest cached
    /// Fixture's season — the same derivation the page's cache read makes — or 0
    /// when the Match Cache holds nothing yet.</summary>
    private async Task<int> DeriveSeasonAsync(int teamId, CancellationToken cancellationToken)
    {
        var window = await cache.GetCachedWindowAsync(teamId, season: 0, last: 1, cancellationToken);
        return window.Count == 0 ? 0 : window[0].Season;
    }

    /// <summary>One live read with the page's request/response logging shape, so the
    /// debug panel shows the capture's two calls and their cost.</summary>
    private async Task<string> ReadLiveAsync(Func<Task<string>> read, string url)
    {
        apiLogger.LogRequest("GET", url);
        var stopwatch = Stopwatch.StartNew();
        var xml = await read();
        apiLogger.LogResponse(url, 200, Truncate(xml), stopwatch.ElapsedMilliseconds);
        return xml;
    }

    /// <summary>D3's failure-class rule: the exact upstream error strings are not
    /// live-verified, so classification keys on the error text with a defensive
    /// fallback (prior art: ClubLinkService and the legacy Squad load).</summary>
    private static string ClassifyApiError(string errorText)
    {
        var text = errorText.Trim();
        var normalized = text.ToLowerInvariant();

        if (normalized.Contains("no data requested"))
        {
            return "The game API answered 'No data requested' — a recorded transient state. Wait a moment and capture again.";
        }

        if (normalized.Contains("key") || normalized.Contains("credential") || normalized.Contains("password"))
        {
            return $"The API rejected your Member Key ({text}). It changes whenever the in-game password changes — re-link from Settings, then capture again.";
        }

        if (normalized.Contains("member") || normalized.Contains("user"))
        {
            return $"The API rejected the member credentials ({text}). Re-link from Settings if your Member ID changed.";
        }

        return $"The API rejected the read ({text}). If your Member Key changed, re-link from Settings.";
    }

    /// <summary>Connectivity-class failures degrade to the last capture; anything
    /// else is a hard error.</summary>
    private static bool IsTransportFailure(Exception exception) =>
        exception is HttpRequestException or TaskCanceledException or SocketException;

    private static string? Truncate(string? value) =>
        value?.Length > 3000 ? value[..3000] + "\n... (truncated)" : value;

    private string Endpoint() =>
        string.IsNullOrWhiteSpace(defaults.Value.BaseEndpoint)
            ? "http://classic-api.blackoutrugby.com"
            : defaults.Value.BaseEndpoint;
}
