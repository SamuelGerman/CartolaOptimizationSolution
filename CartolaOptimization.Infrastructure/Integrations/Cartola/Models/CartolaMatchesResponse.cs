using System.Text.Json.Serialization;

namespace CartolaOptimization.Infrastructure.Integrations.Cartola.Models;

public sealed class CartolaMatchesResponse
{
    [JsonPropertyName("clubes")]
    public Dictionary<int, CartolaClubResponse> Clubs { get; init; } = [];

    [JsonPropertyName("partidas")]
    public List<CartolaMatchResponse> Matches { get; init; } = [];

    [JsonPropertyName("rodada")]
    public int Round { get; init; }
}
