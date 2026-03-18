using CartolaOptimization.Application.Interfaces;
using CartolaOptimization.Application.Models;
using Microsoft.AspNetCore.Mvc;

namespace CartolaOptimization.Api.Controllers;

/// <summary>
/// Exposes administrative endpoints used to prepare persisted rounds for model training and historical enrichment.
/// </summary>
[ApiController]
[Route("api/admin/training")]
[Produces("application/json")]
public sealed class TrainingDatasetController(ITrainingDatasetPreparationService trainingDatasetPreparationService) : ControllerBase
{
    /// <summary>
    /// Recomputes training-readiness metadata for athlete and club rounds in the informed inclusive range.
    /// </summary>
    /// <param name="fromRound">Initial round of the recomputation range.</param>
    /// <param name="toRound">Final round of the recomputation range.</param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <returns>A summary describing how many rows are complete, partial or unfit for training.</returns>
    [HttpPost("readiness/recompute")]
    [ProducesResponseType(typeof(TrainingDatasetReadinessResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> RecomputeReadiness(
        [FromQuery] int fromRound,
        [FromQuery] int toRound,
        CancellationToken cancellationToken = default)
    {
        var result = await trainingDatasetPreparationService.RecomputeReadinessAsync(fromRound, toRound, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Executes a historical backfill of Gato Mestre rounds for the informed inclusive range.
    /// </summary>
    /// <param name="season">Season used by the Gato Mestre round endpoint.</param>
    /// <param name="fromRound">Initial round of the backfill range.</param>
    /// <param name="toRound">Final round of the backfill range.</param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <returns>A per-round summary of the historical backfill execution.</returns>
    [HttpPost("backfill/gatomestre/season/{season:int}")]
    [ProducesResponseType(typeof(GatoMestreBackfillResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> BackfillGatoMestre(
        int season,
        [FromQuery] int fromRound,
        [FromQuery] int toRound,
        CancellationToken cancellationToken = default)
    {
        var result = await trainingDatasetPreparationService.BackfillGatoMestreAsync(season, fromRound, toRound, cancellationToken);
        return Ok(result);
    }
}
