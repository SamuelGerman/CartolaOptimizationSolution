namespace CartolaOptimization.Application.Models;

/// <summary>
/// Representa as restrições de orçamento e de formação (opcional) usadas para montar uma escalação prevista ótima.
/// </summary>
public sealed class PredictedLineupRequest
{
    public decimal Budget { get; init; }

    public IReadOnlyCollection<PredictedLineupPositionRequirement>? Formation { get; init; }
}
