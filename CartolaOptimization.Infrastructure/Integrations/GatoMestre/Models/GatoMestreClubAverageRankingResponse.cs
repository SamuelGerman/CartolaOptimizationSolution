using System.Text.Json.Serialization;

namespace CartolaOptimization.Infrastructure.Integrations.GatoMestre.Models;

public sealed class GatoMestreClubAverageRankingResponse
{
    [JsonPropertyName("clube_id")]
    public string ClubId { get; init; } = string.Empty;

    [JsonPropertyName("medias")]
    public GatoMestreClubAverageRankingBucketsResponse Averages { get; init; } = new();
}
