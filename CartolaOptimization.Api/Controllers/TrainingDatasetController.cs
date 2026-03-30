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
    ITrainingDatasetDiagnosticsService trainingDatasetDiagnosticsService,
    IEndOfRoundMaintenanceService endOfRoundMaintenanceService,
    IAthleteScoreModelTrainingService athleteScoreModelTrainingService,
    IAthleteContextAthleteScoreModelTrainingService athleteContextAthleteScoreModelTrainingService,
    IFullContextAthleteScoreModelTrainingService fullContextAthleteScoreModelTrainingService) : ControllerBase
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
    /// Returns an export-ready athlete-only historical dataset that can already be used by baseline regression models.
    /// </summary>
    /// <param name="fromRound">Optional initial round of the export window.</param>
    /// <param name="toRound">Optional final round of the export window.</param>
    /// <param name="take">Maximum number of rows returned. Limited to 5000 rows.</param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <returns>A historical dataset slice with athlete-only predictive features and target values.</returns>
    [HttpGet("datasets/athlete-only")]
    [ProducesResponseType(typeof(TrainingAthleteDatasetResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAthleteOnlyDataset(
        [FromQuery] int? fromRound,
        [FromQuery] int? toRound,
        [FromQuery] int take = 1000,
        CancellationToken cancellationToken = default)
    {
        var result = await trainingDatasetDiagnosticsService.GetAthleteOnlyDatasetAsync(
            fromRound,
            toRound,
            take,
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Returns an export-ready historical dataset slice that combines athlete-level Gato Mestre signals with Cartola match context.
    /// </summary>
    /// <param name="fromRound">Optional initial round of the export window.</param>
    /// <param name="toRound">Optional final round of the export window.</param>
    /// <param name="take">Maximum number of rows returned. Limited to 5000 rows.</param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <returns>A historical dataset slice with athlete and match context that does not depend on club-round enrichment.</returns>
    [HttpGet("datasets/athlete-context")]
    [ProducesResponseType(typeof(TrainingAthleteContextDatasetResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAthleteContextDataset(
        [FromQuery] int? fromRound,
        [FromQuery] int? toRound,
        [FromQuery] int take = 1000,
        CancellationToken cancellationToken = default)
    {
        var result = await trainingDatasetDiagnosticsService.GetAthleteContextDatasetAsync(
            fromRound,
            toRound,
            take,
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Returns an export-ready historical dataset slice that includes athlete, club, opponent and match context.
    /// </summary>
    /// <param name="fromRound">Optional initial round of the export window.</param>
    /// <param name="toRound">Optional final round of the export window.</param>
    /// <param name="take">Maximum number of rows returned. Limited to 5000 rows.</param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <returns>A historical dataset slice with full contextual features and target values.</returns>
    [HttpGet("datasets/full-context")]
    [ProducesResponseType(typeof(TrainingFullContextDatasetResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFullContextDataset(
        [FromQuery] int? fromRound,
        [FromQuery] int? toRound,
        [FromQuery] int take = 1000,
        CancellationToken cancellationToken = default)
    {
        var result = await trainingDatasetDiagnosticsService.GetFullContextDatasetAsync(
            fromRound,
            toRound,
            take,
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Trains and persists the v0 athlete-score machine learning model using the athlete-only historical dataset.
    /// </summary>
    /// <param name="fromRound">Optional initial round of the training window.</param>
    /// <param name="toRound">Optional final round of the training window.</param>
    /// <param name="validationRounds">Number of last rounds reserved for temporal validation. Defaults to one round.</param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <returns>The model metadata and validation metrics of the newly trained artifact.</returns>
    [HttpPost("models/athlete-score-v0/train")]
    [ProducesResponseType(typeof(AthleteScoreModelTrainingResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> TrainAthleteScoreModel(
        [FromQuery] int? fromRound,
        [FromQuery] int? toRound,
        [FromQuery] int validationRounds = 1,
        CancellationToken cancellationToken = default)
    {
        var result = await athleteScoreModelTrainingService.TrainAsync(
            fromRound,
            toRound,
            validationRounds,
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Returns metadata about the currently persisted v0 athlete-score model artifact, if one is available.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <returns>The active model metadata or 404 when the artifact has not been trained yet.</returns>
    [HttpGet("models/athlete-score-v0")]
    [ProducesResponseType(typeof(AthleteScoreModelInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAthleteScoreModelInfo(CancellationToken cancellationToken = default)
    {
        var result = await athleteScoreModelTrainingService.GetCurrentModelInfoAsync(cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Trains and persists the v1 athlete-context machine learning model using athlete-level Gato Mestre signals and Cartola match context.
    /// </summary>
    /// <param name="fromRound">Optional initial round of the training window.</param>
    /// <param name="toRound">Optional final round of the training window.</param>
    /// <param name="validationRounds">Number of last rounds reserved for temporal validation. Defaults to one round.</param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <returns>The model metadata and validation metrics of the newly trained athlete-context artifact.</returns>
    [HttpPost("models/athlete-score-v1-athlete-context/train")]
    [ProducesResponseType(typeof(AthleteScoreModelTrainingResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> TrainAthleteContextAthleteScoreModel(
        [FromQuery] int? fromRound,
        [FromQuery] int? toRound,
        [FromQuery] int validationRounds = 1,
        CancellationToken cancellationToken = default)
    {
        var result = await athleteContextAthleteScoreModelTrainingService.TrainAsync(
            fromRound,
            toRound,
            validationRounds,
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Returns metadata about the currently persisted v1 athlete-context athlete-score model artifact, if one is available.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <returns>The active athlete-context model metadata or 404 when the artifact has not been trained yet.</returns>
    [HttpGet("models/athlete-score-v1-athlete-context")]
    [ProducesResponseType(typeof(AthleteScoreModelInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAthleteContextAthleteScoreModelInfo(CancellationToken cancellationToken = default)
    {
        var result = await athleteContextAthleteScoreModelTrainingService.GetCurrentModelInfoAsync(cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Trains and persists the v1 full-context athlete-score machine learning model using athlete, club, opponent and match context.
    /// </summary>
    /// <param name="fromRound">Optional initial round of the training window.</param>
    /// <param name="toRound">Optional final round of the training window.</param>
    /// <param name="validationRounds">Number of last rounds reserved for temporal validation. Defaults to one round.</param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <returns>The model metadata and validation metrics of the newly trained full-context artifact.</returns>
    [HttpPost("models/athlete-score-v1-full-context/train")]
    [ProducesResponseType(typeof(AthleteScoreModelTrainingResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> TrainFullContextAthleteScoreModel(
        [FromQuery] int? fromRound,
        [FromQuery] int? toRound,
        [FromQuery] int validationRounds = 1,
        CancellationToken cancellationToken = default)
    {
        var result = await fullContextAthleteScoreModelTrainingService.TrainAsync(
            fromRound,
            toRound,
            validationRounds,
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Returns metadata about the currently persisted v1 full-context athlete-score model artifact, if one is available.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <returns>The active full-context model metadata or 404 when the artifact has not been trained yet.</returns>
    [HttpGet("models/athlete-score-v1-full-context")]
    [ProducesResponseType(typeof(AthleteScoreModelInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFullContextAthleteScoreModelInfo(CancellationToken cancellationToken = default)
    {
        var result = await fullContextAthleteScoreModelTrainingService.GetCurrentModelInfoAsync(cancellationToken);
        return result is null ? NotFound() : Ok(result);
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

    /// <summary>
    /// Executes the end-of-round maintenance workflow: synchronizes the current market, backfills only the missing historical closed rounds, refreshes readiness metadata and retrains the primary full-context model when needed.
    /// </summary>
    /// <param name="validationRounds">Number of last rounds reserved for temporal validation when the model is retrained. Defaults to one round.</param>
    /// <param name="forceLatestClosedRound">When true, forces the latest closed round to be reprocessed even if it was already finalized before.</param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <returns>A consolidated summary of the automated end-of-round maintenance workflow.</returns>
    [HttpPost("maintenance/end-of-round")]
    [ProducesResponseType(typeof(EndOfRoundMaintenanceResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> RunEndOfRoundMaintenance(
        [FromQuery] int validationRounds = 1,
        [FromQuery] bool forceLatestClosedRound = false,
        CancellationToken cancellationToken = default)
    {
        var result = await endOfRoundMaintenanceService.RunAsync(
            validationRounds,
            forceLatestClosedRound,
            cancellationToken);

        return Ok(result);
    }
}
