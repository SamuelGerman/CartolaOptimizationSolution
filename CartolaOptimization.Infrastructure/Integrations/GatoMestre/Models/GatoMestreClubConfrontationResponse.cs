using System.Text.Json.Serialization;

namespace CartolaOptimization.Infrastructure.Integrations.GatoMestre.Models;

/// <summary>
/// Represents the confrontation payload used to build team round features.
/// </summary>
public sealed class GatoMestreClubConfrontationResponse
{
    [JsonPropertyName("rodada")]
    public int Round { get; init; }

    [JsonPropertyName("mandante")]
    public GatoMestreClubRoundBlockResponse Home { get; init; } = new();

    [JsonPropertyName("visitante")]
    public GatoMestreClubRoundBlockResponse Away { get; init; } = new();
}
