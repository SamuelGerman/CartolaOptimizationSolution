namespace CartolaOptimization.Application.Models;

/// <summary>
/// Represents the budget and formation constraints used to build an optimal predicted lineup.
/// </summary>
public sealed class PredictedLineupRequest
{
    public decimal Budget { get; init; }

    public IReadOnlyCollection<PredictedLineupPositionRequirement> Formation { get; init; } = [];
}
