using CartolaOptimization.Application.Models;

namespace CartolaOptimization.Application.Interfaces;

/// <summary>
/// Defines read operations that build an optimal predicted lineup for a target round under budget and formation constraints.
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
