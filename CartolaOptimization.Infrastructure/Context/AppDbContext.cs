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

        public DbSet<AthleteRoundScouts> AthleteRoundScouts => Set<AthleteRoundScouts>();

        public DbSet<AthleteStatus> AthleteStatuses => Set<AthleteStatus>();

        public DbSet<Club> Clubs => Set<Club>();

        public DbSet<ClubRound> ClubRounds => Set<ClubRound>();

        public DbSet<ClubRoundPosition> ClubRoundPositions => Set<ClubRoundPosition>();

        public DbSet<Match> Matches => Set<Match>();

        public DbSet<Position> Positions => Set<Position>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        }
    }
}
