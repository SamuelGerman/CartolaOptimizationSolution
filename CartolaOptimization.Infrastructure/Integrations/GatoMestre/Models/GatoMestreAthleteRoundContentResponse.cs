using System.Text.Json.Serialization;

namespace CartolaOptimization.Infrastructure.Integrations.GatoMestre.Models;

/// <summary>
/// Representa o payload de conteúdo de rodada por atleta publicado pelo endpoint legado de conteúdo v1.
/// </summary>
public sealed class GatoMestreAthleteRoundContentResponse
{
    [JsonPropertyName("atleta_id")]
    public int AthleteId { get; init; }

    [JsonPropertyName("equipe_id")]
    public int TeamId { get; init; }

    [JsonPropertyName("media_pontos_equipe")]
    public decimal TeamAverageScore { get; init; }

    [JsonPropertyName("media_mandante_atual")]
    public decimal TeamHomeAverageCurrent { get; init; }

    [JsonPropertyName("media_mandante_anterior")]
    public decimal TeamHomeAveragePrevious { get; init; }

    [JsonPropertyName("media_visitante_atual")]
    public decimal TeamAwayAverageCurrent { get; init; }

    [JsonPropertyName("media_visitante_anterior")]
    public decimal TeamAwayAveragePrevious { get; init; }

    [JsonPropertyName("pontos_por_cartoleta_atual")]
    public decimal PointsPerCartoletaCurrent { get; init; }

    [JsonPropertyName("pontos_por_cartoleta_anterior")]
    public decimal PointsPerCartoletaPrevious { get; init; }

    [JsonPropertyName("media_bruta_geral_atual")]
    public decimal GrossOverallAverageCurrent { get; init; }

    [JsonPropertyName("media_bruta_geral_movel_atual")]
    public decimal GrossOverallMovingAverageCurrent { get; init; }

    [JsonPropertyName("media_bruta_geral_anterior")]
    public decimal GrossOverallAveragePrevious { get; init; }

    [JsonPropertyName("media_bruta_geral_movel_anterior")]
    public decimal GrossOverallMovingAveragePrevious { get; init; }

    [JsonPropertyName("media_liquida_geral_atual")]
    public decimal NetOverallAverageCurrent { get; init; }

    [JsonPropertyName("media_liquida_geral_movel_atual")]
    public decimal NetOverallMovingAverageCurrent { get; init; }

    [JsonPropertyName("media_liquida_geral_anterior")]
    public decimal NetOverallAveragePrevious { get; init; }

    [JsonPropertyName("media_liquida_geral_movel_anterior")]
    public decimal NetOverallMovingAveragePrevious { get; init; }

    [JsonPropertyName("valorizacao_atual")]
    public decimal ValuationCurrent { get; init; }

    [JsonPropertyName("minimo_para_valorizar_atual")]
    public decimal MinimumToAppreciateCurrent { get; init; }

    [JsonPropertyName("valorizacao_anterior")]
    public decimal ValuationPrevious { get; init; }

    [JsonPropertyName("minimo_para_valorizar_anterior")]
    public decimal MinimumToAppreciatePrevious { get; init; }

    [JsonPropertyName("indices")]
    public List<GatoMestreNamedMetricResponse> Indices { get; init; } = [];
}
