namespace CartolaOptimization.Application.Models;

/// <summary>
/// Representa o resultado do recálculo dos metadados de prontidão para treino das linhas de rodada persistidas.
/// </summary>
public sealed class TrainingDatasetReadinessResult
{
    public int FromRound { get; init; }

    public int ToRound { get; init; }

    public int AthleteRoundsEvaluated { get; init; }

    public int AthleteRoundsComplete { get; init; }

    public int AthleteRoundsPartial { get; init; }

    public int AthleteRoundsUnfit { get; init; }

    public int ClubRoundsEvaluated { get; init; }

    public int ClubRoundsComplete { get; init; }

    public int ClubRoundsPartial { get; init; }

    public int ClubRoundsUnfit { get; init; }
}
