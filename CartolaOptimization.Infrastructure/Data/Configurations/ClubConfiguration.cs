using CartolaOptimization.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CartolaOptimization.Infrastructure.Data.Configurations;

public class ClubConfiguration : IEntityTypeConfiguration<Club>
{
    public void Configure(EntityTypeBuilder<Club> builder)
    {
        builder.ToTable("Clubs");

        builder.HasKey(c => c.Id);

        builder.HasIndex(c => c.CartolaId).IsUnique();

        builder.Property(c => c.Abreviation).IsRequired().HasMaxLength(10);
        builder.Property(c => c.Slug).IsRequired().HasMaxLength(100);
        builder.Property(c => c.Nickname).IsRequired().HasMaxLength(100);
        builder.Property(c => c.LogoUrl).HasMaxLength(255);
    }
}