using System.Text.Json.Serialization;

namespace CartolaOptimization.Infrastructure.Integrations.GatoMestre.Models;

/// <summary>
/// Representa o payload de agregação de atletas retornado para uma rodada específica.
/// </summary>
public sealed class GatoMestreAthletesRoundResponse
{
    [JsonPropertyName("lastUpdate")]
    public string LastUpdate { get; init; } = string.Empty;

    [JsonPropertyName("result")]
    public List<GatoMestreAthleteRoundSummaryResponse> Result { get; init; } = [];
}
