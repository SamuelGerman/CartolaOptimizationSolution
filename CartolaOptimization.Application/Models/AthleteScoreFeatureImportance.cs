namespace CartolaOptimization.Application.Models;

/// <summary>
/// Represents the permutation-based importance of a single feature group in the full-context athlete-score model.
/// A more negative <see cref="MeanRSquaredDrop"/> indicates the feature had a larger impact on the model's
/// predictive accuracy: permuting it caused the biggest drop in R².
/// </summary>
public sealed class AthleteScoreFeatureImportance
{
    /// <summary>
    /// Feature column name for scalar numeric features, or the aggregated group name for one-hot encoded
    /// categorical features (e.g. "ClubEncoded", "PositionEncoded").
    /// </summary>
    public string FeatureName { get; init; } = string.Empty;

    /// <summary>
    /// Average change in R² across permutation runs when this feature is shuffled on the holdout set.
    /// A more negative value indicates a more important feature (permuting it degraded the model more).
    /// Near-zero values indicate features that contribute little to prediction accuracy.
    /// </summary>
    public double MeanRSquaredDrop { get; init; }

    /// <summary>
    /// Standard deviation of the R² change across permutation runs, providing a reliability estimate
    /// for the importance measurement.
    /// </summary>
    public double RSquaredDropStdDev { get; init; }

    /// <summary>
    /// Importance rank where 1 is the most important feature group.
    /// </summary>
    public int Rank { get; init; }

    /// <summary>
    /// Whether this feature was included in the final trained model.
    /// Always true when no <c>topNFeatures</c> filter was applied during training.
    /// False for numeric features that were dropped because they ranked below the top-N cutoff.
    /// Categorical encoded features (Club, Opponent, Position, Status) are always selected.
    /// </summary>
    public bool IsSelected { get; init; }
}
