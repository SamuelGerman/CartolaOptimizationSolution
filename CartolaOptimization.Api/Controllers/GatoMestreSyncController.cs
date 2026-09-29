using CartolaOptimization.Application.Interfaces;
using CartolaOptimization.Application.Models;
using Microsoft.AspNetCore.Mvc;

namespace CartolaOptimization.Api.Controllers;

/// <summary>
/// Expõe endpoints administrativos usados para sincronizar conjuntos de dados de enriquecimento originados da API do Gato Mestre.
/// </summary>
[ApiController]
[Route("api/gatomestre/sync")]
[Produces("application/json")]
public sealed class GatoMestreSyncController(IGatoMestreSyncService gatoMestreSyncService) : ControllerBase
{
    /// <summary>
    /// Sincroniza os dados do Gato Mestre para uma temporada e rodada específicas.
    /// </summary>
    /// <param name="season">Temporada usada pelo endpoint de atletas por rodada.</param>
    /// <param name="round">Rodada que deve ser sincronizada.</param>
    /// <param name="cancellationToken">Token de cancelamento da requisição.</param>
    /// <returns>Um resumo descrevendo as entidades e linhas sincronizadas.</returns>
    [HttpPost("season/{season:int}/round/{round:int}")]
    [ProducesResponseType(typeof(GatoMestreSyncResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SyncRound(int season, int round, CancellationToken cancellationToken)
    {
        var result = await gatoMestreSyncService.SyncRoundAsync(season, round, cancellationToken);
        return Ok(result);
    }
}
