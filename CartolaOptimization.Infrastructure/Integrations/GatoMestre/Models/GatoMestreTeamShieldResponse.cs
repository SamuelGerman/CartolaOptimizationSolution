using System.Text.Json.Serialization;

namespace CartolaOptimization.Infrastructure.Integrations.GatoMestre.Models;

/// <summary>
/// Representa as URLs do escudo do time retornadas pelo Gato Mestre.
/// </summary>
public sealed class GatoMestreTeamShieldResponse
{
    [JsonPropertyName("60x60")]
    public string Size60 { get; init; } = string.Empty;

    [JsonPropertyName("45x45")]
    public string Size45 { get; init; } = string.Empty;

    [JsonPropertyName("30x30")]
    public string Size30 { get; init; } = string.Empty;
}
