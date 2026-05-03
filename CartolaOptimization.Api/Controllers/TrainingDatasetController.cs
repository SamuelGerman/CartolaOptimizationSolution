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
    IFullContextAthleteScoreModelTrainingService fullContextAthleteScoreModelTrainingService,
    IFullContextAthleteScoreModelHyperparameterTuningService fullContextAthleteScoreModelHyperparameterTuningService) : ControllerBase
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
    /// Permutation feature importance is always computed after the initial training pass and stored in the model metadata.
    /// When <paramref name="topNFeatures"/> is provided the model is then retrained using only the top-N numeric features,
    /// which can reduce noise and improve generalisation when many features are redundant.
    /// </summary>
    /// <param name="fromRound">Optional initial round of the training window.</param>
    /// <param name="toRound">Optional final round of the training window.</param>
    /// <param name="validationRounds">Number of last rounds reserved for temporal validation. Defaults to one round.</param>
    /// <param name="topNFeatures">
    /// When specified, retrain the model using only the top-N most important numeric features (as ranked by
    /// permutation feature importance on the holdout set). Categorical features (club, opponent, position,
    /// status) are always included. Omit this parameter to train on all available features.
    /// </param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <returns>The model metadata and validation metrics of the newly trained full-context artifact.</returns>
    [HttpPost("models/athlete-score-v1-full-context/train")]
    [ProducesResponseType(typeof(AthleteScoreModelTrainingResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> TrainFullContextAthleteScoreModel(
        [FromQuery] int? fromRound,
        [FromQuery] int? toRound,
        [FromQuery] int validationRounds = 1,
        [FromQuery] int? topNFeatures = null,
        CancellationToken cancellationToken = default)
    {
        var result = await fullContextAthleteScoreModelTrainingService.TrainAsync(
            fromRound,
            toRound,
            validationRounds,
            topNFeatures,
            cancellationToken: cancellationToken);

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

    /// <summary>
    /// Runs a random-search hyperparameter optimisation for the full-context LightGBM model using
    /// temporal walk-forward cross-validation, then optionally retrains and persists the model with
    /// the best combination found.
    /// </summary>
    /// <param name="fromRound">Optional earliest round to include in the search dataset.</param>
    /// <param name="toRound">Optional latest round to include in the search dataset.</param>
    /// <param name="numberOfTrials">
    /// Number of random hyperparameter combinations to evaluate. More trials improve the chance of
    /// finding the global optimum at the cost of longer runtime. Defaults to 30.
    /// </param>
    /// <param name="validationFolds">
    /// Number of walk-forward cross-validation folds. Each fold holds out one additional round from
    /// the tail of the dataset. Defaults to 3.
    /// </param>
    /// <param name="applyBest">
    /// When <c>true</c> (default), retrains and persists the full-context model with the best
    /// hyperparameter combination and the configured <c>DefaultTopNFeatures</c> feature selection.
    /// </param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <returns>Search results including all trial CV scores, the best hyperparameters, and the final model info.</returns>
    [HttpPost("models/athlete-score-v1-full-context/hyperparameter-search")]
    [ProducesResponseType(typeof(HyperparameterSearchResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SearchFullContextHyperparameters(
        [FromQuery] int? fromRound,
        [FromQuery] int? toRound,
        [FromQuery] int numberOfTrials = 30,
        [FromQuery] int validationFolds = 3,
        [FromQuery] bool applyBest = true,
        CancellationToken cancellationToken = default)
    {
        var result = await fullContextAthleteScoreModelHyperparameterTuningService.SearchAsync(
            fromRound,
            toRound,
            numberOfTrials,
            validationFolds,
            applyBest,
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
