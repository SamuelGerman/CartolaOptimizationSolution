using CartolaOptimization.Application.Models;

namespace CartolaOptimization.Application.Interfaces;

/// <summary>
/// Defines read operations that estimate the expected score of each athlete for a target round.
/// </summary>
public interface IAthleteProjectionService
{
    Task<AthleteRoundProjectionResult> GetRoundProjectionAsync(
        int round,
        int? positionCartolaId = null,
        int? clubCartolaId = null,
        int take = 100,
        CancellationToken cancellationToken = default);
}
