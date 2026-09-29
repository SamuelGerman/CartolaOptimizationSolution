using System.Text.Json.Serialization;

namespace CartolaOptimization.Infrastructure.Integrations.GatoMestre.Models;

/// <summary>
/// Representa o descritor de posição do atleta retornado pelo Gato Mestre.
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
