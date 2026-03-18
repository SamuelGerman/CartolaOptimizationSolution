using CartolaOptimization.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CartolaOptimization.Infrastructure.Data.Configurations;

public class ClubRoundConfiguration : IEntityTypeConfiguration<ClubRound>
{
    public void Configure(EntityTypeBuilder<ClubRound> builder)
    {
        builder.ToTable("ClubRounds");
        builder.HasKey(cr => cr.Id);
        builder.HasIndex(cr => new { cr.Round, cr.ClubId }).IsUnique();
        builder.HasIndex(cr => new { cr.Round, cr.IsTrainingEligible });
        builder.HasIndex(cr => cr.TrainingReadinessStatus);

        builder.Property(cr => cr.WinProbability).HasPrecision(10, 4);
        builder.Property(cr => cr.DrawProbability).HasPrecision(10, 4);
        builder.Property(cr => cr.LoseProbability).HasPrecision(10, 4);
        builder.Property(cr => cr.OverallAverage).HasPrecision(10, 2);
        builder.Property(cr => cr.MovingAverage).HasPrecision(10, 2);
        builder.Property(cr => cr.ExpectedPoints).HasPrecision(10, 2);
        builder.Property(cr => cr.TrainingReadinessStatus).IsRequired().HasMaxLength(30);
        builder.Property(cr => cr.TrainingReadinessReason).IsRequired().HasMaxLength(500);

        builder.HasOne(cr => cr.Club)
               .WithMany()
               .HasForeignKey(cr => cr.ClubId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
