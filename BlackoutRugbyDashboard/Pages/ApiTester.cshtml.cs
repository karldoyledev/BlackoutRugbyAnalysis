using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlackoutRugbyDashboard.Pages;

public class ApiTesterModel : PageModel
{
    private readonly ILogger<ApiTesterModel> _logger;

    public ApiTesterModel(ILogger<ApiTesterModel> logger)
    {
        _logger = logger;
    }

    public void OnGet()
    {
    }
}
