using System.Text.Json.Serialization;

namespace CartolaOptimization.Infrastructure.Integrations.Cartola.Models;

public sealed class CartolaAthleteScoresResponse
{
    [JsonPropertyName("atletas")]
    public Dictionary<int, CartolaAthleteScoreResponse> Athletes { get; init; } = [];

    [JsonPropertyName("clubes")]
    public Dictionary<int, CartolaClubResponse> Clubs { get; init; } = [];

    [JsonPropertyName("posicoes")]
    public Dictionary<int, CartolaPositionResponse> Positions { get; init; } = [];

    [JsonPropertyName("rodada")]
    public int Round { get; init; }

    [JsonPropertyName("total_atletas")]
    public int TotalAthletes { get; init; }
}
