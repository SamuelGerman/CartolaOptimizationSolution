using System.Text.Json.Serialization;

namespace CartolaOptimization.Infrastructure.Integrations.GatoMestre.Models;

/// <summary>
/// Represents the price block returned by Gato Mestre for a specific athlete round.
/// </summary>
public sealed class GatoMestrePriceSnapshotResponse
{
    [JsonPropertyName("num")]
    public decimal? Num { get; init; }

    [JsonPropertyName("variacao")]
    public decimal? Variation { get; init; }
}
