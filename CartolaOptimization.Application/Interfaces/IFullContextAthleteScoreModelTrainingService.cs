using CartolaOptimization.Application.Models;

namespace CartolaOptimization.Application.Interfaces;

/// <summary>
/// Define operações administrativas que treinam e persistem o modelo de aprendizado de máquina de pontuação de atletas full-context v1.
/// </summary>
public interface IFullContextAthleteScoreModelTrainingService
{
    /// <summary>
    /// Treina e persiste o modelo full-context. Quando <paramref name="hyperparameters"/> é
    /// <c>null</c>, a implementação recorre aos valores configurados em
    /// <c>MachineLearning:FullContext:Hyperparameters</c>.
    /// </summary>
    Task<AthleteScoreModelTrainingResult> TrainAsync(
        int? fromRound = null,
        int? toRound = null,
        int validationRounds = 1,
        int? topNFeatures = null,
        LightGbmHyperparameters? hyperparameters = null,
        CancellationToken cancellationToken = default);

    Task<AthleteScoreModelInfo?> GetCurrentModelInfoAsync(CancellationToken cancellationToken = default);
}
