using System.Text.Json.Serialization;

namespace CartolaOptimization.Infrastructure.Integrations.GatoMestre.Models;

/// <summary>
/// Represents the athlete detail payload returned by Gato Mestre.
/// </summary>
public sealed class GatoMestreAthleteDetailResponse
{
    [JsonPropertyName("atleta")]
    public GatoMestreAthleteProfileResponse Athlete { get; init; } = new();

    [JsonPropertyName("equipe")]
    public GatoMestreTeamResponse Team { get; init; } = new();

    [JsonPropertyName("rodadas")]
    public List<GatoMestreAthleteRoundDetailResponse> Rounds { get; init; } = [];
}
