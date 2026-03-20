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
}
