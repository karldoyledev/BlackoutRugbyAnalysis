using BlackoutRugbyDashboard.Services;
using Microsoft.AspNetCore.Mvc;

namespace BlackoutRugbyDashboard.Pages.Players;

/// <summary>One row of the /Players index: the Player and the CSR chip.</summary>
public sealed record PlayerRow(int Id, string Name, int Csr);

/// <summary>
/// The /Players index (D2 route, D7 amendment). Link-required. One row per Player in
/// the latest Squad Snapshot (Name · CSR · link to the detail page) with zero API
/// calls whenever a snapshot exists; when none does, an empty state offers an explicit
/// Capture squad action — the same roster read + snapshot capture the Squad page's
/// auto-load performs.
/// </summary>
public class PlayersIndexModel(
    ClubLinkService clubLinks, SnapshotStore snapshots, SquadCaptureService captures)
    : ClubLinkedPageModel(clubLinks)
{
    public IReadOnlyList<PlayerRow> Players { get; private set; } = Array.Empty<PlayerRow>();

    /// <summary>True when a Squad Snapshot exists — the index's source, so the page
    /// costs no API call.</summary>
    public bool HasCapture { get; private set; }

    public string? StatusMessage { get; private set; }

    public async Task OnGetAsync(string? captured = null)
    {
        var teamId = ClubLinks.GetLinkState()?.TeamId ?? 0;
        var capture = await snapshots.GetLatestAsync(teamId);

        HasCapture = capture is { Players.Count: > 0 };
        Players = capture is null
            ? Array.Empty<PlayerRow>()
            : capture.Players
                .OrderByDescending(player => player.Csr)
                .ThenBy(player => player.Name)
                .Select(player => new PlayerRow(player.Id, player.Name, player.Csr))
                .ToList();

        StatusMessage = captured switch
        {
            "saved" => "Squad captured — the list below is from the new Squad Snapshot.",
            "degraded" => "The live roster read failed, so the page shows your last capture.",
            "failed" => "The API rejected the roster read. If your Member Key changed, re-link from Settings.",
            _ => null
        };
    }

    /// <summary>
    /// The empty state's Capture squad action (D7): the same roster read + snapshot
    /// capture the Squad page makes. Redirects back so a refresh never re-posts.
    /// </summary>
    public async Task<IActionResult> OnPostCaptureAsync()
    {
        var teamId = ClubLinks.GetLinkState()?.TeamId ?? 0;
        var result = await captures.CaptureAsync(teamId);

        var outcome = result.IsHardError ? "failed" : result.IsDegraded ? "degraded" : "saved";
        return RedirectToPage("/Players", new { captured = outcome });
    }
}
