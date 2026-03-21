using System.Text.Json.Serialization;

namespace CartolaOptimization.Infrastructure.Integrations.GatoMestre.Models;

public sealed class GatoMestreSimpleTeamReferenceResponse
{
    [JsonPropertyName("abbr")]
    public string Abbreviation { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("image")]
    public string? Image { get; init; }

    [JsonPropertyName("flag")]
    public string? Flag { get; init; }
}
