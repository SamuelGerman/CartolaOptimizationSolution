namespace CartolaOptimization.Application.Models;

/// <summary>
/// Represents the aggregated outcome of the automated end-of-round maintenance workflow.
/// </summary>
public sealed class EndOfRoundMaintenanceResult
{
    public string Message { get; init; } = string.Empty;

    public int Season { get; init; }

    public int CurrentRound { get; init; }

    public int LastClosedRound { get; init; }

    public int ValidationRounds { get; init; }

    public bool HistoricalDataUpdated { get; init; }

    public bool ModelRetrained { get; init; }

    public CartolaSyncResult CurrentMarketSync { get; init; } = new();

    public CartolaBackfillResult? CartolaClosedRoundBackfill { get; init; }

    public GatoMestreBackfillResult? GatoMestreBackfill { get; init; }

    public TrainingDatasetReadinessResult? ReadinessRecompute { get; init; }

    public AthleteScoreModelTrainingResult? PrimaryModelTraining { get; init; }

    public AthleteScoreModelInfo? PrimaryModelInfo { get; init; }

    public AthleteScoreModelTrainingResult? AthleteContextModelTraining { get; init; }

    public AthleteScoreModelInfo? AthleteContextModelInfo { get; init; }

    public PositionalAthleteScoreTrainingResult? PositionalModelTraining { get; init; }

    public IReadOnlyCollection<EndOfRoundMaintenanceStepResult> Steps { get; init; } = Array.Empty<EndOfRoundMaintenanceStepResult>();
}
