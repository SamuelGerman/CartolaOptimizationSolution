using System.Text.Json.Serialization;

namespace CartolaOptimization.Infrastructure.Integrations.Cartola.Models;

public sealed class CartolaAthletesMarketResponse
{
    [JsonPropertyName("clubes")]
    public Dictionary<int, CartolaClubResponse> Clubs { get; init; } = [];

    [JsonPropertyName("posicoes")]
    public Dictionary<int, CartolaPositionResponse> Positions { get; init; } = [];

    [JsonPropertyName("status")]
    public Dictionary<int, CartolaStatusResponse> Statuses { get; init; } = [];

    [JsonPropertyName("atletas")]
    public List<CartolaAthleteResponse> Athletes { get; init; } = [];
}
