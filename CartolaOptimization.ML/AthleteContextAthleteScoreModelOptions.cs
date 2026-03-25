namespace CartolaOptimization.ML;

/// <summary>
/// Defines file-system options used to persist the v1 athlete-context model artifacts.
/// </summary>
public sealed class AthleteContextAthleteScoreModelOptions
{
    public string ArtifactsDirectory { get; set; } = "artifacts\\ml";

    public string ModelFileName { get; set; } = "athlete-score-v1-athlete-context.zip";

    public string MetadataFileName { get; set; } = "athlete-score-v1-athlete-context.metadata.json";

    public string ModelVersion { get; set; } = "athlete-score-v1-athlete-context";
}
