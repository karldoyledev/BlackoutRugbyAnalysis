namespace BlackoutRugbyDashboard.Models;

/// <summary>
/// Static dashboard defaults (spec §Secrets & config, decision 13): only the
/// endpoint survives the scrub. Member credentials ride the signed-in User's
/// Club Link; developer Key/IV live in user-secrets — never appsettings.
/// </summary>
public class DashboardDefaultsOptions
{
    public string BaseEndpoint { get; set; } = string.Empty;
}