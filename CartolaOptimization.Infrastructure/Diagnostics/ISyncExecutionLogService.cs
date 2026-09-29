namespace CartolaOptimization.Infrastructure.Diagnostics;

/// <summary>
/// Persiste o ciclo de vida das execuções de sincronização fora da transação principal de negócio.
/// </summary>
public interface ISyncExecutionLogService
{
    Task<int?> StartAsync(SyncExecutionLogEntry entry, CancellationToken cancellationToken = default);

    Task UpdateAsync(int executionId, SyncExecutionLogEntry entry, CancellationToken cancellationToken = default);
}
