using System.Net.Http.Json;
using System.Text.Json;
using CartolaOptimization.Infrastructure.Integrations.GatoMestre.Models;

namespace CartolaOptimization.Infrastructure.Integrations.GatoMestre;

/// <summary>
/// Default HTTP client used to consume the public Gato Mestre endpoints.
/// </summary>
public sealed class GatoMestreApiClient(HttpClient httpClient) : IGatoMestreApiClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    public Task<Dictionary<int, GatoMestreTeamResponse>> GetTeamsAsync(CancellationToken cancellationToken = default)
        => GetRequiredAsync<Dictionary<int, GatoMestreTeamResponse>>("api/v2/equipes/", cancellationToken);

    public Task<GatoMestreAthletesRoundResponse> GetAthletesRoundAsync(int season, int round, CancellationToken cancellationToken = default)
        => GetRequiredAsync<GatoMestreAthletesRoundResponse>($"api/v2/atletas/{season}/campeonato-brasileiro/home/atletas/rodadas/{round}?a=true", cancellationToken);

    public Task<GatoMestreAthleteDetailResponse> GetAthleteDetailAsync(int athleteId, CancellationToken cancellationToken = default)
        => GetRequiredAsync<GatoMestreAthleteDetailResponse>($"api/v2/atletas/{athleteId}?a=true", cancellationToken);

    public Task<GatoMestreClubConfrontationResponse> GetClubConfrontationAsync(int clubId, CancellationToken cancellationToken = default)
        => GetRequiredAsync<GatoMestreClubConfrontationResponse>($"api/v1/conteudos/confrontos/equipe/{clubId}?a=true", cancellationToken);

    private async Task<T> GetRequiredAsync<T>(string endpoint, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(endpoint, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<T>(SerializerOptions, cancellationToken);
        return payload ?? throw new InvalidOperationException($"The Gato Mestre API returned an empty payload for '{endpoint}'.");
    }
}
