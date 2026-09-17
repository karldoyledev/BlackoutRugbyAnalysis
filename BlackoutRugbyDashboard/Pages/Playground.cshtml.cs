using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlackoutRugbyDashboard.Pages;

/// <summary>
/// The API Playground (D2): the old ApiTester page at its new route — auth-only,
/// not link-required (developer-credential reads work without member credentials;
/// a linked User's reads ride their own credentials). Raw endpoint exploration.
/// </summary>
public class PlaygroundModel(ILogger<PlaygroundModel> logger) : PageModel
{
    public void OnGet()
    {
        logger.LogDebug("API Playground opened");
    }
}