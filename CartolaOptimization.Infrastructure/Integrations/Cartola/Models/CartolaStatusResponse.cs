using System.Text.Json.Serialization;

namespace CartolaOptimization.Infrastructure.Integrations.Cartola.Models;

public sealed class CartolaStatusResponse
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("nome")]
    public string Name { get; init; } = string.Empty;
}
