using System.Text.Json.Serialization;

namespace CartolaOptimization.Infrastructure.Integrations.GatoMestre.Models;

public sealed class GatoMestreClubAverageRankingBucketsResponse
{
    [JsonPropertyName("geral")]
    public GatoMestreAveragesBucketResponse Overall { get; init; } = new();

    [JsonPropertyName("mandante")]
    public GatoMestreAveragesBucketResponse Home { get; init; } = new();

    [JsonPropertyName("visitante")]
    public GatoMestreAveragesBucketResponse Away { get; init; } = new();
}
