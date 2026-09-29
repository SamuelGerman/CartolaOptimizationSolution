namespace CartolaOptimization.Application.Models;

/// <summary>
/// Representa o resultado de uma execução de backfill histórico do Gato Mestre.
/// </summary>
public sealed class GatoMestreBackfillResult
{
    public int Season { get; init; }

    public int FromRound { get; init; }

    public int ToRound { get; init; }

    public int RequestedRounds { get; init; }

    public int SuccessfulRounds { get; init; }

    public int PartialRounds { get; init; }

    public int FailedRounds { get; init; }

    public IReadOnlyCollection<GatoMestreBackfillRoundResult> Rounds { get; init; } = Array.Empty<GatoMestreBackfillRoundResult>();
}
