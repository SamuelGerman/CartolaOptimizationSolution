using System.Text.Json.Serialization;

namespace CartolaOptimization.Infrastructure.Integrations.GatoMestre.Models;

/// <summary>
/// Represents a numeric value optionally accompanied by a variation.
/// </summary>
public sealed class GatoMestreNumericSnapshotResponse
{
    [JsonPropertyName("num")]
    public decimal? Num { get; init; }

    [JsonPropertyName("variacao")]
    public decimal? Variation { get; init; }
}
