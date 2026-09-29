using System.Text.Json.Serialization;

namespace CartolaOptimization.Infrastructure.Integrations.GatoMestre.Models;

/// <summary>
/// Representa as estatísticas de posição em casa e fora de um time.
/// </summary>
public sealed class GatoMestrePointsByPositionBlockResponse
{
    [JsonPropertyName("mandante")]
    public List<GatoMestreClubPositionStatsResponse> Home { get; init; } = [];

    [JsonPropertyName("visitante")]
    public List<GatoMestreClubPositionStatsResponse> Away { get; init; } = [];
}
