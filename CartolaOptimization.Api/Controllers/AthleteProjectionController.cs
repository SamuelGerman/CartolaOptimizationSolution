using CartolaOptimization.Application.Interfaces;
using CartolaOptimization.Application.Models;
using Microsoft.AspNetCore.Mvc;

namespace CartolaOptimization.Api.Controllers;

/// <summary>
/// Exposes the principal full-context prediction endpoints used by the application.
/// </summary>
[ApiController]
[Route("api/projections")]
[Produces("application/json")]
public sealed class AthleteProjectionController(
    IFullContextAthleteScorePredictionService fullContextAthleteScorePredictionService,
    IPredictedLineupOptimizationService predictedLineupOptimizationService) : ControllerBase
{
    /// <summary>
    /// Returns machine-learning score predictions for the athletes that can be evaluated with the principal full-context model for the informed round.
    /// </summary>
    /// <param name="round">Target round for the prediction.</param>
    /// <param name="statusCartolaId">Optional Cartola status filter that uses the athlete current status for the informed round.</param>
    /// <param name="clubCartolaId">Optional Cartola club filter that uses the athlete current club for the informed round.</param>
    /// <param name="positionCartolaId">Optional Cartola position filter that uses the athlete current position for the informed round.</param>
    /// <param name="take">Maximum number of predicted athletes returned. Limited to 5000 rows.</param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <returns>A ranked list of machine-learning predicted scores for the informed round using the principal full-context model.</returns>
    [HttpGet("round/{round:int}/athletes/ml")]
    [ProducesResponseType(typeof(AthleteScorePredictionResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMachineLearningPredictions(
        int round,
        [FromQuery] int? statusCartolaId,
        [FromQuery] int? clubCartolaId,
        [FromQuery] int? positionCartolaId,
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default)
    {
        var result = await fullContextAthleteScorePredictionService.GetPredictedScoresAsync(
            round,
            statusCartolaId,
            clubCartolaId,
            positionCartolaId,
            take,
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Returns the highest-total-score lineup composed only of provável athletes for the informed round, respecting the requested budget and formation.
    /// </summary>
    /// <param name="round">Target round for the lineup optimization.</param>
    /// <param name="request">Budget and formation constraints used by the optimizer.</param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <returns>The optimal predicted lineup under the informed constraints.</returns>
    [HttpPost("round/{round:int}/optimal-lineup/ml")]
    [ProducesResponseType(typeof(PredictedLineupResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetOptimalMachineLearningLineup(
        int round,
        [FromBody] PredictedLineupRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await predictedLineupOptimizationService.GetOptimalFullContextLineupAsync(
                round,
                request,
                cancellationToken);

            return Ok(result);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid lineup request",
                Detail = exception.Message,
                Status = StatusCodes.Status400BadRequest,
                Instance = HttpContext.Request.Path,
            });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Unable to optimize lineup",
                Detail = exception.Message,
                Status = StatusCodes.Status400BadRequest,
                Instance = HttpContext.Request.Path,
            });
        }
    }
}
