namespace CartolaOptimization.Application.Models;

/// <summary>
/// Result of training (or attempting to train) the athlete-score model for a single Cartola position.
/// </summary>
public sealed class PositionalAthleteScoreModelResult
{
    public int PositionCartolaId { get; init; }

    public string PositionCode { get; init; } = string.Empty;

    public string PositionName { get; init; } = string.Empty;

    /// <summary>Whether this position's model was successfully trained in this run.</summary>
    public bool Trained { get; init; }

    /// <summary>When <see cref="Trained"/> is <c>false</c>, the reason training was skipped.</summary>
    public string? SkipReason { get; init; }

    /// <summary>Training rows used for this position's model.</summary>
    public int TrainingRows { get; init; }

    /// <summary>Holdout rows used for validation.</summary>
    public int ValidationRows { get; init; }

    /// <summary>Model info when training succeeded; <c>null</c> when skipped.</summary>
    public AthleteScoreModelInfo? ModelInfo { get; init; }
}
