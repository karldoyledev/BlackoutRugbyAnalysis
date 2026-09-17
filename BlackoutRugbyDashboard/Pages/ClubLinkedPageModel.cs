using BlackoutRugbyDashboard.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlackoutRugbyDashboard.Pages;

/// <summary>
/// The link gate (D2): link-required pages — /Fixtures/{id}, /Players/{id},
/// /Players, /Squad — enforce "Linked" in code, not route config. Unlinked
/// users land on Home's "Link your club" prompt card.
/// </summary>
public abstract class ClubLinkedPageModel(ClubLinkService clubLinks) : PageModel
{
    protected ClubLinkService ClubLinks { get; } = clubLinks;

    public override void OnPageHandlerExecuting(Microsoft.AspNetCore.Mvc.Filters.PageHandlerExecutingContext context)
    {
        if (ClubLinks.GetLinkState() is null)
        {
            context.Result = RedirectToPage("/Index");
        }
    }
}