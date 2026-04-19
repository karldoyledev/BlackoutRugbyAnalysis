namespace BlackoutRugbyDashboard.Models;

public class TeamDashboardViewModel
{
    public int TeamId { get; init; }

    public string TeamName { get; init; } = string.Empty;

    public string CountryIso { get; init; } = string.Empty;

    public int PlayerCount { get; init; }

    public decimal AverageAge { get; init; }

    public decimal AverageCsr { get; init; }

    public decimal AverageForm { get; init; }

    public decimal AverageEnergy { get; init; }

    public int TotalSalary { get; init; }

    public int TotalPoints { get; init; }

    public int TotalTries { get; init; }

    public int TotalTackles { get; init; }

    public int TotalMetres { get; init; }

    public IReadOnlyList<PlayerDashboardItem> Players { get; init; } = Array.Empty<PlayerDashboardItem>();
}
