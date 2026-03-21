using System.Net.Http.Headers;
using System.Text.Json;
using CartolaOptimization.Infrastructure.Diagnostics;
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

    public Task<GatoMestreAthleteRoundContentResponse> GetAthleteRoundContentAsync(int athleteId, int round, CancellationToken cancellationToken = default)
        => GetRequiredAsync<GatoMestreAthleteRoundContentResponse>($"api/v1/conteudos/atletas/{athleteId}/rodadas/{round}?a=true", cancellationToken);

    public Task<GatoMestreClubConfrontationResponse> GetClubConfrontationAsync(int clubId, CancellationToken cancellationToken = default)
        => GetRequiredAsync<GatoMestreClubConfrontationResponse>($"api/v1/conteudos/confrontos/equipe/{clubId}?a=true", cancellationToken);

    public Task<GatoMestreRoundCollectionResponse<GatoMestreTipItemResponse>> GetRoundTipsAsync(int season, int round, CancellationToken cancellationToken = default)
        => GetRequiredAsync<GatoMestreRoundCollectionResponse<GatoMestreTipItemResponse>>($"api/v2/dicas/{season}/campeonato-brasileiro/rodadas/{round}?a=true", cancellationToken);

    public Task<GatoMestreRoundCollectionResponse<GatoMestreFavoriteMatchResponse>> GetFavoriteMatchesAsync(int season, int round, CancellationToken cancellationToken = default)
        => GetRequiredAsync<GatoMestreRoundCollectionResponse<GatoMestreFavoriteMatchResponse>>($"api/v2/equipes/{season}/campeonato-brasileiro/favoritos/rodadas/{round}?a=true", cancellationToken);

    public Task<GatoMestreRoundCollectionResponse<GatoMestreForecastMatchResponse>> GetMatchForecastsAsync(int season, int round, CancellationToken cancellationToken = default)
        => GetRequiredAsync<GatoMestreRoundCollectionResponse<GatoMestreForecastMatchResponse>>($"api/v2/equipes/{season}/campeonato-brasileiro/previsoes/rodadas/{round}?a=true", cancellationToken);

    public Task<GatoMestreRoundCollectionResponse<GatoMestreClubAverageRankingResponse>> GetClubAverageRankingsAsync(int season, int round, CancellationToken cancellationToken = default)
        => GetRequiredAsync<GatoMestreRoundCollectionResponse<GatoMestreClubAverageRankingResponse>>($"api/v2/rankings/{season}/campeonato-brasileiro/clubes/medias/rodadas/{round}?a=true", cancellationToken);

    private async Task<T> GetRequiredAsync<T>(string endpoint, CancellationToken cancellationToken)
    {
        var requestUrl = ResolveRequestUrl(endpoint);
        var requestHeadersJson = SerializeHeaders(httpClient.DefaultRequestHeaders);
        var responsePayload = string.Empty;
        var responseHeadersJson = string.Empty;
        int? responseStatusCode = null;
        string responseReasonPhrase = string.Empty;

        try
        {
            using var response = await httpClient.GetAsync(endpoint, cancellationToken);
            responseStatusCode = (int)response.StatusCode;
            responseReasonPhrase = response.ReasonPhrase ?? string.Empty;
            responsePayload = await response.Content.ReadAsStringAsync(cancellationToken);
            responseHeadersJson = SerializeHeaders(response.Headers, response.Content.Headers);

            if (!response.IsSuccessStatusCode)
            {
                throw new ExternalApiException(
                    sourceSystem: "GatoMestre",
                    errorLocation: "GatoMestreApiClient.GetRequiredAsync",
                    requestMethod: HttpMethod.Get.Method,
                    requestUrl: requestUrl,
                    requestHeadersJson: requestHeadersJson,
                    requestPayload: string.Empty,
                    responseStatusCode: responseStatusCode,
                    responseReasonPhrase: responseReasonPhrase,
                    responseHeadersJson: responseHeadersJson,
                    responsePayload: responsePayload,
                    message: $"The Gato Mestre API returned status code {(int)response.StatusCode} for '{endpoint}'.");
            }

            using var responseDocument = JsonDocument.Parse(responsePayload);
            if (responseDocument.RootElement.ValueKind == JsonValueKind.Object &&
                responseDocument.RootElement.TryGetProperty("error", out var errorProperty) &&
                errorProperty.ValueKind == JsonValueKind.True)
            {
                var apiMessage = responseDocument.RootElement.TryGetProperty("message", out var messageProperty) && messageProperty.ValueKind == JsonValueKind.String
                    ? messageProperty.GetString()
                    : "Unknown application error.";

                throw new ExternalApiException(
                    sourceSystem: "GatoMestre",
                    errorLocation: "GatoMestreApiClient.GetRequiredAsync",
                    requestMethod: HttpMethod.Get.Method,
                    requestUrl: requestUrl,
                    requestHeadersJson: requestHeadersJson,
                    requestPayload: string.Empty,
                    responseStatusCode: responseStatusCode,
                    responseReasonPhrase: responseReasonPhrase,
                    responseHeadersJson: responseHeadersJson,
                    responsePayload: responsePayload,
                    message: $"The Gato Mestre API returned an application error for '{endpoint}': {apiMessage}");
            }

            var payload = JsonSerializer.Deserialize<T>(responsePayload, SerializerOptions);
            return payload ?? throw new ExternalApiException(
                sourceSystem: "GatoMestre",
                errorLocation: "GatoMestreApiClient.GetRequiredAsync",
                requestMethod: HttpMethod.Get.Method,
                requestUrl: requestUrl,
                requestHeadersJson: requestHeadersJson,
                requestPayload: string.Empty,
                responseStatusCode: responseStatusCode,
                responseReasonPhrase: responseReasonPhrase,
                responseHeadersJson: responseHeadersJson,
                responsePayload: responsePayload,
                message: $"The Gato Mestre API returned an empty payload for '{endpoint}'.");
        }
        catch (JsonException exception)
        {
            throw new ExternalApiException(
                sourceSystem: "GatoMestre",
                errorLocation: "GatoMestreApiClient.GetRequiredAsync",
                requestMethod: HttpMethod.Get.Method,
                requestUrl: requestUrl,
                requestHeadersJson: requestHeadersJson,
                requestPayload: string.Empty,
                responseStatusCode: responseStatusCode,
                responseReasonPhrase: responseReasonPhrase,
                responseHeadersJson: responseHeadersJson,
                responsePayload: responsePayload,
                message: $"The Gato Mestre API payload for '{endpoint}' could not be deserialized.",
                innerException: exception);
        }
        catch (HttpRequestException exception)
        {
            throw new ExternalApiException(
                sourceSystem: "GatoMestre",
                errorLocation: "GatoMestreApiClient.GetRequiredAsync",
                requestMethod: HttpMethod.Get.Method,
                requestUrl: requestUrl,
                requestHeadersJson: requestHeadersJson,
                requestPayload: string.Empty,
                responseStatusCode: null,
                responseReasonPhrase: string.Empty,
                responseHeadersJson: string.Empty,
                responsePayload: string.Empty,
                message: $"The Gato Mestre API request to '{endpoint}' failed before a valid response was received.",
                innerException: exception);
        }
    }

    private string ResolveRequestUrl(string endpoint)
        => httpClient.BaseAddress is null ? endpoint : new Uri(httpClient.BaseAddress, endpoint).ToString();

    private static string SerializeHeaders(params HttpHeaders[] headers)
    {
        var dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var headerGroup in headers)
        {
            foreach (var header in headerGroup)
            {
                dictionary[header.Key] = string.Join(", ", header.Value);
            }
        }

        return JsonSerializer.Serialize(dictionary);
    }
}
