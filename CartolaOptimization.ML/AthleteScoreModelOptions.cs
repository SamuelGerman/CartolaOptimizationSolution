namespace CartolaOptimization.ML;

/// <summary>
/// Define opções de sistema de arquivos usadas para persistir os artefatos do modelo de pontuação de atletas v0.
/// </summary>
public sealed class AthleteScoreModelOptions
{
    public string ArtifactsDirectory { get; set; } = "artifacts\\ml";

    public string ModelFileName { get; set; } = "athlete-score-v0.zip";

    public string MetadataFileName { get; set; } = "athlete-score-v0.metadata.json";

    public string ModelVersion { get; set; } = "athlete-score-v0";
}
