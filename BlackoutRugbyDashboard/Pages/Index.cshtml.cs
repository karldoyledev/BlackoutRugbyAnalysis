using BlackoutRugbyDashboard.Data;
using BlackoutRugbyDashboard.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlackoutRugbyDashboard.Pages;

/// <summary>
/// Home at `/` (D2, D5). Unlinked → the "Link your club" prompt card and the
/// Playground stay reachable. Linked → the club line and the C7 results strip
/// (S7): the cached last-8 window as navigation, newest first, every row opening
/// that Fixture's Match Analysis. The bare visit fills the Home window first
/// (one `f` read plus batched `ms`/`t` for what is missing — D5), then the strip
/// renders from the cache; a failed fill degrades to whatever is already cached
/// rather than failing the page.
/// </summary>
public class IndexModel(
    ClubLinkService clubLinks,
    MatchCacheService cache,
    SquadPageReader reader,
    ILogger<IndexModel> logger) : PageModel
{
    /// <summary>How many Fixtures the Home window holds (D5).</summary>
    private const int HomeWindowLast = 8;

    public ClubLinkState? Link { get; private set; }

    /// <summary>The C7 results strip (S7): the cached window as a slim,
    /// newest-first navigation strip. Cache-only — built by <see cref="SquadResults"/>.</summary>
    public IReadOnlyList<ResultRow> Results { get; private set; } = Array.Empty<ResultRow>();

    public async Task OnGetAsync()
    {
        Link = clubLinks.GetLinkState();
        if (Link is null)
        {
            return;
        }

        try
        {
            await cache.FillHomeWindowAsync(Link.TeamId, HomeWindowLast);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "The Home window refresh failed for team {TeamId}", Link.TeamId);
        }

        var page = await reader.ReadAsync(Link.TeamId, windowLast: HomeWindowLast);
        Results = SquadResults.Build(page);
    }
}
