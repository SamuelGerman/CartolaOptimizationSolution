namespace CartolaOptimization.Application.Models;

/// <summary>
/// Representa uma visão agregada da janela de prontidão do conjunto de dados de treino persistido.
/// </summary>
public sealed class TrainingDatasetCoverageSummary
{
    public int? FromRound { get; init; }

    public int? ToRound { get; init; }

    public int AthleteRoundsTotal { get; init; }

    public int AthleteRoundsEligible { get; init; }

    public int AthleteRoundsIneligible { get; init; }

    public int AthleteRoundsComplete { get; init; }

    public int AthleteRoundsPartial { get; init; }

    public int AthleteRoundsUnfit { get; init; }

    public int AthleteRoundsHistoricalClosed { get; init; }

    public int AthleteRoundsCurrentOpen { get; init; }

    public int AthleteRoundsUnknownPhase { get; init; }

    public decimal AthleteEligibilityRate { get; init; }

    public int ClubRoundsTotal { get; init; }

    public int ClubRoundsEligible { get; init; }

    public int ClubRoundsIneligible { get; init; }

    public int ClubRoundsComplete { get; init; }

    public int ClubRoundsPartial { get; init; }

    public int ClubRoundsUnfit { get; init; }

    public decimal ClubEligibilityRate { get; init; }

    public IReadOnlyCollection<TrainingReadinessReasonCountItem> AthleteTopBlockingReasons { get; init; } = [];

    public IReadOnlyCollection<TrainingReadinessReasonCountItem> ClubTopBlockingReasons { get; init; } = [];
}
