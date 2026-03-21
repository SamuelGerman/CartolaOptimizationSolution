using System.Text.Json.Serialization;

namespace CartolaOptimization.Infrastructure.Integrations.GatoMestre.Models;

public sealed class GatoMestreClubPointsBalanceResponse
{
    [JsonPropertyName("media_geral_cedidos")]
    public decimal OverallPointsConcededAverage { get; init; }

    [JsonPropertyName("media_geral_conquistados")]
    public decimal OverallPointsConqueredAverage { get; init; }

    [JsonPropertyName("media_mandante_cedidos")]
    public decimal HomePointsConcededAverage { get; init; }

    [JsonPropertyName("media_mandante_conquistados")]
    public decimal HomePointsConqueredAverage { get; init; }

    [JsonPropertyName("media_visitante_cedidos")]
    public decimal AwayPointsConcededAverage { get; init; }

    [JsonPropertyName("media_visitante_conquistados")]
    public decimal AwayPointsConqueredAverage { get; init; }
}
