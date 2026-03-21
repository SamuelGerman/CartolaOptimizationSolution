using System.Text.Json.Serialization;

namespace CartolaOptimization.Infrastructure.Integrations.GatoMestre.Models;

public sealed class GatoMestreFavoriteMatchResponse
{
    [JsonPropertyName("homeTeam")]
    public GatoMestreSimpleTeamReferenceResponse HomeTeam { get; init; } = new();

    [JsonPropertyName("awayTeam")]
    public GatoMestreSimpleTeamReferenceResponse AwayTeam { get; init; } = new();

    [JsonPropertyName("date")]
    public string Date { get; init; } = string.Empty;

    [JsonPropertyName("hour")]
    public string Hour { get; init; } = string.Empty;

    [JsonPropertyName("odds")]
    public GatoMestreFavoriteMatchOddsResponse Odds { get; init; } = new();
}
