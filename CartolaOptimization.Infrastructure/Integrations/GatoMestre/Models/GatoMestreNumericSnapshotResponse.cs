using System.Text.Json.Serialization;

namespace CartolaOptimization.Infrastructure.Integrations.GatoMestre.Models;

/// <summary>
/// Representa um valor numérico opcionalmente acompanhado de uma variação.
/// </summary>
public sealed class GatoMestreNumericSnapshotResponse
{
    [JsonPropertyName("num")]
    public decimal? Num { get; init; }

    [JsonPropertyName("variacao")]
    public decimal? Variation { get; init; }
}
