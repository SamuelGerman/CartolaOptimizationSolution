using System.Text.Json.Serialization;

namespace CartolaOptimization.Infrastructure.Integrations.GatoMestre.Models;

/// <summary>
/// Representa as médias do atleta nos contextos geral, casa e fora.
/// </summary>
public sealed class GatoMestreDetailedAveragesResponse
{
    [JsonPropertyName("geral")]
    public GatoMestreAveragesBucketResponse Overall { get; init; } = new();

    [JsonPropertyName("mandante")]
    public GatoMestreAveragesBucketResponse Home { get; init; } = new();

    [JsonPropertyName("visitante")]
    public GatoMestreAveragesBucketResponse Away { get; init; } = new();
}
