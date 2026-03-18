using CartolaOptimization.Application.Models;

namespace CartolaOptimization.Application.Interfaces;

/// <summary>
/// Defines administrative operations used to prepare the persisted rounds for model training.
/// </summary>
public interface ITrainingDatasetPreparationService
{
    Task<TrainingDatasetReadinessResult> RecomputeReadinessAsync(
        int fromRound,
        int toRound,
        CancellationToken cancellationToken = default);

    Task<GatoMestreBackfillResult> BackfillGatoMestreAsync(
        int season,
        int fromRound,
        int toRound,
        CancellationToken cancellationToken = default);
}
