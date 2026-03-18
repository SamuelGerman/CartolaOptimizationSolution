using CartolaOptimization.Application.Interfaces;
using CartolaOptimization.Application.Models;
using CartolaOptimization.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CartolaOptimization.Infrastructure.Services;

/// <summary>
/// Provides administrative read access to synchronization history and persisted failure diagnostics.
/// </summary>
public sealed class SyncDiagnosticsService(AppDbContext dbContext) : ISyncDiagnosticsService
{
    public async Task<IReadOnlyCollection<SyncExecutionListItem>> GetSyncExecutionsAsync(
        string? sourceSystem = null,
        string? status = null,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 200);

        var query = dbContext.SyncExecutions.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(sourceSystem))
        {
            query = query.Where(item => item.SourceSystem == sourceSystem);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(item => item.Status == status);
        }

        return await query
            .OrderByDescending(item => item.StartedAtUtc)
            .Take(take)
            .Select(item => new SyncExecutionListItem
            {
                Id = item.Id,
                CorrelationId = item.CorrelationId,
                SourceSystem = item.SourceSystem,
                ProcessName = item.ProcessName,
                StartedAtUtc = item.StartedAtUtc,
                FinishedAtUtc = item.FinishedAtUtc,
                DurationMs = item.DurationMs,
                Status = item.Status,
                Season = item.Season,
                Round = item.Round,
                ErrorCode = item.ErrorCode,
                CoverageRate = item.CoverageRate,
                SkippedRate = item.SkippedRate,
                QualityCriteriaSatisfied = item.QualityCriteriaSatisfied,
                WarningLogsCount = item.WarningLogsCount,
                ErrorLogsCount = item.ErrorLogsCount,
                Notes = item.Notes,
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<SyncExecutionDetails?> GetSyncExecutionByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await dbContext.SyncExecutions
            .AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new SyncExecutionDetails
            {
                Id = item.Id,
                CorrelationId = item.CorrelationId,
                SourceSystem = item.SourceSystem,
                ProcessName = item.ProcessName,
                StartedAtUtc = item.StartedAtUtc,
                FinishedAtUtc = item.FinishedAtUtc,
                DurationMs = item.DurationMs,
                Status = item.Status,
                LastStepName = item.LastStepName,
                Season = item.Season,
                Round = item.Round,
                ErrorCode = item.ErrorCode,
                Notes = item.Notes,
                MetricsJson = item.MetricsJson,
                ContextJson = item.ContextJson,
                QualityCriteriaJson = item.QualityCriteriaJson,
                CoverageRate = item.CoverageRate,
                SkippedRate = item.SkippedRate,
                QualityCriteriaSatisfied = item.QualityCriteriaSatisfied,
                WarningLogsCount = item.WarningLogsCount,
                ErrorLogsCount = item.ErrorLogsCount,
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<IntegrationFailureLogListItem>> GetFailureLogsAsync(
        string? sourceSystem = null,
        string? correlationId = null,
        string? errorCode = null,
        string? severity = null,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 200);

        var query = dbContext.IntegrationFailureLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(sourceSystem))
        {
            query = query.Where(item => item.SourceSystem == sourceSystem);
        }

        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            query = query.Where(item => item.CorrelationId == correlationId);
        }

        if (!string.IsNullOrWhiteSpace(errorCode))
        {
            query = query.Where(item => item.ErrorCode == errorCode);
        }

        if (!string.IsNullOrWhiteSpace(severity))
        {
            query = query.Where(item => item.Severity == severity);
        }

        return await query
            .OrderByDescending(item => item.OccurredAtUtc)
            .Take(take)
            .Select(item => new IntegrationFailureLogListItem
            {
                Id = item.Id,
                OccurredAtUtc = item.OccurredAtUtc,
                CorrelationId = item.CorrelationId,
                SourceSystem = item.SourceSystem,
                ProcessName = item.ProcessName,
                StepName = item.StepName,
                ErrorCode = item.ErrorCode,
                Severity = item.Severity,
                ExceptionType = item.ExceptionType,
                ExceptionMessage = item.ExceptionMessage,
                ResponseStatusCode = item.ResponseStatusCode,
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IntegrationFailureLogDetails?> GetFailureLogByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await dbContext.IntegrationFailureLogs
            .AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new IntegrationFailureLogDetails
            {
                Id = item.Id,
                OccurredAtUtc = item.OccurredAtUtc,
                CorrelationId = item.CorrelationId,
                SourceSystem = item.SourceSystem,
                ProcessName = item.ProcessName,
                StepName = item.StepName,
                ErrorLocation = item.ErrorLocation,
                ErrorCode = item.ErrorCode,
                Severity = item.Severity,
                RequestMethod = item.RequestMethod,
                RequestUrl = item.RequestUrl,
                RequestHeadersJson = item.RequestHeadersJson,
                RequestPayload = item.RequestPayload,
                ResponseStatusCode = item.ResponseStatusCode,
                ResponseReasonPhrase = item.ResponseReasonPhrase,
                ResponseHeadersJson = item.ResponseHeadersJson,
                ResponsePayload = item.ResponsePayload,
                ExecutedStepsJson = item.ExecutedStepsJson,
                ContextJson = item.ContextJson,
                ExceptionType = item.ExceptionType,
                ExceptionMessage = item.ExceptionMessage,
                InnerExceptionMessage = item.InnerExceptionMessage,
                StackTrace = item.StackTrace,
                TransactionRolledBack = item.TransactionRolledBack,
            })
            .FirstOrDefaultAsync(cancellationToken);
    }
}
