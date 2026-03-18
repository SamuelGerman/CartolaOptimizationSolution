using System.Text.Json.Serialization;

namespace CartolaOptimization.Infrastructure.Integrations.Cartola.Models;

public sealed class CartolaMatchResponse
{
    [JsonPropertyName("partida_id")]
    public int MatchId { get; init; }

    [JsonPropertyName("partida_data")]
    public string MatchDate { get; init; } = string.Empty;

    [JsonPropertyName("local")]
    public string Location { get; init; } = string.Empty;

    [JsonPropertyName("valida")]
    public bool ValidForCartola { get; init; }

    [JsonPropertyName("clube_casa_id")]
    public int HomeClubId { get; init; }

    [JsonPropertyName("clube_casa_posicao")]
    public int HomeClubPosition { get; init; }

    [JsonPropertyName("aproveitamento_mandante")]
    public List<string> HomeForm { get; init; } = [];

    [JsonPropertyName("placar_oficial_mandante")]
    public int? HomeScore { get; init; }

    [JsonPropertyName("clube_visitante_id")]
    public int AwayClubId { get; init; }

    [JsonPropertyName("clube_visitante_posicao")]
    public int AwayClubPosition { get; init; }

    [JsonPropertyName("aproveitamento_visitante")]
    public List<string> AwayForm { get; init; } = [];

    [JsonPropertyName("placar_oficial_visitante")]
    public int? AwayScore { get; init; }
}
