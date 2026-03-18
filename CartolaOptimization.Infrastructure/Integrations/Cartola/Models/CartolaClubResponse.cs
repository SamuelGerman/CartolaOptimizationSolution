using System.Text.Json.Serialization;

namespace CartolaOptimization.Infrastructure.Integrations.Cartola.Models;

public sealed class CartolaClubResponse
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("abreviacao")]
    public string Abbreviation { get; init; } = string.Empty;

    [JsonPropertyName("slug")]
    public string Slug { get; init; } = string.Empty;

    [JsonPropertyName("apelido")]
    public string Nickname { get; init; } = string.Empty;

    [JsonPropertyName("nome_fantasia")]
    public string FantasyName { get; init; } = string.Empty;

    [JsonPropertyName("nome")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("escudos")]
    public CartolaShieldResponse? Shields { get; init; }
}
