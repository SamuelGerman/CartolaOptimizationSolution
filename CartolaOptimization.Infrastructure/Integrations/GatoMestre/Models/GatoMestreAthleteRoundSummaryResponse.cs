using System.Text.Json;
using System.Text.Json.Serialization;

namespace CartolaOptimization.Infrastructure.Integrations.GatoMestre.Models;

/// <summary>
/// Represents a summary athlete row returned by the round aggregation endpoint.
/// </summary>
public sealed class GatoMestreAthleteRoundSummaryResponse
{
    [JsonPropertyName("atleta_id")]
    public int AthleteId { get; init; }

    [JsonPropertyName("medias")]
    public JsonElement Averages { get; init; }

    [JsonPropertyName("partidas_jogadas")]
    public JsonElement Games { get; init; }
}
