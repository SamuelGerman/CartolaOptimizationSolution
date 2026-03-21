using CartolaOptimization.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CartolaOptimization.Infrastructure.Data.Configurations;

public sealed class ClubRoundContextConfiguration : IEntityTypeConfiguration<ClubRoundContext>
{
    public void Configure(EntityTypeBuilder<ClubRoundContext> builder)
    {
        builder.ToTable("ClubRoundContexts");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => item.ClubRoundId).IsUnique();

        builder.Property(item => item.PreviousOverallAverage).HasPrecision(10, 2);
        builder.Property(item => item.RankingsOverallAverage).HasPrecision(10, 2);
        builder.Property(item => item.RankingsMovingAverage).HasPrecision(10, 2);
        builder.Property(item => item.RankingsHomeAverage).HasPrecision(10, 2);
        builder.Property(item => item.RankingsHomeMovingAverage).HasPrecision(10, 2);
        builder.Property(item => item.RankingsAwayAverage).HasPrecision(10, 2);
        builder.Property(item => item.RankingsAwayMovingAverage).HasPrecision(10, 2);
        builder.Property(item => item.GeneralPointsConcededAverage).HasPrecision(10, 2);
        builder.Property(item => item.GeneralPointsConqueredAverage).HasPrecision(10, 2);
        builder.Property(item => item.HomePointsConcededAverage).HasPrecision(10, 2);
        builder.Property(item => item.HomePointsConqueredAverage).HasPrecision(10, 2);
        builder.Property(item => item.AwayPointsConcededAverage).HasPrecision(10, 2);
        builder.Property(item => item.AwayPointsConqueredAverage).HasPrecision(10, 2);
        builder.Property(item => item.HistoricalWinRate).HasPrecision(10, 4);
        builder.Property(item => item.HistoricalDrawRate).HasPrecision(10, 4);
        builder.Property(item => item.HistoricalLossRate).HasPrecision(10, 4);
        builder.Property(item => item.FavoriteWinProbability).HasPrecision(10, 4);
        builder.Property(item => item.FavoriteDrawProbability).HasPrecision(10, 4);
        builder.Property(item => item.FavoriteLoseProbability).HasPrecision(10, 4);
        builder.Property(item => item.ForecastRiskLevel).IsRequired().HasMaxLength(20);
        builder.Property(item => item.AverageInsightConfidence).HasPrecision(10, 4);

        builder.HasOne(item => item.ClubRound)
            .WithOne(item => item.Context)
            .HasForeignKey<ClubRoundContext>(item => item.ClubRoundId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
