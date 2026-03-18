using CartolaOptimization.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CartolaOptimization.Infrastructure.Data.Configurations;

public class AthleteRoundConfiguration : IEntityTypeConfiguration<AthleteRound>
{
    public void Configure(EntityTypeBuilder<AthleteRound> builder)
    {
        builder.ToTable("AthleteRounds");
        builder.HasKey(ar => ar.Id);
        builder.HasIndex(ar => new { ar.RoundId, ar.AthleteId }).IsUnique();

        builder.Property(ar => ar.ScoreNum).HasPrecision(10, 2);
        builder.Property(ar => ar.MovingAverage).HasPrecision(10, 2);
        builder.Property(ar => ar.AverageScoreNum).HasPrecision(10, 2);
        builder.Property(ar => ar.HomeAverageScore).HasPrecision(10, 2);
        builder.Property(ar => ar.AwayAverageScore).HasPrecision(10, 2);
        builder.Property(ar => ar.VariationNum).HasPrecision(10, 2);
        builder.Property(ar => ar.PriceNum).HasPrecision(10, 2);

        builder.HasOne(ar => ar.Club)
               .WithMany(club => club.AthleteRounds)
               .HasForeignKey(ar => ar.ClubId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ar => ar.Position)
               .WithMany(position => position.AthleteRounds)
               .HasForeignKey(ar => ar.PositionId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ar => ar.Status)
               .WithMany(s => s.AthleteRounds)
               .HasForeignKey(ar => ar.StatusId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
