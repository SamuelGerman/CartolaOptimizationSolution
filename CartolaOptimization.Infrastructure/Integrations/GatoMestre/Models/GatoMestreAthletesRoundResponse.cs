using System.Text.Json.Serialization;

namespace CartolaOptimization.Infrastructure.Integrations.GatoMestre.Models;

/// <summary>
/// Represents the athlete aggregation payload returned for a specific round.
/// </summary>
public sealed class GatoMestreAthletesRoundResponse
{
    [JsonPropertyName("lastUpdate")]
    public string LastUpdate { get; init; } = string.Empty;

    [JsonPropertyName("result")]
    public List<GatoMestreAthleteRoundSummaryResponse> Result { get; init; } = [];
}
