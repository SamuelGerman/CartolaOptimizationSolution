using CartolaOptimization.Infrastructure.Integrations.GatoMestre.Models;

namespace CartolaOptimization.Infrastructure.Integrations.GatoMestre;

/// <summary>
/// Define o contrato HTTP usado para consumir os endpoints públicos do Gato Mestre exigidos pela solução.
/// </summary>
public interface IGatoMestreApiClient
{
    /// <summary>
    /// Carrega o catálogo de times exposto pelo Gato Mestre.
    /// </summary>
    Task<Dictionary<int, GatoMestreTeamResponse>> GetTeamsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Carrega os dados agregados de atletas para uma temporada e rodada específicas.
    /// </summary>
    Task<GatoMestreAthletesRoundResponse> GetAthletesRoundAsync(int season, int round, CancellationToken cancellationToken = default);

    /// <summary>
    /// Carrega o payload completo de detalhe do atleta, usado como fallback quando o resumo da rodada não é suficiente para montar uma linha de rodada.
    /// </summary>
    Task<GatoMestreAthleteDetailResponse> GetAthleteDetailAsync(int athleteId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Carrega o payload legado de conteúdo de atleta-rodada com indicadores mais ricos em nível de atleta.
    /// </summary>
    Task<GatoMestreAthleteRoundContentResponse> GetAthleteRoundContentAsync(int athleteId, int round, CancellationToken cancellationToken = default);

    /// <summary>
    /// Carrega os dados de confronto do time para o próximo confronto publicado pelo Gato Mestre.
    /// </summary>
    Task<GatoMestreClubConfrontationResponse> GetClubConfrontationAsync(int clubId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Carrega o payload público de dicas da rodada.
    /// </summary>
    Task<GatoMestreRoundCollectionResponse<GatoMestreTipItemResponse>> GetRoundTipsAsync(int season, int round, CancellationToken cancellationToken = default);

    /// <summary>
    /// Carrega os dados de favoritismo/probabilidade das partidas de uma rodada.
    /// </summary>
    Task<GatoMestreRoundCollectionResponse<GatoMestreFavoriteMatchResponse>> GetFavoriteMatchesAsync(int season, int round, CancellationToken cancellationToken = default);

    /// <summary>
    /// Carrega as previsões editoriais das partidas de uma rodada.
    /// </summary>
    Task<GatoMestreRoundCollectionResponse<GatoMestreForecastMatchResponse>> GetMatchForecastsAsync(int season, int round, CancellationToken cancellationToken = default);

    /// <summary>
    /// Carrega os rankings de médias de clubes para uma rodada específica.
    /// </summary>
    Task<GatoMestreRoundCollectionResponse<GatoMestreClubAverageRankingResponse>> GetClubAverageRankingsAsync(int season, int round, CancellationToken cancellationToken = default);
}
