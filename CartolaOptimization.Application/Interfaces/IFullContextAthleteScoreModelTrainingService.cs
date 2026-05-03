using CartolaOptimization.Application.Models;

namespace CartolaOptimization.Application.Interfaces;

/// <summary>
/// Defines administrative operations that train and persist the v1 full-context athlete-score machine learning model.
/// </summary>
public interface IFullContextAthleteScoreModelTrainingService
{
    /// <summary>
    /// Trains and persists the full-context model. When <paramref name="hyperparameters"/> is
    /// <c>null</c> the implementation falls back to the values configured in
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
