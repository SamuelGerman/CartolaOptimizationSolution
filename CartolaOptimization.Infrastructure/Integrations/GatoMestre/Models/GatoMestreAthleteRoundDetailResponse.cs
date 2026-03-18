using System.Text.Json.Serialization;

namespace CartolaOptimization.Infrastructure.Integrations.GatoMestre.Models;

/// <summary>
/// Represents a round entry inside the athlete detail payload.
/// </summary>
public sealed class GatoMestreAthleteRoundDetailResponse
{
    [JsonPropertyName("rodada")]
    public int Round { get; init; }

    [JsonPropertyName("preco")]
    public GatoMestrePriceSnapshotResponse? Price { get; init; }

    [JsonPropertyName("pontos")]
    public GatoMestreNumericSnapshotResponse? Points { get; init; }

    [JsonPropertyName("medias")]
    public GatoMestreDetailedAveragesResponse Averages { get; init; } = new();

    [JsonPropertyName("entrou_em_campo")]
    public bool WasInGame { get; init; }

    [JsonPropertyName("quantidade_jogos")]
    public int GamesCount { get; init; }

    [JsonPropertyName("status_pre")]
    public string StatusName { get; init; } = string.Empty;

    [JsonPropertyName("scouts")]
    public Dictionary<string, decimal> Scouts { get; init; } = [];
}
