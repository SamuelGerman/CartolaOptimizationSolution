using System.Text.Json.Serialization;

namespace CartolaOptimization.Infrastructure.Integrations.Cartola.Models;

public sealed class CartolaAthleteScoreResponse
{
    [JsonPropertyName("apelido")]
    public string Nickname { get; init; } = string.Empty;

    [JsonPropertyName("pontuacao")]
    public decimal Score { get; init; }

    [JsonPropertyName("posicao_id")]
    public int PositionId { get; init; }

    [JsonPropertyName("clube_id")]
    public int ClubId { get; init; }

    [JsonPropertyName("entrou_em_campo")]
    public bool WasInGame { get; init; }

    [JsonPropertyName("scout")]
    public Dictionary<string, decimal>? Scout { get; init; } = [];
}
