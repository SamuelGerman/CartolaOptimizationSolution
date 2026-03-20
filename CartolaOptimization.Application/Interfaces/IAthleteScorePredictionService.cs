using CartolaOptimization.Application.Models;

namespace CartolaOptimization.Application.Interfaces;

/// <summary>
/// Defines read operations that return machine-learning athlete score predictions for a target round.
/// </summary>
public interface IAthleteScorePredictionService
{
    Task<AthleteScorePredictionResult> GetPredictedScoresAsync(
        int round,
        int? statusCartolaId = null,
        int? clubCartolaId = null,
        int? positionCartolaId = null,
        int take = 100,
        CancellationToken cancellationToken = default);
}
