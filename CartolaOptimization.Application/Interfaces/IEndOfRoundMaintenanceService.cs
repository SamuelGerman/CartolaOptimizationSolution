using CartolaOptimization.Application.Models;

namespace CartolaOptimization.Application.Interfaces;

/// <summary>
/// Define o contrato do fluxo de manutenção de fim de rodada que atualiza os dados históricos e retreina o modelo full-context principal.
/// </summary>
public interface IEndOfRoundMaintenanceService
{
    Task<EndOfRoundMaintenanceResult> RunAsync(
        int validationRounds = 1,
        bool forceLatestClosedRound = false,
        CancellationToken cancellationToken = default);
}
