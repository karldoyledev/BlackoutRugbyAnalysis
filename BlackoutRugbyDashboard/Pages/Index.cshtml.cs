using BlackoutRugbyDashboard.Models;
using BlackoutRugbyDashboard.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlackoutRugbyDashboard.Pages;

/// <summary>
/// Home at `/` (D2). Transitional scaffold for the accounts slice: the gate-driven
/// states are live — an Unlinked user sees the "Link your club" prompt card and
/// the API Playground stays reachable; a Linked user gets the launch panel. The
/// last-8 form rows (D5) land with the Home build slice.
/// </summary>
public class IndexModel(ClubLinkService clubLinks) : PageModel
{
    public ClubLinkState? Link { get; private set; }

    public void OnGet()
    {
        Link = clubLinks.GetLinkState();
    }
}
