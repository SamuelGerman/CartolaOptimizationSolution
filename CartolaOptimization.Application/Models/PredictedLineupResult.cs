namespace CartolaOptimization.Application.Models;

/// <summary>
/// Represents the optimal machine-learning lineup found for a round under budget and formation constraints.
/// </summary>
public sealed class PredictedLineupResult
{
    public int Round { get; init; }

    public string ModelVersion { get; init; } = string.Empty;

    public DateTime GeneratedAtUtc { get; init; }

    public DateTime ModelTrainedAtUtc { get; init; }

    public decimal Budget { get; init; }

    public decimal TotalCost { get; init; }

    public decimal RemainingBudget { get; init; }

    public decimal TotalPredictedScore { get; init; }

    public int TotalPlayers { get; init; }

    public int TotalProbableAthletesEvaluated { get; init; }

    public int ProbableStatusCartolaId { get; init; }

    public string ProbableStatusName { get; init; } = string.Empty;

    public double ValidationMeanAbsoluteError { get; init; }

    public double ValidationRootMeanSquaredError { get; init; }

    public string OptimizationNotes { get; init; } = string.Empty;

    public IReadOnlyCollection<PredictedLineupPositionRequirement> Formation { get; init; } = [];

    public IReadOnlyCollection<PredictedLineupAthleteItem> Athletes { get; init; } = [];
}
