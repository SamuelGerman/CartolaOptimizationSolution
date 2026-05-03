namespace CartolaOptimization.ML;

/// <summary>
/// Defines file-system options used to persist the v1 full-context athlete-score model artifacts.
/// </summary>
public sealed class FullContextAthleteScoreModelOptions
{
    public string ArtifactsDirectory { get; set; } = "artifacts\\ml";

    public string ModelFileName { get; set; } = "athlete-score-v1-full-context.zip";

    public string MetadataFileName { get; set; } = "athlete-score-v1-full-context.metadata.json";

    public string ModelVersion { get; set; } = "athlete-score-v1-full-context";

    /// <summary>
    /// Default number of top-ranked numeric features (by permutation feature importance) to use
    /// during training. Set to <c>null</c> to train on all available numeric features.
    /// Determined empirically: topNFeatures=10 yielded the best generalisation (R²=0.896, MAE=0.789)
    /// on the 2025 Brasileirão dataset across a grid search from 5 to 262 features.
    /// </summary>
    public int? DefaultTopNFeatures { get; set; } = 10;
}
