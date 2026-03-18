using CartolaOptimization.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CartolaOptimization.Infrastructure.Data.Configurations;

public sealed class IntegrationFailureLogConfiguration : IEntityTypeConfiguration<IntegrationFailureLog>
{
    public void Configure(EntityTypeBuilder<IntegrationFailureLog> builder)
    {
        builder.ToTable("IntegrationFailureLogs");

        builder.HasKey(log => log.Id);

        builder.HasIndex(log => log.OccurredAtUtc);
        builder.HasIndex(log => log.CorrelationId);
        builder.HasIndex(log => new { log.SourceSystem, log.ProcessName, log.OccurredAtUtc });

        builder.Property(log => log.CorrelationId).IsRequired().HasMaxLength(50);
        builder.Property(log => log.SourceSystem).IsRequired().HasMaxLength(50);
        builder.Property(log => log.ProcessName).IsRequired().HasMaxLength(150);
        builder.Property(log => log.StepName).IsRequired().HasMaxLength(200);
        builder.Property(log => log.ErrorLocation).IsRequired().HasMaxLength(200);
        builder.Property(log => log.RequestMethod).IsRequired().HasMaxLength(10);
        builder.Property(log => log.RequestUrl).IsRequired().HasMaxLength(500);
        builder.Property(log => log.ResponseReasonPhrase).IsRequired().HasMaxLength(100);
        builder.Property(log => log.ExceptionType).IsRequired().HasMaxLength(300);
        builder.Property(log => log.ExceptionMessage).IsRequired().HasMaxLength(4000);
        builder.Property(log => log.InnerExceptionMessage).IsRequired().HasMaxLength(4000);

        builder.Property(log => log.RequestHeadersJson).HasColumnType("longtext");
        builder.Property(log => log.RequestPayload).HasColumnType("longtext");
        builder.Property(log => log.ResponseHeadersJson).HasColumnType("longtext");
        builder.Property(log => log.ResponsePayload).HasColumnType("longtext");
        builder.Property(log => log.ExecutedStepsJson).HasColumnType("longtext");
        builder.Property(log => log.ContextJson).HasColumnType("longtext");
        builder.Property(log => log.StackTrace).HasColumnType("longtext");
    }
}
