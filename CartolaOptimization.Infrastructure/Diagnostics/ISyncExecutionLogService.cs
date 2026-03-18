namespace CartolaOptimization.Infrastructure.Diagnostics;

/// <summary>
/// Persists the lifecycle of synchronization executions outside the main business transaction.
/// </summary>
public interface ISyncExecutionLogService
{
    Task<int?> StartAsync(SyncExecutionLogEntry entry, CancellationToken cancellationToken = default);

    Task UpdateAsync(int executionId, SyncExecutionLogEntry entry, CancellationToken cancellationToken = default);
}
