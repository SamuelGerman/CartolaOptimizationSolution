using System.Text.Json.Serialization;

namespace CartolaOptimization.Infrastructure.Integrations.GatoMestre.Models;

public sealed class GatoMestreHistoricalRateBucketResponse
{
    [JsonPropertyName("geral")]
    public decimal Overall { get; init; }

    [JsonPropertyName("mandante")]
    public decimal Home { get; init; }

    [JsonPropertyName("visitante")]
    public decimal Away { get; init; }
}
