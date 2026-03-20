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
public sealed class AthleteProjectionController(
    IAthleteProjectionService athleteProjectionService,
    IAthleteScorePredictionService athleteScorePredictionService,
    IPredictedLineupOptimizationService predictedLineupOptimizationService) : ControllerBase
{
    /// <summary>
    /// Returns an explainable expected-score projection for the athletes that can be evaluated for the informed round.
    /// </summary>
    /// <param name="round">Target round for the projection.</param>
    /// <param name="positionCartolaId">Optional Cartola position filter.</param>
    /// <param name="clubCartolaId">Optional Cartola club filter.</param>
    /// <param name="statusCartolaId">Optional Cartola status filter that uses the athlete current status for the informed round.</param>
    /// <param name="take">Maximum number of projected athletes returned. Limited to 500 rows.</param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <returns>A ranked list of athlete expected scores together with the main factors behind each projection.</returns>
    [HttpGet("round/{round:int}/athletes")]
    [ProducesResponseType(typeof(AthleteRoundProjectionResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRoundProjection(
        int round,
        [FromQuery] int? positionCartolaId,
        [FromQuery] int? clubCartolaId,
        [FromQuery] int? statusCartolaId,
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default)
    {
        var result = await athleteProjectionService.GetRoundProjectionAsync(
            round,
            positionCartolaId,
            clubCartolaId,
            statusCartolaId,
            take,
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Returns machine-learning score predictions for the athletes that already have a current baseline synchronized for the informed round.
    /// </summary>
    /// <param name="round">Target round for the prediction.</param>
    /// <param name="statusCartolaId">Optional Cartola status filter that uses the athlete current status for the informed round.</param>
    /// <param name="clubCartolaId">Optional Cartola club filter that uses the athlete current club for the informed round.</param>
    /// <param name="positionCartolaId">Optional Cartola position filter that uses the athlete current position for the informed round.</param>
    /// <param name="take">Maximum number of predicted athletes returned. Limited to 5000 rows.</param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <returns>A ranked list of machine-learning predicted scores for the informed round.</returns>
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
        var result = await athleteScorePredictionService.GetPredictedScoresAsync(
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
            var result = await predictedLineupOptimizationService.GetOptimalLineupAsync(
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
