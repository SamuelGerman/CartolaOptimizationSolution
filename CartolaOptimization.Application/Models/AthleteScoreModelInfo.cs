namespace CartolaOptimization.Application.Models;

/// <summary>
/// Represents persisted metadata about a trained athlete-score model artifact.
/// </summary>
public sealed class AthleteScoreModelInfo
{
    public string ModelVersion { get; init; } = string.Empty;

    public string DatasetKey { get; init; } = string.Empty;

    public string TrainerName { get; init; } = string.Empty;

    public DateTime TrainedAtUtc { get; init; }

    public int? FromRound { get; init; }

    public int? ToRound { get; init; }

    public int ValidationRounds { get; init; }

    public int TrainingRows { get; init; }

    public int ValidationRows { get; init; }

    public int TrainingDistinctRounds { get; init; }

    public int ValidationDistinctRounds { get; init; }

    public double MeanAbsoluteError { get; init; }

    public double RootMeanSquaredError { get; init; }

    public double RSquared { get; init; }

    public string ModelArtifactPath { get; init; } = string.Empty;

    public string MetadataArtifactPath { get; init; } = string.Empty;

    public IReadOnlyList<AthleteScorePositionDiagnostics> PositionDiagnostics { get; init; } = [];

    /// <summary>Total number of numeric features available before any top-N filter is applied.</summary>
    public int TotalNumericFeatureCount { get; init; }

    /// <summary>Number of numeric features actually used in the final trained model.</summary>
    public int ActiveNumericFeatureCount { get; init; }

    /// <summary>
    /// Per-feature permutation importance ranked from most to least important.
    /// Populated after every training run. Empty for artifacts trained before this feature was introduced.
    /// </summary>
    public IReadOnlyList<AthleteScoreFeatureImportance> FeatureImportances { get; init; } = [];

    /// <summary>
    /// LightGBM hyperparameters that were active when this model artifact was trained.
    /// </summary>
    public LightGbmHyperparameters Hyperparameters { get; init; } = new();
}
