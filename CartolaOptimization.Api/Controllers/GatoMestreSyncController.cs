using CartolaOptimization.Application.Interfaces;
using CartolaOptimization.Application.Models;
using Microsoft.AspNetCore.Mvc;

namespace CartolaOptimization.Api.Controllers;

/// <summary>
/// Exposes administrative endpoints used to synchronize enrichment datasets sourced from the Gato Mestre API.
/// </summary>
[ApiController]
[Route("api/gatomestre/sync")]
[Produces("application/json")]
public sealed class GatoMestreSyncController(IGatoMestreSyncService gatoMestreSyncService) : ControllerBase
{
    /// <summary>
    /// Synchronizes Gato Mestre data for a specific season and round.
    /// </summary>
    /// <param name="season">Season used by the athlete round endpoint.</param>
    /// <param name="round">Round that should be synchronized.</param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <returns>A summary describing the synchronized entities and rows.</returns>
    [HttpPost("season/{season:int}/round/{round:int}")]
    [ProducesResponseType(typeof(GatoMestreSyncResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SyncRound(int season, int round, CancellationToken cancellationToken)
    {
        var result = await gatoMestreSyncService.SyncRoundAsync(season, round, cancellationToken);
        return Ok(result);
    }
}
