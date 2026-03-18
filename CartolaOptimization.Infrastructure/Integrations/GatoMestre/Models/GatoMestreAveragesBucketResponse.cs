using System.Text.Json.Serialization;

namespace CartolaOptimization.Infrastructure.Integrations.GatoMestre.Models;

/// <summary>
/// Represents a bucket of average values returned by Gato Mestre.
/// </summary>
public sealed class GatoMestreAveragesBucketResponse
{
    [JsonPropertyName("total")]
    public GatoMestreNumericSnapshotResponse? Total { get; init; }

    [JsonPropertyName("movel")]
    public GatoMestreNumericSnapshotResponse? Moving { get; init; }
}
