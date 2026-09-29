using CartolaOptimization.Application.Interfaces;
using CartolaOptimization.Application.Models;
using Microsoft.AspNetCore.Mvc;

namespace CartolaOptimization.Api.Controllers;

/// <summary>
/// Expõe endpoints administrativos usados para sincronizar dados originados da API do Cartola.
/// </summary>
[ApiController]
[Route("api/cartola/sync")]
[Produces("application/json")]
public sealed class CartolaSyncController(ICartolaSyncService cartolaSyncService) : ControllerBase
{
    /// <summary>
    /// Sincroniza o catálogo atual do Cartola, os dados de mercado dos atletas e as partidas da rodada atual.
    /// </summary>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    /// <returns>Um resumo descrevendo as entidades e linhas sincronizadas.</returns>
    [HttpPost("current")]
    [ProducesResponseType(typeof(CartolaSyncResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SyncCurrent(CancellationToken cancellationToken)
    {
        var result = await cartolaSyncService.SyncAsync(cancellationToken: cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Finaliza uma única rodada histórica fechada usando apenas dados do Cartola (sem Gato Mestre, sem treino):
    /// busca o payload de pontuação final, o aplica às linhas-base de atleta-rodada persistidas e marca a
    /// rodada como historicamente fechada para que se torne elegível ao treino do modelo. Use isto para, retroativamente,
    /// fechar rodadas que passaram sem uma execução de fim de rodada. A rodada deve ser anterior à rodada atual.
    /// </summary>
    /// <param name="round">Rodada histórica fechada a finalizar.</param>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    /// <returns>Um resumo descrevendo a rodada finalizada.</returns>
    [HttpPost("closed-round/{round:int}")]
    [ProducesResponseType(typeof(CartolaSyncResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SyncClosedRound(int round, CancellationToken cancellationToken)
    {
        if (round <= 0)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid round", Detail = "Round must be greater than zero." });
        }

        var result = await cartolaSyncService.SyncClosedRoundAsync(round, cancellationToken);
        return Ok(result);
    }
}
