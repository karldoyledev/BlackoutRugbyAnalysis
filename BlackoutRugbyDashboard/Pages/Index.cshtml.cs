using BlackoutRugbyDashboard.Data;
using BlackoutRugbyDashboard.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlackoutRugbyDashboard.Pages;

/// <summary>
/// Home at `/` (D2, D5). Unlinked → the "Link your club" prompt card and the
/// Playground stay reachable. Linked → the club line, the league-table card (the
/// club's league standings, r=s) and the C7 results strip (S7): the cached last-8
/// window as navigation, newest first, every row opening that Fixture's Match
/// Analysis. The bare visit fills the Home window first (one `f` read plus batched
/// `ms`/`t` for what is missing — D5) and reads the club's league table, then both
/// cards render; a failed read degrades to whatever is available rather than
/// failing the page.
/// </summary>
public class IndexModel(
    ClubLinkService clubLinks,
    MatchCacheService cache,
    SquadPageReader reader,
    LeagueTableService leagueTable,
    ILogger<IndexModel> logger) : PageModel
{
    /// <summary>How many Fixtures the Home window holds (D5).</summary>
    private const int HomeWindowLast = 8;

    public ClubLinkState? Link { get; private set; }

    /// <summary>The C7 results strip (S7): the cached window as a slim,
    /// newest-first navigation strip. Cache-only — built by <see cref="SquadResults"/>.</summary>
    public IReadOnlyList<ResultRow> Results { get; private set; } = Array.Empty<ResultRow>();

    /// <summary>The club's league table (r=s), or null when the club's league is
    /// unknown or the read answered nothing — the card is then left off.</summary>
    public LeagueTable? League { get; private set; }

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

        var leagueId = ResolveLeagueId(page);
        if (leagueId > 0)
        {
            try
            {
                League = await leagueTable.GetAsync(leagueId, Link.TeamId);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "The league table read failed for league {LeagueId}", leagueId);
            }
        }
    }

    /// <summary>
    /// The club's league: the newest cached League Fixture's league id. Cup and
    /// friendly Fixtures are skipped so their competition ids never name the table;
    /// zero when no league Fixture is cached yet, in which case there is no table.
    /// </summary>
    private static int ResolveLeagueId(SquadPageData page) =>
        page.Window
            .Where(entry => entry.Fixture.LeagueId > 0
                && string.Equals(entry.Fixture.Competition, "League", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(entry => entry.Fixture.MatchStartUnix)
            .Select(entry => entry.Fixture.LeagueId)
            .FirstOrDefault();
}
