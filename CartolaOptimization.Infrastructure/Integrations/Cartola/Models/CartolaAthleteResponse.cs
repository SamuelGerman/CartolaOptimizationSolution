using System.Text.Json.Serialization;

namespace CartolaOptimization.Infrastructure.Integrations.Cartola.Models;

public sealed class CartolaAthleteResponse
{
    [JsonPropertyName("atleta_id")]
    public int AthleteId { get; init; }

    [JsonPropertyName("rodada_id")]
    public int RoundId { get; init; }

    [JsonPropertyName("clube_id")]
    public int ClubId { get; init; }

    [JsonPropertyName("posicao_id")]
    public int PositionId { get; init; }

    [JsonPropertyName("status_id")]
    public int StatusId { get; init; }

    [JsonPropertyName("pontos_num")]
    public decimal Score { get; init; }

    [JsonPropertyName("media_num")]
    public decimal AverageScore { get; init; }

    [JsonPropertyName("variacao_num")]
    public decimal PriceVariation { get; init; }

    [JsonPropertyName("preco_num")]
    public decimal Price { get; init; }

    [JsonPropertyName("jogos_num")]
    public int Games { get; init; }

    [JsonPropertyName("entrou_em_campo")]
    public bool WasInGame { get; init; }

    [JsonPropertyName("apelido")]
    public string Nickname { get; init; } = string.Empty;

    [JsonPropertyName("apelido_abreviado")]
    public string ShortNickname { get; init; } = string.Empty;

    [JsonPropertyName("nome")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("scout")]
    public Dictionary<string, decimal> Scout { get; init; } = [];
}
