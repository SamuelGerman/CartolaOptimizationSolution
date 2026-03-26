using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CartolaOptimization.Infrastructure.Diagnostics;
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

    public Task<CartolaAthleteScoresResponse> GetAthleteScoresAsync(int round, CancellationToken cancellationToken = default)
        => GetRequiredAsync<CartolaAthleteScoresResponse>($"atletas/pontuados/{round}", cancellationToken);

    public Task<CartolaMatchesResponse> GetMatchesAsync(int? round = null, CancellationToken cancellationToken = default)
    {
        var endpoint = round.HasValue ? $"partidas/{round.Value}" : "partidas?a=true";
        return GetRequiredAsync<CartolaMatchesResponse>(endpoint, cancellationToken);
    }

    public Task<JsonElement> SaveTeamAsync(
        CartolaSaveTeamRequest request,
        string cartolaAuthorization,
        string? cartolaGlbTag = null,
        CancellationToken cancellationToken = default)
        => PostAuthenticatedAsync("auth/time/salvar", request, cartolaAuthorization, cartolaGlbTag, cancellationToken);

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
                    sourceSystem: "Cartola",
                    errorLocation: "CartolaApiClient.GetRequiredAsync",
                    requestMethod: HttpMethod.Get.Method,
                    requestUrl: requestUrl,
                    requestHeadersJson: requestHeadersJson,
                    requestPayload: string.Empty,
                    responseStatusCode: responseStatusCode,
                    responseReasonPhrase: responseReasonPhrase,
                    responseHeadersJson: responseHeadersJson,
                    responsePayload: responsePayload,
                    message: $"The Cartola API returned status code {(int)response.StatusCode} for '{endpoint}'.");
            }

            var payload = JsonSerializer.Deserialize<T>(responsePayload, SerializerOptions);
            return payload ?? throw new ExternalApiException(
                sourceSystem: "Cartola",
                errorLocation: "CartolaApiClient.GetRequiredAsync",
                requestMethod: HttpMethod.Get.Method,
                requestUrl: requestUrl,
                requestHeadersJson: requestHeadersJson,
                requestPayload: string.Empty,
                responseStatusCode: responseStatusCode,
                responseReasonPhrase: responseReasonPhrase,
                responseHeadersJson: responseHeadersJson,
                responsePayload: responsePayload,
                message: $"The Cartola API returned an empty payload for '{endpoint}'.");
        }
        catch (JsonException exception)
        {
            throw new ExternalApiException(
                sourceSystem: "Cartola",
                errorLocation: "CartolaApiClient.GetRequiredAsync",
                requestMethod: HttpMethod.Get.Method,
                requestUrl: requestUrl,
                requestHeadersJson: requestHeadersJson,
                requestPayload: string.Empty,
                responseStatusCode: responseStatusCode,
                responseReasonPhrase: responseReasonPhrase,
                responseHeadersJson: responseHeadersJson,
                responsePayload: responsePayload,
                message: $"The Cartola API payload for '{endpoint}' could not be deserialized.",
                innerException: exception);
        }
        catch (HttpRequestException exception)
        {
            throw new ExternalApiException(
                sourceSystem: "Cartola",
                errorLocation: "CartolaApiClient.GetRequiredAsync",
                requestMethod: HttpMethod.Get.Method,
                requestUrl: requestUrl,
                requestHeadersJson: requestHeadersJson,
                requestPayload: string.Empty,
                responseStatusCode: null,
                responseReasonPhrase: string.Empty,
                responseHeadersJson: string.Empty,
                responsePayload: string.Empty,
                message: $"The Cartola API request to '{endpoint}' failed before a valid response was received.",
                innerException: exception);
        }
    }

    private async Task<JsonElement> PostAuthenticatedAsync(
        string endpoint,
        object payload,
        string cartolaAuthorization,
        string? cartolaGlbTag,
        CancellationToken cancellationToken)
    {
        var requestUrl = ResolveRequestUrl(endpoint);
        var requestPayload = JsonSerializer.Serialize(payload, SerializerOptions);
        var responsePayload = string.Empty;
        var responseHeadersJson = string.Empty;
        int? responseStatusCode = null;
        string responseReasonPhrase = string.Empty;

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(requestPayload, Encoding.UTF8, "application/json"),
        };

        request.Headers.Authorization = ParseAuthorizationHeader(cartolaAuthorization);
        request.Headers.Accept.ParseAdd("*/*");
        request.Headers.TryAddWithoutValidation("Origin", "https://cartola.globo.com");
        request.Headers.Referrer = new Uri("https://cartola.globo.com/");
        request.Headers.TryAddWithoutValidation("x-glb-app", "cartola_web");
        request.Headers.TryAddWithoutValidation("x-glb-auth", "oidc");

        if (!string.IsNullOrWhiteSpace(cartolaGlbTag))
        {
            request.Headers.TryAddWithoutValidation("x-glb-tag", cartolaGlbTag);
        }

        var requestHeadersJson = SerializeHeaders(request.Headers, request.Content.Headers);

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            responseStatusCode = (int)response.StatusCode;
            responseReasonPhrase = response.ReasonPhrase ?? string.Empty;
            responsePayload = await response.Content.ReadAsStringAsync(cancellationToken);
            responseHeadersJson = SerializeHeaders(response.Headers, response.Content.Headers);

            if (!response.IsSuccessStatusCode)
            {
                throw new ExternalApiException(
                    sourceSystem: "Cartola",
                    errorLocation: "CartolaApiClient.PostAuthenticatedAsync",
                    requestMethod: HttpMethod.Post.Method,
                    requestUrl: requestUrl,
                    requestHeadersJson: requestHeadersJson,
                    requestPayload: requestPayload,
                    responseStatusCode: responseStatusCode,
                    responseReasonPhrase: responseReasonPhrase,
                    responseHeadersJson: responseHeadersJson,
                    responsePayload: responsePayload,
                    message: $"The Cartola API returned status code {(int)response.StatusCode} for '{endpoint}'.");
            }

            if (string.IsNullOrWhiteSpace(responsePayload))
            {
                return default;
            }

            using var document = JsonDocument.Parse(responsePayload);
            return document.RootElement.Clone();
        }
        catch (JsonException exception)
        {
            throw new ExternalApiException(
                sourceSystem: "Cartola",
                errorLocation: "CartolaApiClient.PostAuthenticatedAsync",
                requestMethod: HttpMethod.Post.Method,
                requestUrl: requestUrl,
                requestHeadersJson: requestHeadersJson,
                requestPayload: requestPayload,
                responseStatusCode: responseStatusCode,
                responseReasonPhrase: responseReasonPhrase,
                responseHeadersJson: responseHeadersJson,
                responsePayload: responsePayload,
                message: $"The Cartola API payload for '{endpoint}' could not be deserialized.",
                innerException: exception);
        }
        catch (HttpRequestException exception)
        {
            throw new ExternalApiException(
                sourceSystem: "Cartola",
                errorLocation: "CartolaApiClient.PostAuthenticatedAsync",
                requestMethod: HttpMethod.Post.Method,
                requestUrl: requestUrl,
                requestHeadersJson: requestHeadersJson,
                requestPayload: requestPayload,
                responseStatusCode: null,
                responseReasonPhrase: string.Empty,
                responseHeadersJson: string.Empty,
                responsePayload: string.Empty,
                message: $"The Cartola API request to '{endpoint}' failed before a valid response was received.",
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
                dictionary[header.Key] = string.Equals(header.Key, "Authorization", StringComparison.OrdinalIgnoreCase)
                    ? "Bearer ***REDACTED***"
                    : string.Join(", ", header.Value);
            }
        }

        return JsonSerializer.Serialize(dictionary);
    }

    private static AuthenticationHeaderValue ParseAuthorizationHeader(string cartolaAuthorization)
    {
        if (string.IsNullOrWhiteSpace(cartolaAuthorization))
        {
            throw new ArgumentException("A Cartola authorization header must be provided to save the lineup.", nameof(cartolaAuthorization));
        }

        var trimmed = cartolaAuthorization.Trim();
        if (trimmed.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticationHeaderValue.Parse(trimmed);
        }

        return new AuthenticationHeaderValue("Bearer", trimmed);
    }
}
