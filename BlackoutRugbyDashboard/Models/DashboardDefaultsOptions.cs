namespace BlackoutRugbyDashboard.Models;

public class DashboardDefaultsOptions
{
    public string BaseEndpoint { get; set; } = string.Empty;

    public int TeamId { get; set; }

    public int? MemberId { get; set; }

    public string? MemberKey { get; set; }
}