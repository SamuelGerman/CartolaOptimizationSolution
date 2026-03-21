using System.Text.Json.Serialization;

namespace CartolaOptimization.Infrastructure.Integrations.GatoMestre.Models;

public sealed class GatoMestreFavoriteMatchOddsResponse
{
    [JsonPropertyName("win")]
    public decimal WinProbability { get; init; }

    [JsonPropertyName("tie")]
    public decimal DrawProbability { get; init; }

    [JsonPropertyName("loss")]
    public decimal LoseProbability { get; init; }
}
