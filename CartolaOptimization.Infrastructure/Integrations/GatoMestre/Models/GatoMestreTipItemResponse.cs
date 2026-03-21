using System.Text.Json;
using System.Text.Json.Serialization;

namespace CartolaOptimization.Infrastructure.Integrations.GatoMestre.Models;

public sealed class GatoMestreTipItemResponse
{
    [JsonPropertyName("hot")]
    public bool Hot { get; init; }

    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("info")]
    public JsonElement Info { get; init; }
}
