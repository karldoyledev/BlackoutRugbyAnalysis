namespace BlackoutRugbyDashboard.Models;

public class PlayerDashboardItem
{
    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public int Age { get; init; }

    public int Csr { get; init; }

    public int Salary { get; init; }

    public int Form { get; init; }

    public int Energy { get; init; }

    public int Tackles { get; init; }

    public int MetresGained { get; init; }

    public int Tries { get; init; }

    public int TotalPoints { get; init; }

    public int TotalCaps { get; init; }

    public IReadOnlyList<string> RecentPops { get; init; } = Array.Empty<string>();
}
