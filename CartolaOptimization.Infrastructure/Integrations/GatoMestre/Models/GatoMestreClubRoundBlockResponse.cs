using System.Text.Json.Serialization;

namespace CartolaOptimization.Infrastructure.Integrations.GatoMestre.Models;

/// <summary>
/// Represents one side of the confrontation payload.
/// </summary>
public sealed class GatoMestreClubRoundBlockResponse
{
    [JsonPropertyName("equipe_id")]
    public int TeamId { get; init; }

    [JsonPropertyName("media_geral_atual")]
    public decimal OverallAverage { get; init; }

    [JsonPropertyName("media_movel_atual")]
    public decimal MovingAverage { get; init; }

    [JsonPropertyName("proximo_confronto")]
    public GatoMestreNextConfrontationResponse NextConfrontation { get; init; } = new();

    [JsonPropertyName("pontos_cedidos_conquistados_por_posicao")]
    public GatoMestrePointsByPositionBlockResponse PositionStats { get; init; } = new();
}
