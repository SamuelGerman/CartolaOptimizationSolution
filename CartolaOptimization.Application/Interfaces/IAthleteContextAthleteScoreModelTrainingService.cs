using CartolaOptimization.Application.Models;

namespace CartolaOptimization.Application.Interfaces;

/// <summary>
/// Define operações administrativas que treinam e persistem o modelo de aprendizado de máquina de contexto de atleta v1.
/// </summary>
public interface IAthleteContextAthleteScoreModelTrainingService
{
    Task<AthleteScoreModelTrainingResult> TrainAsync(
        int? fromRound = null,
        int? toRound = null,
        int validationRounds = 1,
        CancellationToken cancellationToken = default);

    Task<AthleteScoreModelInfo?> GetCurrentModelInfoAsync(CancellationToken cancellationToken = default);
}
