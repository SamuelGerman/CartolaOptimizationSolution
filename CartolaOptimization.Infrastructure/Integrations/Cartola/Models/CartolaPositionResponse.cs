using System.Text.Json.Serialization;

namespace CartolaOptimization.Infrastructure.Integrations.Cartola.Models;

public sealed class CartolaPositionResponse
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("nome")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("abreviacao")]
    public string Abbreviation { get; init; } = string.Empty;
}
