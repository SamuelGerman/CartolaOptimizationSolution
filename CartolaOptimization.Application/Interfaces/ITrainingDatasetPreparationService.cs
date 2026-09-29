using CartolaOptimization.Application.Models;

namespace CartolaOptimization.Application.Interfaces;

/// <summary>
/// Define operações administrativas usadas para preparar as rodadas persistidas para o treino do modelo.
/// </summary>
public interface ITrainingDatasetPreparationService
{
    Task<TrainingDatasetReadinessResult> RecomputeReadinessAsync(
        int fromRound,
        int toRound,
        CancellationToken cancellationToken = default);

    Task<CartolaBackfillResult> BackfillCartolaResultsAsync(
        int fromRound,
        int toRound,
        CancellationToken cancellationToken = default);

    Task<GatoMestreBackfillResult> BackfillGatoMestreAsync(
        int season,
        int fromRound,
        int toRound,
        CancellationToken cancellationToken = default);
}
