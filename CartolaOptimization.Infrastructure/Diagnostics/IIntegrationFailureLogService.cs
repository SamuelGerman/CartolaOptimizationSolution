namespace CartolaOptimization.Infrastructure.Diagnostics;

/// <summary>
/// Persiste os diagnósticos de falha de sincronização fora da transação principal de ingestão.
/// </summary>
public interface IIntegrationFailureLogService
{
    /// <summary>
    /// Tenta persistir uma entrada de log de falha e retorna seu identificador no banco quando a operação tem sucesso.
    /// </summary>
    Task<int?> TryLogAsync(IntegrationFailureLogEntry entry, CancellationToken cancellationToken = default);
}
