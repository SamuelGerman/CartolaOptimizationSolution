using CartolaOptimization.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CartolaOptimization.Infrastructure.Data.Configurations;

public class AthleteConfiguration : IEntityTypeConfiguration<Athlete>
{
    public void Configure(EntityTypeBuilder<Athlete> builder)
    {
        builder.ToTable("Athletes");
        builder.HasKey(a => a.Id);
        builder.HasIndex(a => a.CartolaId).IsUnique();

        builder.Property(a => a.Name).IsRequired().HasMaxLength(150);
        builder.Property(a => a.Nickname).IsRequired().HasMaxLength(100);

        builder.HasMany(a => a.Rounds)
               .WithOne(r => r.Athlete)
               .HasForeignKey(r => r.AthleteId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}