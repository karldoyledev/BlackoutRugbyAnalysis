using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BlackoutRugbyDashboard.Data;

/// <summary>
/// The one SQLite database (spec §Foundation, decision 4): hosts both the Identity
/// sets (D3) and the Match Cache parsed rows (D1) — one DashboardDbContext for
/// both, file at Data/dashboard.db, migrations in the web project.
/// </summary>
public class DashboardDbContext(DbContextOptions<DashboardDbContext> options)
    : IdentityDbContext<DashboardUser>(options)
{
    public DbSet<FixtureRow> Fixtures => Set<FixtureRow>();
    public DbSet<MatchSummaryRow> MatchSummaries => Set<MatchSummaryRow>();
    public DbSet<MatchSummaryScorerRow> MatchSummaryScorers => Set<MatchSummaryScorerRow>();
    public DbSet<PlayerFixtureRow> PlayerFixtures => Set<PlayerFixtureRow>();
    public DbSet<TeamFixtureStatRow> TeamFixtureStats => Set<TeamFixtureStatRow>();
    public DbSet<TeamFactRow> TeamFacts => Set<TeamFactRow>();
    public DbSet<PlayerSeasonRow> PlayerSeasons => Set<PlayerSeasonRow>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // D3: one Member per User — the store refuses a second User claiming the
        // same Member; the Club Link flow checks first for its error copy.
        builder.Entity<DashboardUser>()
            .HasIndex(user => user.MemberId)
            .IsUnique();
    }
}
