using CartolaOptimization.Application.Interfaces;
using CartolaOptimization.Application.Models;
using Microsoft.AspNetCore.Mvc;

namespace CartolaOptimization.Api.Controllers;

/// <summary>
/// Expõe os principais endpoints de previsão full-context usados pela aplicação.
/// </summary>
[ApiController]
[Route("api/projections")]
[Produces("application/json")]
public sealed class AthleteProjectionController(
    IFullContextAthleteScorePredictionService fullContextAthleteScorePredictionService,
    IPredictedLineupOptimizationService predictedLineupOptimizationService) : ControllerBase
{
    /// <summary>
    /// Retorna previsões de pontuação por aprendizado de máquina para os atletas que podem ser avaliados com o modelo full-context principal para a rodada informada.
    /// </summary>
    /// <param name="round">Rodada-alvo para a previsão.</param>
    /// <param name="statusCartolaId">Filtro opcional de status do Cartola que usa o status atual do atleta para a rodada informada.</param>
    /// <param name="clubCartolaId">Filtro opcional de clube do Cartola que usa o clube atual do atleta para a rodada informada.</param>
    /// <param name="positionCartolaId">Filtro opcional de posição do Cartola que usa a posição atual do atleta para a rodada informada.</param>
    /// <param name="take">Número máximo de atletas previstos retornados. Limitado a 5000 linhas.</param>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    /// <returns>Uma lista ordenada de pontuações previstas por aprendizado de máquina para a rodada informada usando o modelo full-context principal.</returns>
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
    /// Retorna a escalação de maior pontuação total composta apenas por atletas prováveis para a rodada informada, respeitando o orçamento e a formação solicitados.
    /// </summary>
    /// <param name="round">Rodada-alvo para a otimização da escalação.</param>
    /// <param name="request">Restrições de orçamento e formação usadas pelo otimizador.</param>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    /// <returns>A escalação prevista ótima sob as restrições informadas.</returns>
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
