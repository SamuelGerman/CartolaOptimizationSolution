namespace CartolaOptimization.Application.Models;

/// <summary>
/// Aggregated result of a positional athlete-score model training run across all six Cartola positions.
/// </summary>
public sealed class PositionalAthleteScoreTrainingResult
{
    public string Message { get; init; } = string.Empty;

    public int? FromRound { get; init; }

    public int? ToRound { get; init; }

    public int ValidationRounds { get; init; }

    /// <summary>Number of positions for which a model was successfully trained and persisted.</summary>
    public int TrainedPositions { get; init; }

    /// <summary>Number of positions skipped due to insufficient data.</summary>
    public int SkippedPositions { get; init; }

    /// <summary>Per-position training outcomes ordered by <c>PositionCartolaId</c>.</summary>
    public IReadOnlyList<PositionalAthleteScoreModelResult> Positions { get; init; } = [];
}
