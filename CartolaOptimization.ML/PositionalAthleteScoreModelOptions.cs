using CartolaOptimization.Application.Models;

namespace CartolaOptimization.ML;

/// <summary>
/// Configuration for the per-position athlete-score model artifacts and training behaviour.
/// </summary>
public sealed class PositionalAthleteScoreModelOptions
{
    /// <summary>Directory where position-specific .zip and .metadata.json artifacts are stored.</summary>
    public string ArtifactsDirectory { get; set; } = "artifacts\\ml";

    /// <summary>
    /// Minimum number of training rows required for a position to be trained.
    /// Positions with fewer rows produce underfitted models and are skipped.
    /// </summary>
    public int MinimumTrainingRows { get; set; } = 30;

    /// <summary>
    /// Default number of top-ranked numeric features (by PFI) to use in the final model.
    /// Set to <c>null</c> to train on all available numeric features.
    /// </summary>
    public int? DefaultTopNFeatures { get; set; } = 10;

    /// <summary>LightGBM hyperparameters applied when training each positional model.</summary>
    public LightGbmHyperparameters Hyperparameters { get; set; } = new();
}
