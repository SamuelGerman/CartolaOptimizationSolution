using CartolaOptimization.Application.Models;

namespace CartolaOptimization.Application.Interfaces;

/// <summary>
/// Define o contrato para sincronizar os conjuntos de dados originados da API pública do Cartola.
/// </summary>
public interface ICartolaSyncService
{
    /// <summary>
    /// Sincroniza o catálogo atual do Cartola e os dados de mercado dos atletas, e opcionalmente sobrescreve a rodada usada para carregar as partidas.
    /// </summary>
    /// <param name="matchesRound">
    /// Sobrescrita opcional de rodada usada pelo endpoint de partidas. Quando omitida, o serviço usa a rodada atual retornada pelo endpoint de status do mercado do Cartola.
    /// </param>
    /// <param name="cancellationToken">Token de cancelamento da operação.</param>
    /// <returns>Um resumo da execução da sincronização.</returns>
    Task<CartolaSyncResult> SyncAsync(int? matchesRound = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sincroniza o payload de pontuação final de uma rodada histórica fechada do Cartola e o aplica às atleta-rodadas locais já persistidas.
    /// </summary>
    /// <param name="round">Rodada histórica fechada que deve ser finalizada localmente.</param>
    /// <param name="cancellationToken">Token de cancelamento da operação.</param>
    /// <returns>Um resumo da execução da sincronização.</returns>
    Task<CartolaSyncResult> SyncClosedRoundAsync(int round, CancellationToken cancellationToken = default);
}
