using System.Text.Json.Serialization;

namespace CartolaOptimization.Infrastructure.Integrations.GatoMestre.Models;

public sealed class GatoMestreForecastMatchResponse
{
    [JsonPropertyName("homeTeam")]
    public GatoMestreSimpleTeamReferenceResponse HomeTeam { get; init; } = new();

    [JsonPropertyName("awayTeam")]
    public GatoMestreSimpleTeamReferenceResponse AwayTeam { get; init; } = new();

    [JsonPropertyName("resultado")]
    public int ResultCode { get; init; }

    [JsonPropertyName("risco")]
    public string RiskLevel { get; init; } = string.Empty;
}
