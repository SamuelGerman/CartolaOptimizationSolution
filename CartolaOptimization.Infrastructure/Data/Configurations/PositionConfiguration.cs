using CartolaOptimization.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CartolaOptimization.Infrastructure.Data.Configurations;

public class PositionConfiguration : IEntityTypeConfiguration<Position>
{
    public void Configure(EntityTypeBuilder<Position> builder)
    {
        builder.ToTable("Positions");

        builder.HasKey(p => p.Id);

        builder.HasIndex(p => p.CartolaId).IsUnique();

        builder.Property(p => p.Name).IsRequired().HasMaxLength(50);
        builder.Property(p => p.Abreviation).IsRequired().HasMaxLength(5);
    }
}