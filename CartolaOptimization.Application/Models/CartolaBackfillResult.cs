namespace CartolaOptimization.Application.Models;

/// <summary>
/// Represents an administrative backfill execution that closes historical rounds using Cartola final scores.
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
