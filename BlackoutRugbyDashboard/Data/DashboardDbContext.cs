using Microsoft.EntityFrameworkCore;

namespace BlackoutRugbyDashboard.Data;

/// <summary>
/// The one SQLite database (spec §Foundation, decision 4): hosts the Match Cache
/// parsed rows now; the Identity sets join in the auth slice (D3) — one
/// DashboardDbContext for both, per D1/D3.
/// </summary>
public class DashboardDbContext(DbContextOptions<DashboardDbContext> options) : DbContext(options)
{
    public DbSet<FixtureRow> Fixtures => Set<FixtureRow>();
    public DbSet<MatchSummaryRow> MatchSummaries => Set<MatchSummaryRow>();
    public DbSet<MatchSummaryScorerRow> MatchSummaryScorers => Set<MatchSummaryScorerRow>();
    public DbSet<PlayerFixtureRow> PlayerFixtures => Set<PlayerFixtureRow>();
    public DbSet<TeamFixtureStatRow> TeamFixtureStats => Set<TeamFixtureStatRow>();
    public DbSet<TeamFactRow> TeamFacts => Set<TeamFactRow>();
    public DbSet<PlayerSeasonRow> PlayerSeasons => Set<PlayerSeasonRow>();
}
