using CartolaOptimization.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CartolaOptimization.Infrastructure.Data.Configurations;

public class MatchConfiguration : IEntityTypeConfiguration<Match>
{
    public void Configure(EntityTypeBuilder<Match> builder)
    {
        builder.ToTable("Matches");
        builder.HasKey(m => m.Id);

        builder.HasIndex(m => m.CartolaId).IsUnique();

        builder.Property(m => m.Location).HasMaxLength(150);
        builder.Property(m => m.HomeForm).HasMaxLength(10); // "veeed"
        builder.Property(m => m.AwayForm).HasMaxLength(10); // "veeed"

        builder.HasOne(m => m.HomeClub)
               .WithMany()
               .HasForeignKey(m => m.HomeClubId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.AwayClub)
               .WithMany()
               .HasForeignKey(m => m.AwayClubId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
