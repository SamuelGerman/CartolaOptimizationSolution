using System.Text.Json.Serialization;

namespace CartolaOptimization.Infrastructure.Integrations.Cartola.Models;

public sealed class CartolaMarketStatusResponse
{
    [JsonPropertyName("rodada_atual")]
    public int CurrentRound { get; init; }

    [JsonPropertyName("temporada")]
    public int Season { get; init; }
}
