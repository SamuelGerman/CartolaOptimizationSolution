using CartolaOptimization.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CartolaOptimization.Infrastructure.Data.Configurations;

public class AthleteStatusConfiguration : IEntityTypeConfiguration<AthleteStatus>
{
    public void Configure(EntityTypeBuilder<AthleteStatus> builder)
    {
        builder.ToTable("AthleteStatuses");

        builder.HasKey(s => s.Id);

        builder.HasIndex(s => s.CartolaId).IsUnique();

        builder.Property(s => s.Name).IsRequired().HasMaxLength(50);
    }
}