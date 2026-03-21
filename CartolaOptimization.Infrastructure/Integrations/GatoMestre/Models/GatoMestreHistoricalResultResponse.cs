using System.Text.Json.Serialization;

namespace CartolaOptimization.Infrastructure.Integrations.GatoMestre.Models;

public sealed class GatoMestreHistoricalResultResponse
{
    [JsonPropertyName("pct")]
    public GatoMestreHistoricalRateBucketResponse Rate { get; init; } = new();
}
