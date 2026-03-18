using CartolaOptimization.Domain.Entities;
using CartolaOptimization.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CartolaOptimization.Infrastructure.Diagnostics;

/// <summary>
/// Stores failure diagnostics using an isolated DbContext instance so that logs survive synchronization rollbacks.
/// </summary>
public sealed class IntegrationFailureLogService(IDbContextFactory<AppDbContext> dbContextFactory) : IIntegrationFailureLogService
{
    public async Task<int?> TryLogAsync(IntegrationFailureLogEntry entry, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

            var entity = new IntegrationFailureLog
            {
                OccurredAtUtc = entry.OccurredAtUtc,
                CorrelationId = entry.CorrelationId,
                SourceSystem = entry.SourceSystem,
                ProcessName = entry.ProcessName,
                StepName = entry.StepName,
                ErrorLocation = entry.ErrorLocation,
                RequestMethod = entry.RequestMethod,
                RequestUrl = entry.RequestUrl,
                RequestHeadersJson = entry.RequestHeadersJson,
                RequestPayload = entry.RequestPayload,
                ResponseStatusCode = entry.ResponseStatusCode,
                ResponseReasonPhrase = entry.ResponseReasonPhrase,
                ResponseHeadersJson = entry.ResponseHeadersJson,
                ResponsePayload = entry.ResponsePayload,
                ExecutedStepsJson = entry.ExecutedStepsJson,
                ContextJson = entry.ContextJson,
                ErrorCode = entry.ErrorCode,
                Severity = entry.Severity,
                ExceptionType = entry.ExceptionType,
                ExceptionMessage = entry.ExceptionMessage,
                InnerExceptionMessage = entry.InnerExceptionMessage,
                StackTrace = entry.StackTrace,
                TransactionRolledBack = entry.TransactionRolledBack,
            };

            dbContext.IntegrationFailureLogs.Add(entity);
            await dbContext.SaveChangesAsync(cancellationToken);

            return entity.Id;
        }
        catch
        {
            return null;
        }
    }
}
