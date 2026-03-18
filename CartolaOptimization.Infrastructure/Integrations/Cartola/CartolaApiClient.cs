using System.Net.Http.Json;
using System.Text.Json;
using CartolaOptimization.Infrastructure.Integrations.Cartola.Models;

namespace CartolaOptimization.Infrastructure.Integrations.Cartola;

public sealed class CartolaApiClient(HttpClient httpClient) : ICartolaApiClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    public Task<CartolaMarketStatusResponse> GetMarketStatusAsync(CancellationToken cancellationToken = default)
        => GetRequiredAsync<CartolaMarketStatusResponse>("mercado/status", cancellationToken);

    public Task<Dictionary<int, CartolaClubResponse>> GetClubsAsync(CancellationToken cancellationToken = default)
        => GetRequiredAsync<Dictionary<int, CartolaClubResponse>>("clubes", cancellationToken);

    public Task<CartolaAthletesMarketResponse> GetAthletesMarketAsync(CancellationToken cancellationToken = default)
        => GetRequiredAsync<CartolaAthletesMarketResponse>("atletas/mercado?a=true", cancellationToken);

    public Task<CartolaMatchesResponse> GetMatchesAsync(int? round = null, CancellationToken cancellationToken = default)
    {
        var endpoint = round.HasValue ? $"partidas/{round.Value}" : "partidas?a=true";
        return GetRequiredAsync<CartolaMatchesResponse>(endpoint, cancellationToken);
    }

    private async Task<T> GetRequiredAsync<T>(string endpoint, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(endpoint, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<T>(SerializerOptions, cancellationToken);
        return payload ?? throw new InvalidOperationException($"The Cartola API returned an empty payload for '{endpoint}'.");
    }
}
