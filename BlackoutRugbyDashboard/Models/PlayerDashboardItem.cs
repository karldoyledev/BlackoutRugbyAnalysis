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

    // Match Statistics
    public int Tackles { get; init; }

    public int MetresGained { get; init; }

    public int Tries { get; init; }

    public int Conversions { get; init; }

    public int DropGoals { get; init; }

    public int Penalties { get; init; }

    public int TotalPoints { get; init; }

    public int YellowCards { get; init; }

    public int RedCards { get; init; }

    public int Linebreaks { get; init; }

    public int Intercepts { get; init; }

    public int Kicks { get; init; }

    public int KnockOns { get; init; }

    public int ForwardPasses { get; init; }

    public int TryAssists { get; init; }

    public int BeatenDefenders { get; init; }

    public int Injuries { get; init; }

    public int HandlingErrors { get; init; }

    public int MissedTackles { get; init; }

    public int Fights { get; init; }

    public int KickingMetres { get; init; }

    public int MissedConversions { get; init; }

    public int MissedDropGoals { get; init; }

    public int MissedPenalties { get; init; }

    public int GoodUpAndUnders { get; init; }

    public int BadUpAndUnders { get; init; }

    public int UpAndUnders { get; init; }

    public int GoodKicks { get; init; }

    public int BadKicks { get; init; }

    public int TurnoversWon { get; init; }

    public int LineoutsSecured { get; init; }

    public int LineoutsConceded { get; init; }

    public int LineoutsStolen { get; init; }

    public int SuccessfulLineoutThrows { get; init; }

    public int UnsuccessfulLineoutThrows { get; init; }

    public int PenaltiesConceded { get; init; }

    public int KicksOutOnTheFull { get; init; }

    public int BallTime { get; init; }

    public int PenaltyTime { get; init; }

    // Caps
    public int TotalCaps { get; init; }

    public int LeagueCaps { get; init; }

    public int FriendlyCaps { get; init; }

    public int CupCaps { get; init; }

    public int UnderTwentyCaps { get; init; }

    public int NationalCaps { get; init; }

    public int WorldCupCaps { get; init; }

    public int UnderTwentyWorldCupCaps { get; init; }

    public int OtherCaps { get; init; }

    public IReadOnlyList<string> RecentPops { get; init; } = Array.Empty<string>();
}
