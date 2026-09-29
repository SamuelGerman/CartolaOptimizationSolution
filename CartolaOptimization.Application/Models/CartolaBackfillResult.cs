namespace CartolaOptimization.Application.Models;

/// <summary>
/// Representa uma execução administrativa de backfill que fecha rodadas históricas usando as pontuações finais do Cartola.
/// </summary>
public sealed class CartolaBackfillResult
{
    public int FromRound { get; init; }

    public int ToRound { get; init; }

    public int RequestedRounds { get; init; }

    public int SuccessfulRounds { get; init; }

    public int PartialRounds { get; init; }

    public int FailedRounds { get; init; }

    public IReadOnlyCollection<CartolaBackfillRoundResult> Rounds { get; init; } = [];
}
