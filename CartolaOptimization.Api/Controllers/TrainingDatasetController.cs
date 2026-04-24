using CartolaOptimization.Application.Interfaces;
using CartolaOptimization.Application.Models;
using CartolaOptimization.Application.Utilities;
using Microsoft.AspNetCore.Mvc;

namespace CartolaOptimization.Api.Controllers;

/// <summary>
/// Exposes the principal administrative endpoints used to keep the training base current and operate the full-context model.
/// </summary>
[ApiController]
[Route("api/admin/training")]
[Produces("application/json")]
public sealed class TrainingDatasetController(
    ITrainingDatasetDiagnosticsService trainingDatasetDiagnosticsService,
    IEndOfRoundMaintenanceService endOfRoundMaintenanceService,
    IFullContextAthleteScoreModelTrainingService fullContextAthleteScoreModelTrainingService) : ControllerBase
{
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
    /// Exports the full-context training dataset slice as Excel so all engineered features can be inspected intentionally outside the API.
    /// </summary>
    /// <param name="fromRound">Optional initial round of the export window.</param>
    /// <param name="toRound">Optional final round of the export window.</param>
    /// <param name="take">Maximum number of rows returned. Limited to 5000 rows.</param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <returns>An Excel file with the full-context training dataset rows.</returns>
    [HttpGet("datasets/full-context/xlsx")]
    [Produces("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    public async Task<IActionResult> ExportFullContextDatasetXlsx(
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

        return CreateExcelFileResult("full-context", result.Rows, fromRound, toRound);
    }

    /// <summary>
    /// Trains and persists the principal full-context athlete-score machine learning model.
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
    /// Returns metadata about the currently persisted full-context athlete-score model artifact, if one is available.
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
    /// Executes the end-of-round maintenance workflow: synchronizes the current market, backfills only the missing historical closed rounds, refreshes readiness metadata and retrains the principal full-context model when needed.
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

    private FileContentResult CreateExcelFileResult<T>(string datasetKey, IReadOnlyCollection<T> rows, int? fromRound, int? toRound)
    {
        var content = TrainingDatasetExcelExporter.ExportXlsx(datasetKey, rows);
        var fileName = BuildDatasetFileName(datasetKey, fromRound, toRound);
        return File(content, TrainingDatasetExcelExporter.ContentType, fileName);
    }

    private static string BuildDatasetFileName(string datasetKey, int? fromRound, int? toRound)
    {
        var windowLabel = fromRound.HasValue || toRound.HasValue
            ? $"r{fromRound?.ToString() ?? "start"}-r{toRound?.ToString() ?? "end"}"
            : "all-rounds";

        return $"training-{datasetKey}-{windowLabel}.xlsx";
    }
}
