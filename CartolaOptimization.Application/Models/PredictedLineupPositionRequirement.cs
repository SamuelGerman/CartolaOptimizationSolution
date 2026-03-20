namespace CartolaOptimization.Application.Models;

/// <summary>
/// Represents how many athletes of a specific Cartola position should be present in the optimized lineup.
/// </summary>
public sealed class PredictedLineupPositionRequirement
{
    public int PositionCartolaId { get; init; }

    public int Quantity { get; init; }
}
