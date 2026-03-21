using System.Text.Json.Serialization;

namespace CartolaOptimization.Infrastructure.Integrations.GatoMestre.Models;

public sealed class GatoMestreRoundCollectionResponse<TItem>
{
    [JsonPropertyName("lastUpdate")]
    public string LastUpdate { get; init; } = string.Empty;

    [JsonPropertyName("result")]
    public List<TItem> Result { get; init; } = [];
}
