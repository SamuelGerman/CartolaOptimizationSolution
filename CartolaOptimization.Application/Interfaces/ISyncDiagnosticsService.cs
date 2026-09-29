using CartolaOptimization.Application.Models;

namespace CartolaOptimization.Application.Interfaces;

/// <summary>
/// Define operações administrativas de leitura para o histórico de execuções de sincronização e os diagnósticos de falha.
/// </summary>
public interface ISyncDiagnosticsService
{
    Task<IReadOnlyCollection<SyncExecutionListItem>> GetSyncExecutionsAsync(
        string? sourceSystem = null,
        string? status = null,
        int take = 50,
        CancellationToken cancellationToken = default);

    Task<SyncExecutionDetails?> GetSyncExecutionByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<IntegrationFailureLogListItem>> GetFailureLogsAsync(
        string? sourceSystem = null,
        string? correlationId = null,
        string? errorCode = null,
        string? severity = null,
        int take = 100,
        CancellationToken cancellationToken = default);

    Task<IntegrationFailureLogDetails?> GetFailureLogByIdAsync(int id, CancellationToken cancellationToken = default);
}
