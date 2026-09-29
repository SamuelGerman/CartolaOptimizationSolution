using CartolaOptimization.Application.Models;

namespace CartolaOptimization.Application.Interfaces;

/// <summary>
/// Define operações de leitura que montam uma escalação prevista ótima para uma rodada-alvo sob restrições de orçamento e formação.
/// </summary>
public interface IPredictedLineupOptimizationService
{
    Task<PredictedLineupResult> GetOptimalLineupAsync(
        int round,
        PredictedLineupRequest request,
        CancellationToken cancellationToken = default);

    Task<PredictedLineupResult> GetOptimalFullContextLineupAsync(
        int round,
        PredictedLineupRequest request,
        CancellationToken cancellationToken = default);

    Task<PredictedLineupResult> GetOptimalAthleteContextLineupAsync(
        int round,
        PredictedLineupRequest request,
        CancellationToken cancellationToken = default);
}
