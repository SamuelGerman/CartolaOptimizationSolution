using System.Text.Json.Serialization;

namespace CartolaOptimization.Infrastructure.Integrations.GatoMestre.Models;

public sealed class GatoMestreNamedMetricResponse
{
    [JsonPropertyName("sigla")]
    public string Key { get; init; } = string.Empty;

    [JsonPropertyName("nome")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("valor")]
    public decimal Value { get; init; }
}
