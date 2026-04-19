namespace BlackoutRugbyDashboard.Models;

public class TeamSnapshot
{
    public int TeamId { get; init; }

    public string TeamName { get; init; } = string.Empty;

    public DateTime CapturedAtUtc { get; init; }

    public List<PlayerSnapshotRecord> Players { get; init; } = new();
}

public class PlayerSnapshotRecord
{
    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public int Csr { get; init; }

    public int Salary { get; init; }

    public int Form { get; init; }

    public int Energy { get; init; }

    public int TotalPoints { get; init; }

    public int Tries { get; init; }

    public int Tackles { get; init; }

    public int MetresGained { get; init; }

    public int TotalCaps { get; init; }
}
