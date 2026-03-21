using CartolaOptimization.Infrastructure.Integrations.GatoMestre.Models;

namespace CartolaOptimization.Infrastructure.Integrations.GatoMestre;

/// <summary>
/// Defines the HTTP contract used to consume the public Gato Mestre endpoints required by the solution.
/// </summary>
public interface IGatoMestreApiClient
{
    /// <summary>
    /// Loads the team catalog exposed by Gato Mestre.
    /// </summary>
    Task<Dictionary<int, GatoMestreTeamResponse>> GetTeamsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads athlete aggregate data for a specific season and round.
    /// </summary>
    Task<GatoMestreAthletesRoundResponse> GetAthletesRoundAsync(int season, int round, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the full athlete detail payload used as a fallback when the round summary is not sufficient to build a round row.
    /// </summary>
    Task<GatoMestreAthleteDetailResponse> GetAthleteDetailAsync(int athleteId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the legacy athlete-round content payload with richer athlete-level indicators.
    /// </summary>
    Task<GatoMestreAthleteRoundContentResponse> GetAthleteRoundContentAsync(int athleteId, int round, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads team confrontation data for the next confrontation published by Gato Mestre.
    /// </summary>
    Task<GatoMestreClubConfrontationResponse> GetClubConfrontationAsync(int clubId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the public round tips payload.
    /// </summary>
    Task<GatoMestreRoundCollectionResponse<GatoMestreTipItemResponse>> GetRoundTipsAsync(int season, int round, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads favorites/probability data for the matches of a round.
    /// </summary>
    Task<GatoMestreRoundCollectionResponse<GatoMestreFavoriteMatchResponse>> GetFavoriteMatchesAsync(int season, int round, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the editorial match forecasts for a round.
    /// </summary>
    Task<GatoMestreRoundCollectionResponse<GatoMestreForecastMatchResponse>> GetMatchForecastsAsync(int season, int round, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads club average rankings for a specific round.
    /// </summary>
    Task<GatoMestreRoundCollectionResponse<GatoMestreClubAverageRankingResponse>> GetClubAverageRankingsAsync(int season, int round, CancellationToken cancellationToken = default);
}
