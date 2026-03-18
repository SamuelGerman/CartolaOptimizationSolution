using System.Text.Json.Serialization;

namespace CartolaOptimization.Infrastructure.Integrations.GatoMestre.Models;

/// <summary>
/// Represents a team row returned by Gato Mestre.
/// </summary>
public sealed class GatoMestreTeamResponse
{
    [JsonPropertyName("equipe_id")]
    public int TeamId { get; init; }

    [JsonPropertyName("nome")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("nome_popular")]
    public string PopularName { get; init; } = string.Empty;

    [JsonPropertyName("apelido")]
    public string Nickname { get; init; } = string.Empty;

    [JsonPropertyName("sigla")]
    public string Acronym { get; init; } = string.Empty;

    [JsonPropertyName("slug")]
    public string Slug { get; init; } = string.Empty;

    [JsonPropertyName("escudos")]
    public GatoMestreTeamShieldResponse Shields { get; init; } = new();
}
