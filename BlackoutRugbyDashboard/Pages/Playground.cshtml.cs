using System.Text.Json;
using BlackoutRugbyDashboard.Data;
using BlackoutRugbyDashboard.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlackoutRugbyDashboard.Pages;

/// <summary>
/// The API Playground (D2): the old ApiTester page at its new route — auth-only,
/// not link-required (developer-credential reads work without member credentials;
/// a linked User's reads ride their own credentials). Raw endpoint exploration.
/// Every card's fields are pre-filled from the linked club's own cache — team,
/// member, the newest cached Fixture, a Player, season, round, league and country —
/// so a Test click returns real data with no typing. An unlinked visitor gets the
/// placeholders alone.
/// </summary>
public class PlaygroundModel(
    ClubLinkService clubLinks,
    MatchCacheService cache,
    SnapshotStore snapshots,
    ILogger<PlaygroundModel> logger) : PageModel
{
    /// <summary>
    /// The per-parameter card defaults, as a JSON object (parameter name → value).
    /// Only the ones we hold a value for appear; the card keeps its placeholder
    /// otherwise.
    /// </summary>
    public string DefaultsJson { get; private set; } = "{}";

    public async Task OnGetAsync()
    {
        logger.LogDebug("API Playground opened");

        var link = clubLinks.GetLinkState();
        if (link is null)
        {
            return;
        }

        var defaults = new Dictionary<string, string>
        {
            ["teamid"] = link.TeamId.ToString(),
            ["teamids"] = link.TeamId.ToString(),
            ["memberid"] = link.MemberId.ToString(),
            ["division"] = "1",
            ["divisionid"] = "1",
            ["divisionids"] = "1",
            ["folderid"] = "inbox",
            ["start"] = "0",
            ["limit"] = "25"
        };

        var fact = await cache.GetLatestTeamFactAsync(link.TeamId);
        if (!string.IsNullOrWhiteSpace(fact?.CountryIso))
        {
            defaults["country_iso"] = fact!.CountryIso;
            defaults["iso"] = fact.CountryIso;
            defaults["country"] = fact.CountryIso;
        }

        var window = await cache.GetCachedWindowAsync(link.TeamId, season: 0, last: 12);
        if (window.Count > 0)
        {
            var newest = window[0];
            defaults["fixtureid"] = newest.FixtureId.ToString();
            defaults["fixtureids"] = string.Join(',', window.Take(3).Select(row => row.FixtureId));
            defaults["season"] = newest.Season.ToString();
            defaults["round"] = newest.Round.ToString();
        }

        var leagueId = window
            .Where(row => row.LeagueId > 0 && string.Equals(row.Competition, "League", StringComparison.OrdinalIgnoreCase))
            .Select(row => row.LeagueId)
            .FirstOrDefault();
        if (leagueId > 0)
        {
            defaults["leagueid"] = leagueId.ToString();
        }

        var capture = await snapshots.GetLatestAsync(link.TeamId);
        if (capture?.Players.FirstOrDefault() is { } player)
        {
            defaults["playerid"] = player.Id.ToString();
        }

        DefaultsJson = JsonSerializer.Serialize(defaults);
    }
}
