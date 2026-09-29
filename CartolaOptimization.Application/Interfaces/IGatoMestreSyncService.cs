using CartolaOptimization.Application.Models;

namespace CartolaOptimization.Application.Interfaces;

/// <summary>
/// Define o contrato para sincronizar os conjuntos de dados originados da API pública do Gato Mestre.
/// </summary>
public interface IGatoMestreSyncService
{
    /// <summary>
    /// Sincroniza os conjuntos de dados do Gato Mestre para uma temporada e rodada específicas, enriquecendo os dados de rodada em nível de time e de atleta.
    /// </summary>
    /// <param name="season">Temporada usada pelo endpoint de atletas por rodada do Gato Mestre.</param>
    /// <param name="round">Rodada que deve ser sincronizada.</param>
    /// <param name="cancellationToken">Token de cancelamento da operação.</param>
    /// <returns>Um resumo da execução da sincronização.</returns>
    Task<GatoMestreSyncResult> SyncRoundAsync(int season, int round, CancellationToken cancellationToken = default);
}
