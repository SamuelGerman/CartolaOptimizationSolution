using System.Text.Json.Serialization;

namespace CartolaOptimization.Infrastructure.Integrations.GatoMestre.Models;

/// <summary>
/// Represents the next confrontation probability block returned for a team.
/// </summary>
public sealed class GatoMestreNextConfrontationResponse
{
    [JsonPropertyName("mando")]
    public string HomeAwayFlag { get; init; } = string.Empty;

    [JsonPropertyName("empate")]
    public decimal DrawProbability { get; init; }

    [JsonPropertyName("equipe_mandante_id")]
    public int HomeTeamId { get; init; }

    [JsonPropertyName("vitoria_mandante")]
    public decimal HomeWinProbability { get; init; }

    [JsonPropertyName("expectativa_pontos_mandante")]
    public decimal HomeExpectedPoints { get; init; }

    [JsonPropertyName("equipe_visitante_id")]
    public int AwayTeamId { get; init; }

    [JsonPropertyName("vitoria_visitante")]
    public decimal AwayWinProbability { get; init; }

    [JsonPropertyName("expectativa_pontos_visitante")]
    public decimal AwayExpectedPoints { get; init; }
}
