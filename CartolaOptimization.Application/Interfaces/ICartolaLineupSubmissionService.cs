using CartolaOptimization.Application.Models;

namespace CartolaOptimization.Application.Interfaces;

/// <summary>
/// Defines operations that submit an optimized lineup to the authenticated Cartola team endpoint.
/// </summary>
public interface ICartolaLineupSubmissionService
{
    Task<CartolaLineupSubmissionResult> SubmitAsync(
        int round,
        PredictedLineupResult lineup,
        PredictedLineupRequest request,
        string? cartolaAuthorization,
        string? cartolaGlbTag = null,
        CancellationToken cancellationToken = default);
}
