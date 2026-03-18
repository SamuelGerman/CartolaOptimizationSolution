using CartolaOptimization.Application.Models;

namespace CartolaOptimization.Application.Interfaces;

public interface ICartolaSyncService
{
    Task<CartolaSyncResult> SyncAsync(int? matchesRound = null, CancellationToken cancellationToken = default);
}
