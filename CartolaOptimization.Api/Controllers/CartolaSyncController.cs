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
    /// Finalizes a single closed historical round using Cartola data only (no Gato Mestre, no training):
    /// fetches the final score payload, applies it to the persisted athlete-round baselines and marks the
    /// round as historically closed so it becomes eligible for model training. Use this to retroactively
    /// close rounds that elapsed without an end-of-round run. The round must be earlier than the current round.
    /// </summary>
    /// <param name="round">Closed historical round to finalize.</param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <returns>A summary describing the finalized round.</returns>
    [HttpPost("closed-round/{round:int}")]
    [ProducesResponseType(typeof(CartolaSyncResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SyncClosedRound(int round, CancellationToken cancellationToken)
    {
        if (round <= 0)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid round", Detail = "Round must be greater than zero." });
        }

        var result = await cartolaSyncService.SyncClosedRoundAsync(round, cancellationToken);
        return Ok(result);
    }
}
