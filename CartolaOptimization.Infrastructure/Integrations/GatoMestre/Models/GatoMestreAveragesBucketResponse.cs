using System.Text.Json.Serialization;

namespace CartolaOptimization.Infrastructure.Integrations.GatoMestre.Models;

/// <summary>
/// Representa um agrupamento de valores médios retornado pelo Gato Mestre.
/// </summary>
public sealed class GatoMestreAveragesBucketResponse
{
    [JsonPropertyName("total")]
    public GatoMestreNumericSnapshotResponse? Total { get; init; }

    [JsonPropertyName("movel")]
    public GatoMestreNumericSnapshotResponse? Moving { get; init; }
}
