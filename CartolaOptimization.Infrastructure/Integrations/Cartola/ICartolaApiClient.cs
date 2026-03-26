using System.Text.Json;
using CartolaOptimization.Infrastructure.Integrations.Cartola.Models;

namespace CartolaOptimization.Infrastructure.Integrations.Cartola;

public interface ICartolaApiClient
{
    Task<CartolaMarketStatusResponse> GetMarketStatusAsync(CancellationToken cancellationToken = default);

    Task<Dictionary<int, CartolaClubResponse>> GetClubsAsync(CancellationToken cancellationToken = default);

    Task<CartolaAthletesMarketResponse> GetAthletesMarketAsync(CancellationToken cancellationToken = default);

    Task<CartolaAthleteScoresResponse> GetAthleteScoresAsync(int round, CancellationToken cancellationToken = default);

    Task<CartolaMatchesResponse> GetMatchesAsync(int? round = null, CancellationToken cancellationToken = default);

    Task<JsonElement> SaveTeamAsync(
        CartolaSaveTeamRequest request,
        string cartolaAuthorization,
        string? cartolaGlbTag = null,
        CancellationToken cancellationToken = default);
}
