using System.Text.Json.Serialization;

namespace CartolaOptimization.Infrastructure.Integrations.GatoMestre.Models;

/// <summary>
/// Representa o bloco de preço retornado pelo Gato Mestre para uma atleta-rodada específica.
/// </summary>
public sealed class GatoMestrePriceSnapshotResponse
{
    [JsonPropertyName("num")]
    public decimal? Num { get; init; }

    [JsonPropertyName("variacao")]
    public decimal? Variation { get; init; }
}
