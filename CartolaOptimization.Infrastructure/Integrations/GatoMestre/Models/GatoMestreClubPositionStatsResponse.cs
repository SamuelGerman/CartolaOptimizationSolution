using System.Text.Json.Serialization;

namespace CartolaOptimization.Infrastructure.Integrations.GatoMestre.Models;

/// <summary>
/// Represents the position-level team statistics returned by Gato Mestre.
/// </summary>
public sealed class GatoMestreClubPositionStatsResponse
{
    [JsonPropertyName("posicao_id")]
    public int PositionId { get; init; }

    [JsonPropertyName("media_pontos_cedidos")]
    public decimal PointsConcededAverage { get; init; }

    [JsonPropertyName("media_pontos_conquitados")]
    public decimal PointsConqueredAverage { get; init; }
}
