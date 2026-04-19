namespace BlackoutRugbyDashboard.Models;

public class TeamSnapshotComparison
{
    public DateTime PreviousCapturedAtUtc { get; init; }

    public DateTime CurrentCapturedAtUtc { get; init; }

    public IReadOnlyList<PlayerChangeSummary> PlayerChanges { get; init; } = Array.Empty<PlayerChangeSummary>();
}

public class PlayerChangeSummary
{
    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public bool IsNewPlayer { get; init; }

    public bool IsRemovedPlayer { get; init; }

    public int DeltaCsr { get; init; }

    public int DeltaSalary { get; init; }

    public int DeltaForm { get; init; }

    public int DeltaEnergy { get; init; }

    public int DeltaTotalPoints { get; init; }

    public int DeltaTries { get; init; }

    public int DeltaTackles { get; init; }

    public int DeltaMetresGained { get; init; }

    public int DeltaTotalCaps { get; init; }
}
