using CartolaOptimization.Application.Models;

namespace CartolaOptimization.Application.Interfaces;

/// <summary>
/// Defines the contract for synchronizing the datasets sourced from the public Cartola API.
/// </summary>
public interface ICartolaSyncService
{
    /// <summary>
    /// Synchronizes the current Cartola catalog and athlete market data, and optionally overrides the round used to load matches.
    /// </summary>
    /// <param name="matchesRound">
    /// Optional round override used for the matches endpoint. When omitted, the service uses the current round returned by the Cartola market status endpoint.
    /// </param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A summary of the synchronization execution.</returns>
    Task<CartolaSyncResult> SyncAsync(int? matchesRound = null, CancellationToken cancellationToken = default);
}
