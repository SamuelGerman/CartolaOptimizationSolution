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
    /// Loads team confrontation data for the next confrontation published by Gato Mestre.
    /// </summary>
    Task<GatoMestreClubConfrontationResponse> GetClubConfrontationAsync(int clubId, CancellationToken cancellationToken = default);
}
