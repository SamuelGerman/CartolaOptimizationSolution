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
public sealed class TrainingDatasetController(
    ITrainingDatasetPreparationService trainingDatasetPreparationService,
    ITrainingDatasetDiagnosticsService trainingDatasetDiagnosticsService) : ControllerBase
{
    /// <summary>
    /// Returns an aggregated summary of how ready the persisted athlete and club rounds are for model training.
    /// </summary>
    /// <param name="fromRound">Optional initial round of the summary window.</param>
    /// <param name="toRound">Optional final round of the summary window.</param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <returns>Aggregated readiness counters, rates and top blocking reasons for the requested window.</returns>
    [HttpGet("readiness/summary")]
    [ProducesResponseType(typeof(TrainingDatasetCoverageSummary), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCoverageSummary(
        [FromQuery] int? fromRound,
        [FromQuery] int? toRound,
        CancellationToken cancellationToken = default)
    {
        var result = await trainingDatasetDiagnosticsService.GetCoverageSummaryAsync(fromRound, toRound, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Returns filtered athlete rounds together with phase and readiness metadata so incomplete training rows can be inspected quickly.
    /// </summary>
    /// <param name="fromRound">Optional initial round of the filter window.</param>
    /// <param name="toRound">Optional final round of the filter window.</param>
    /// <param name="readinessStatus">Optional readiness filter, such as Complete, Partial or UnfitForTraining.</param>
    /// <param name="dataPhase">Optional phase filter, such as HistoricalClosed, CurrentOpen or Unknown.</param>
    /// <param name="isTrainingEligible">Optional eligibility filter.</param>
    /// <param name="take">Maximum number of rows returned. Limited to 200 rows.</param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <returns>A filtered list of athlete-round rows prepared for readiness inspection.</returns>
    [HttpGet("readiness/athlete-rounds")]
    [ProducesResponseType(typeof(IReadOnlyCollection<TrainingAthleteRoundListItem>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAthleteRounds(
        [FromQuery] int? fromRound,
        [FromQuery] int? toRound,
        [FromQuery] string? readinessStatus,
        [FromQuery] string? dataPhase,
        [FromQuery] bool? isTrainingEligible,
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default)
    {
        var result = await trainingDatasetDiagnosticsService.GetAthleteRoundsAsync(
            fromRound,
            toRound,
            readinessStatus,
            dataPhase,
            isTrainingEligible,
            take,
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Returns filtered club-round enrichment rows together with readiness metadata.
    /// </summary>
    /// <param name="fromRound">Optional initial round of the filter window.</param>
    /// <param name="toRound">Optional final round of the filter window.</param>
    /// <param name="readinessStatus">Optional readiness filter, such as Complete, Partial or UnfitForTraining.</param>
    /// <param name="isTrainingEligible">Optional eligibility filter.</param>
    /// <param name="take">Maximum number of rows returned. Limited to 200 rows.</param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <returns>A filtered list of club-round rows prepared for readiness inspection.</returns>
    [HttpGet("readiness/club-rounds")]
    [ProducesResponseType(typeof(IReadOnlyCollection<TrainingClubRoundListItem>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetClubRounds(
        [FromQuery] int? fromRound,
        [FromQuery] int? toRound,
        [FromQuery] string? readinessStatus,
        [FromQuery] bool? isTrainingEligible,
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default)
    {
        var result = await trainingDatasetDiagnosticsService.GetClubRoundsAsync(
            fromRound,
            toRound,
            readinessStatus,
            isTrainingEligible,
            take,
            cancellationToken);

        return Ok(result);
    }

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
    /// Executes a historical backfill of closed Cartola rounds for the informed inclusive range.
    /// </summary>
    /// <param name="fromRound">Initial round of the backfill range.</param>
    /// <param name="toRound">Final round of the backfill range.</param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <returns>A per-round summary of the historical Cartola final-score backfill execution.</returns>
    [HttpPost("backfill/cartola/results")]
    [ProducesResponseType(typeof(CartolaBackfillResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> BackfillCartolaResults(
        [FromQuery] int fromRound,
        [FromQuery] int toRound,
        CancellationToken cancellationToken = default)
    {
        var result = await trainingDatasetPreparationService.BackfillCartolaResultsAsync(fromRound, toRound, cancellationToken);
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
