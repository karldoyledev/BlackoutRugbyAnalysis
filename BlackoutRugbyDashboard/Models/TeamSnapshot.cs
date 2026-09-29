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

    /// <summary>Age as captured by the roster read (added in #34, the Capture
    /// squad slice): the comparison card's Age option reads it and the player's
    /// age is part of the volatile present, so it is captured, never derived.</summary>
    public int Age { get; init; }

    public int TotalPoints { get; init; }

    public int Tries { get; init; }

    public int Tackles { get; init; }

    public int MetresGained { get; init; }

    public int TotalCaps { get; init; }
}
