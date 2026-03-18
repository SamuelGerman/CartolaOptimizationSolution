using CartolaOptimization.Application.Interfaces;
using CartolaOptimization.Application.Models;
using Microsoft.AspNetCore.Mvc;

namespace CartolaOptimization.Api.Controllers;

/// <summary>
/// Exposes administrative endpoints used to synchronize data sourced from the Cartola API.
/// </summary>
[ApiController]
[Route("api/cartola/sync")]
[Produces("application/json")]
public sealed class CartolaSyncController(ICartolaSyncService cartolaSyncService) : ControllerBase
{
    /// <summary>
    /// Synchronizes the current Cartola catalog, athlete market data and the matches for the current round.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <returns>A summary describing the synchronized entities and rows.</returns>
    [HttpPost("current")]
    [ProducesResponseType(typeof(CartolaSyncResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SyncCurrent(CancellationToken cancellationToken)
    {
        var result = await cartolaSyncService.SyncAsync(cancellationToken: cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Synchronizes Cartola data while forcing the match synchronization to use a specific round.
    /// </summary>
    /// <param name="round">Round that should be used for the Cartola matches endpoint.</param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <returns>A summary describing the synchronized entities and rows.</returns>
    [HttpPost("matches/{round:int}")]
    [ProducesResponseType(typeof(CartolaSyncResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SyncMatchesRound(int round, CancellationToken cancellationToken)
    {
        var result = await cartolaSyncService.SyncAsync(round, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Synchronizes the final score payload of a closed historical Cartola round and applies it to already persisted athlete rounds.
    /// </summary>
    /// <param name="round">Closed historical round that should be finalized locally.</param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <returns>A summary describing the finalized athlete rounds and any remaining local gaps.</returns>
    [HttpPost("closed-round/{round:int}")]
    [ProducesResponseType(typeof(CartolaSyncResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SyncClosedRound(int round, CancellationToken cancellationToken)
    {
        var result = await cartolaSyncService.SyncClosedRoundAsync(round, cancellationToken);
        return Ok(result);
    }
}
