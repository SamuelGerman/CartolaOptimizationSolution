namespace CartolaOptimization.Infrastructure.Diagnostics;

/// <summary>
/// Persists synchronization failure diagnostics outside the main ingestion transaction.
/// </summary>
public interface IIntegrationFailureLogService
{
    /// <summary>
    /// Tries to persist a failure log entry and returns its database identifier when the operation succeeds.
    /// </summary>
    Task<int?> TryLogAsync(IntegrationFailureLogEntry entry, CancellationToken cancellationToken = default);
}
