using CartolaOptimization.Application.Interfaces;
using CartolaOptimization.Application.Models;
using Microsoft.AspNetCore.Mvc;

namespace CartolaOptimization.Api.Controllers;

/// <summary>
/// Exposes read endpoints that estimate the expected score of athletes for a target round.
/// </summary>
[ApiController]
[Route("api/projections")]
[Produces("application/json")]
public sealed class AthleteProjectionController(IAthleteProjectionService athleteProjectionService) : ControllerBase
{
    /// <summary>
    /// Returns an explainable expected-score projection for the athletes that can be evaluated for the informed round.
    /// </summary>
    /// <param name="round">Target round for the projection.</param>
    /// <param name="positionCartolaId">Optional Cartola position filter.</param>
    /// <param name="clubCartolaId">Optional Cartola club filter.</param>
    /// <param name="take">Maximum number of projected athletes returned. Limited to 500 rows.</param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <returns>A ranked list of athlete expected scores together with the main factors behind each projection.</returns>
    [HttpGet("round/{round:int}/athletes")]
    [ProducesResponseType(typeof(AthleteRoundProjectionResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRoundProjection(
        int round,
        [FromQuery] int? positionCartolaId,
        [FromQuery] int? clubCartolaId,
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default)
    {
        var result = await athleteProjectionService.GetRoundProjectionAsync(
            round,
            positionCartolaId,
            clubCartolaId,
            take,
            cancellationToken);

        return Ok(result);
    }
}
