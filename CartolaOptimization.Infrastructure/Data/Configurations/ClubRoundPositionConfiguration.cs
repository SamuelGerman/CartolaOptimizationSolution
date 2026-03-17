using CartolaOptimization.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CartolaOptimization.Infrastructure.Data.Configurations;

public class ClubRoundPositionConfiguration : IEntityTypeConfiguration<ClubRoundPosition>
{
    public void Configure(EntityTypeBuilder<ClubRoundPosition> builder)
    {
        builder.ToTable("ClubRoundPositions");
        builder.HasKey(crp => crp.Id);

        builder.Property(crp => crp.PointsConcededAverage).HasPrecision(10, 2);
        builder.Property(crp => crp.PointsConqueredAverage).HasPrecision(10, 2);

        builder.HasOne(crp => crp.ClubRound)
               .WithMany(cr => cr.PositionStats)
               .HasForeignKey(crp => crp.ClubRoundId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(crp => crp.Position)
               .WithMany()
               .HasForeignKey(crp => crp.PositionId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}