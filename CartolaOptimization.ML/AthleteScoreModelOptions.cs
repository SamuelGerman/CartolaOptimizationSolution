namespace CartolaOptimization.ML;

/// <summary>
/// Defines file-system options used to persist the v0 athlete-score model artifacts.
/// </summary>
public sealed class AthleteScoreModelOptions
{
    public string ArtifactsDirectory { get; set; } = "artifacts\\ml";

    public string ModelFileName { get; set; } = "athlete-score-v0.zip";

    public string MetadataFileName { get; set; } = "athlete-score-v0.metadata.json";

    public string ModelVersion { get; set; } = "athlete-score-v0";
}
