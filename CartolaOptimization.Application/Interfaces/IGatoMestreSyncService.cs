using CartolaOptimization.Application.Models;

namespace CartolaOptimization.Application.Interfaces;

/// <summary>
/// Defines the contract for synchronizing the datasets sourced from the public Gato Mestre API.
/// </summary>
public interface IGatoMestreSyncService
{
    /// <summary>
    /// Synchronizes Gato Mestre datasets for a specific season and round, enriching team-level and athlete-level round data.
    /// </summary>
    /// <param name="season">Season used by the Gato Mestre athletes round endpoint.</param>
    /// <param name="round">Round that should be synchronized.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A summary of the synchronization execution.</returns>
    Task<GatoMestreSyncResult> SyncRoundAsync(int season, int round, CancellationToken cancellationToken = default);
}
