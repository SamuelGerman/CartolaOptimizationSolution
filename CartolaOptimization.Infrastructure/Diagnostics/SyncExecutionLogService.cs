using CartolaOptimization.Domain.Entities;
using CartolaOptimization.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CartolaOptimization.Infrastructure.Diagnostics;

/// <summary>
/// Armazena as linhas de execução de sincronização usando uma instância isolada de DbContext para que o histórico de execuções sobreviva a rollbacks.
/// </summary>
public sealed class SyncExecutionLogService(IDbContextFactory<AppDbContext> dbContextFactory) : ISyncExecutionLogService
{
    public async Task<int?> StartAsync(SyncExecutionLogEntry entry, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

            var entity = new SyncExecution
            {
                CorrelationId = entry.CorrelationId,
                SourceSystem = entry.SourceSystem,
                ProcessName = entry.ProcessName,
                StartedAtUtc = entry.StartedAtUtc,
                FinishedAtUtc = entry.FinishedAtUtc,
                DurationMs = entry.DurationMs,
                Status = entry.Status,
                LastStepName = entry.LastStepName,
                Season = entry.Season,
                Round = entry.Round,
                ErrorCode = entry.ErrorCode,
                Notes = entry.Notes,
                MetricsJson = entry.MetricsJson,
                ContextJson = entry.ContextJson,
                QualityCriteriaJson = entry.QualityCriteriaJson,
                CoverageRate = entry.CoverageRate,
                SkippedRate = entry.SkippedRate,
                QualityCriteriaSatisfied = entry.QualityCriteriaSatisfied,
                WarningLogsCount = entry.WarningLogsCount,
                ErrorLogsCount = entry.ErrorLogsCount,
            };

            dbContext.SyncExecutions.Add(entity);
            await dbContext.SaveChangesAsync(cancellationToken);

            return entity.Id;
        }
        catch
        {
            return null;
        }
    }

    public async Task UpdateAsync(int executionId, SyncExecutionLogEntry entry, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var entity = await dbContext.SyncExecutions.FirstOrDefaultAsync(item => item.Id == executionId, cancellationToken);
            if (entity is null)
            {
                return;
            }

            entity.FinishedAtUtc = entry.FinishedAtUtc;
            entity.DurationMs = entry.DurationMs;
            entity.Status = entry.Status;
            entity.LastStepName = entry.LastStepName;
            entity.Season = entry.Season;
            entity.Round = entry.Round;
            entity.ErrorCode = entry.ErrorCode;
            entity.Notes = entry.Notes;
            entity.MetricsJson = entry.MetricsJson;
            entity.ContextJson = entry.ContextJson;
            entity.QualityCriteriaJson = entry.QualityCriteriaJson;
            entity.CoverageRate = entry.CoverageRate;
            entity.SkippedRate = entry.SkippedRate;
            entity.QualityCriteriaSatisfied = entry.QualityCriteriaSatisfied;
            entity.WarningLogsCount = entry.WarningLogsCount;
            entity.ErrorLogsCount = entry.ErrorLogsCount;

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
        }
    }
}
