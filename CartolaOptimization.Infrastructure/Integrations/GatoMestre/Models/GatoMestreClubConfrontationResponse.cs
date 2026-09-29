using System.Text.Json.Serialization;

namespace CartolaOptimization.Infrastructure.Integrations.GatoMestre.Models;

/// <summary>
/// Representa o payload de confronto usado para montar as features de time por rodada.
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
