using System.Text.Json.Serialization;

namespace CartolaOptimization.Infrastructure.Integrations.GatoMestre.Models;

public sealed class GatoMestreClubPerformanceResponse
{
    [JsonPropertyName("vitorias")]
    public GatoMestreHistoricalResultResponse Wins { get; init; } = new();

    [JsonPropertyName("empates")]
    public GatoMestreHistoricalResultResponse Draws { get; init; } = new();

    [JsonPropertyName("derrotas")]
    public GatoMestreHistoricalResultResponse Losses { get; init; } = new();
}
