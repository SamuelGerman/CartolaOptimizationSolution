using System.Text.Json.Serialization;

namespace CartolaOptimization.Infrastructure.Integrations.GatoMestre.Models;

/// <summary>
/// Represents home and away position statistics for a team.
/// </summary>
public sealed class GatoMestrePointsByPositionBlockResponse
{
    [JsonPropertyName("mandante")]
    public List<GatoMestreClubPositionStatsResponse> Home { get; init; } = [];

    [JsonPropertyName("visitante")]
    public List<GatoMestreClubPositionStatsResponse> Away { get; init; } = [];
}
