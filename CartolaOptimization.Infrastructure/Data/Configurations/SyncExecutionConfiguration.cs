using CartolaOptimization.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CartolaOptimization.Infrastructure.Data.Configurations;

public sealed class SyncExecutionConfiguration : IEntityTypeConfiguration<SyncExecution>
{
    public void Configure(EntityTypeBuilder<SyncExecution> builder)
    {
        builder.ToTable("SyncExecutions");

        builder.HasKey(execution => execution.Id);

        builder.HasIndex(execution => execution.CorrelationId).IsUnique();
        builder.HasIndex(execution => execution.StartedAtUtc);
        builder.HasIndex(execution => new { execution.SourceSystem, execution.Status, execution.StartedAtUtc });

        builder.Property(execution => execution.CorrelationId).IsRequired().HasMaxLength(50);
        builder.Property(execution => execution.SourceSystem).IsRequired().HasMaxLength(50);
        builder.Property(execution => execution.ProcessName).IsRequired().HasMaxLength(150);
        builder.Property(execution => execution.Status).IsRequired().HasMaxLength(30);
        builder.Property(execution => execution.LastStepName).IsRequired().HasMaxLength(200);
        builder.Property(execution => execution.ErrorCode).IsRequired().HasMaxLength(100);
        builder.Property(execution => execution.Notes).HasMaxLength(4000);

        builder.Property(execution => execution.CoverageRate).HasPrecision(10, 2);
        builder.Property(execution => execution.SkippedRate).HasPrecision(10, 2);

        builder.Property(execution => execution.MetricsJson).HasColumnType("longtext");
        builder.Property(execution => execution.ContextJson).HasColumnType("longtext");
        builder.Property(execution => execution.QualityCriteriaJson).HasColumnType("longtext");
    }
}
