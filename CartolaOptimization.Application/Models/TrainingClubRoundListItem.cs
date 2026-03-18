namespace CartolaOptimization.Application.Models;

/// <summary>
/// Represents a compact row used to inspect club-round enrichment readiness in the training dataset.
/// </summary>
public sealed class TrainingClubRoundListItem
{
    public long Id { get; init; }

    public int Round { get; init; }

    public int ClubCartolaId { get; init; }

    public string ClubName { get; init; } = string.Empty;

    public decimal WinProbability { get; init; }

    public decimal DrawProbability { get; init; }

    public decimal LoseProbability { get; init; }

    public decimal OverallAverage { get; init; }

    public decimal MovingAverage { get; init; }

    public decimal ExpectedPoints { get; init; }

    public int PositionStatsCount { get; init; }

    public string TrainingReadinessStatus { get; init; } = string.Empty;

    public string TrainingReadinessReason { get; init; } = string.Empty;

    public bool IsTrainingEligible { get; init; }
}
