namespace CartolaOptimization.Application.Models;

/// <summary>
/// Represents a Cartola reserve athlete selection keyed by the official position identifier.
/// </summary>
public sealed class CartolaReserveSelection
{
    public int PositionCartolaId { get; init; }

    public int AthleteCartolaId { get; init; }
}
