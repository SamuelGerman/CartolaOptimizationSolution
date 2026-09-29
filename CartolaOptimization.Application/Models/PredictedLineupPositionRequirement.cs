namespace CartolaOptimization.Application.Models;

/// <summary>
/// Representa quantos atletas de uma posição específica do Cartola devem estar presentes na escalação otimizada.
/// </summary>
public sealed class PredictedLineupPositionRequirement
{
    public int PositionCartolaId { get; init; }

    public int Quantity { get; init; }
}
