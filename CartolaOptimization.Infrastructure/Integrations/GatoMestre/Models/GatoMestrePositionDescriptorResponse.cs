using System.Text.Json.Serialization;

namespace CartolaOptimization.Infrastructure.Integrations.GatoMestre.Models;

/// <summary>
/// Represents the athlete position descriptor returned by Gato Mestre.
/// </summary>
public sealed class GatoMestrePositionDescriptorResponse
{
    [JsonPropertyName("macro")]
    public string Macro { get; init; } = string.Empty;

    [JsonPropertyName("sigla")]
    public string Sigla { get; init; } = string.Empty;

    [JsonPropertyName("descricao")]
    public string Description { get; init; } = string.Empty;
}
