using BlackoutRugbyDashboard.Models;
using BlackoutRugbyDashboard.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace BlackoutRugbyDashboard.Pages;

public class IndexModel : PageModel
{
    private readonly TeamDashboardService _dashboardService;
    private readonly SnapshotStore _snapshotStore;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        TeamDashboardService dashboardService,
        SnapshotStore snapshotStore,
        ILogger<IndexModel> logger,
        IOptions<DashboardDefaultsOptions> dashboardDefaults)
    {
        _dashboardService = dashboardService;
        _snapshotStore = snapshotStore;
        _logger = logger;
        var defaults = dashboardDefaults.Value;
        Input = new TeamDashboardRequest
        {
            BaseEndpoint = defaults.BaseEndpoint,
            TeamId = defaults.TeamId,
            MemberId = defaults.MemberId,
            MemberKey = defaults.MemberKey
        };
    }

    [BindProperty]
    public TeamDashboardRequest Input { get; set; }

    public TeamDashboardViewModel? Dashboard { get; private set; }

    public TeamSnapshotComparison? LatestComparison { get; private set; }

    public string? StatusMessage { get; private set; }

    public string? ErrorMessage { get; private set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostLoadAsync()
    {
        return await LoadDashboardAsync(captureSnapshot: false);
    }

    public async Task<IActionResult> OnPostCaptureAsync()
    {
        return await LoadDashboardAsync(captureSnapshot: true);
    }

    private async Task<IActionResult> LoadDashboardAsync(bool captureSnapshot)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            Dashboard = await _dashboardService.BuildDashboardAsync(Input);

            if (captureSnapshot)
            {
                await _snapshotStore.SaveSnapshotAsync(Dashboard);
                StatusMessage = "Snapshot captured. Future captures will show per-player deltas after each match.";
            }
            else
            {
                StatusMessage = "Live dashboard loaded.";
            }

            LatestComparison = await _snapshotStore.GetLatestComparisonAsync(Input.TeamId);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to load dashboard for team {TeamId}", Input.TeamId);
            ErrorMessage = exception.Message;
        }

        return Page();
    }
}
