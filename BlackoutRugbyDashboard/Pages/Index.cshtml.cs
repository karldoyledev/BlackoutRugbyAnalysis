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
    private readonly DashboardDefaultsOptions _dashboardDefaults;
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
        _dashboardDefaults = dashboardDefaults.Value;
        Input = CreateRequestFromDefaults();
    }

    [BindProperty]
    public TeamDashboardRequest Input { get; set; }

    public TeamDashboardViewModel? Dashboard { get; private set; }

    public TeamSnapshotComparison? LatestComparison { get; private set; }

    public string? StatusMessage { get; private set; }

    public string? ErrorMessage { get; private set; }

    public void OnGet()
    {
        Input = CreateRequestFromDefaults();
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
        ApplyDefaultsIfMissing();
        ModelState.Clear();

        if (!TryValidateModel(Input, nameof(Input)))
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

    private TeamDashboardRequest CreateRequestFromDefaults()
    {
        return new TeamDashboardRequest
        {
            BaseEndpoint = _dashboardDefaults.BaseEndpoint,
            TeamId = _dashboardDefaults.TeamId,
            MemberId = _dashboardDefaults.MemberId,
            MemberKey = _dashboardDefaults.MemberKey
        };
    }

    private void ApplyDefaultsIfMissing()
    {
        if (string.IsNullOrWhiteSpace(Input.BaseEndpoint))
        {
            Input.BaseEndpoint = _dashboardDefaults.BaseEndpoint;
        }

        if (Input.TeamId <= 0)
        {
            Input.TeamId = _dashboardDefaults.TeamId;
        }

        if (!Input.MemberId.HasValue && _dashboardDefaults.MemberId.HasValue)
        {
            Input.MemberId = _dashboardDefaults.MemberId;
        }

        if (string.IsNullOrWhiteSpace(Input.MemberKey) && !string.IsNullOrWhiteSpace(_dashboardDefaults.MemberKey))
        {
            Input.MemberKey = _dashboardDefaults.MemberKey;
        }
    }
}
