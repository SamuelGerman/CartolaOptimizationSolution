using CartolaOptimization.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CartolaOptimization.Infrastructure.Context
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Athlete> Athletes => Set<Athlete>();

        public DbSet<AthleteRound> AthleteRounds => Set<AthleteRound>();

        public DbSet<AthleteRoundContext> AthleteRoundContexts => Set<AthleteRoundContext>();

        public DbSet<AthleteRoundScouts> AthleteRoundScouts => Set<AthleteRoundScouts>();

        public DbSet<AthleteStatus> AthleteStatuses => Set<AthleteStatus>();

        public DbSet<Club> Clubs => Set<Club>();

        public DbSet<ClubRound> ClubRounds => Set<ClubRound>();

        public DbSet<ClubRoundContext> ClubRoundContexts => Set<ClubRoundContext>();

        public DbSet<ClubRoundPosition> ClubRoundPositions => Set<ClubRoundPosition>();

        public DbSet<IntegrationFailureLog> IntegrationFailureLogs => Set<IntegrationFailureLog>();

        public DbSet<Match> Matches => Set<Match>();

        public DbSet<Position> Positions => Set<Position>();

        public DbSet<SyncExecution> SyncExecutions => Set<SyncExecution>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        }
    }
}
