using System.Text.Json.Serialization;

namespace CartolaOptimization.Infrastructure.Integrations.GatoMestre.Models;

/// <summary>
/// Representa o bloco de identidade do atleta retornado pelo Gato Mestre.
/// </summary>
public sealed class GatoMestreAthleteProfileResponse
{
    [JsonPropertyName("atleta_id")]
    public int AthleteId { get; init; }

    [JsonPropertyName("equipe_id")]
    public int TeamId { get; init; }

    [JsonPropertyName("nome")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("apelido")]
    public string Nickname { get; init; } = string.Empty;

    [JsonPropertyName("nome_popular")]
    public string PopularName { get; init; } = string.Empty;

    [JsonPropertyName("slug")]
    public string Slug { get; init; } = string.Empty;

    [JsonPropertyName("posicao")]
    public GatoMestrePositionDescriptorResponse Position { get; init; } = new();
}
