using System.Text.Json.Serialization;

namespace CartolaOptimization.Infrastructure.Integrations.GatoMestre.Models;

public sealed class GatoMestrePositionDistributionResponse
{
    [JsonPropertyName("posicao_id")]
    public int PositionId { get; init; }

    [JsonPropertyName("valor")]
    public decimal Value { get; init; }
}
