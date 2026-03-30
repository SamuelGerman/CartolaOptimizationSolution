using CartolaOptimization.Application.Models;

namespace CartolaOptimization.Application.Interfaces;

/// <summary>
/// Defines the contract for the end-of-round maintenance workflow that refreshes historical data and retrains the primary full-context model.
/// </summary>
public interface IEndOfRoundMaintenanceService
{
    Task<EndOfRoundMaintenanceResult> RunAsync(
        int validationRounds = 1,
        bool forceLatestClosedRound = false,
        CancellationToken cancellationToken = default);
}
