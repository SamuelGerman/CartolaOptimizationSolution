using CartolaOptimization.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CartolaOptimization.Infrastructure.Data.Configurations;

public sealed class AthleteRoundContextConfiguration : IEntityTypeConfiguration<AthleteRoundContext>
{
    public void Configure(EntityTypeBuilder<AthleteRoundContext> builder)
    {
        builder.ToTable("AthleteRoundContexts");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => item.AthleteRoundId).IsUnique();

        builder.Property(item => item.TeamAverageScore).HasPrecision(10, 2);
        builder.Property(item => item.TeamHomeAverageCurrent).HasPrecision(10, 2);
        builder.Property(item => item.TeamHomeAveragePrevious).HasPrecision(10, 2);
        builder.Property(item => item.TeamAwayAverageCurrent).HasPrecision(10, 2);
        builder.Property(item => item.TeamAwayAveragePrevious).HasPrecision(10, 2);
        builder.Property(item => item.PointsPerCartoletaCurrent).HasPrecision(10, 4);
        builder.Property(item => item.PointsPerCartoletaPrevious).HasPrecision(10, 4);
        builder.Property(item => item.GrossOverallAverageCurrent).HasPrecision(10, 2);
        builder.Property(item => item.GrossOverallMovingAverageCurrent).HasPrecision(10, 2);
        builder.Property(item => item.GrossOverallAveragePrevious).HasPrecision(10, 2);
        builder.Property(item => item.GrossOverallMovingAveragePrevious).HasPrecision(10, 2);
        builder.Property(item => item.NetOverallAverageCurrent).HasPrecision(10, 2);
        builder.Property(item => item.NetOverallMovingAverageCurrent).HasPrecision(10, 2);
        builder.Property(item => item.NetOverallAveragePrevious).HasPrecision(10, 2);
        builder.Property(item => item.NetOverallMovingAveragePrevious).HasPrecision(10, 2);
        builder.Property(item => item.ValuationCurrent).HasPrecision(10, 2);
        builder.Property(item => item.MinimumToAppreciateCurrent).HasPrecision(10, 2);
        builder.Property(item => item.ValuationPrevious).HasPrecision(10, 2);
        builder.Property(item => item.MinimumToAppreciatePrevious).HasPrecision(10, 2);
        builder.Property(item => item.IndexPointsPerCartoleta).HasPrecision(10, 4);
        builder.Property(item => item.IndexOffensiveUsage).HasPrecision(10, 4);
        builder.Property(item => item.IndexOffensiveRepresentation).HasPrecision(10, 4);
        builder.Property(item => item.IndexPointsRepresentation).HasPrecision(10, 4);
        builder.Property(item => item.IndexOffensiveEfficiency).HasPrecision(10, 4);
        builder.Property(item => item.IndexDefensiveEfficiency).HasPrecision(10, 4);
        builder.Property(item => item.IndexTackleDuelPercent).HasPrecision(10, 4);
        builder.Property(item => item.IndexShotsPerGame).HasPrecision(10, 4);
        builder.Property(item => item.IndexShotsPerGoal).HasPrecision(10, 4);

        builder.HasOne(item => item.AthleteRound)
            .WithOne(item => item.Context)
            .HasForeignKey<AthleteRoundContext>(item => item.AthleteRoundId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
