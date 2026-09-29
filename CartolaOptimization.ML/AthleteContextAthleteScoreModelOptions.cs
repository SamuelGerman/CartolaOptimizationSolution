namespace CartolaOptimization.ML;

/// <summary>
/// Define opções de sistema de arquivos usadas para persistir os artefatos do modelo de contexto de atleta v1.
/// </summary>
public sealed class AthleteContextAthleteScoreModelOptions
{
    public string ArtifactsDirectory { get; set; } = "artifacts\\ml";

    public string ModelFileName { get; set; } = "athlete-score-v1-athlete-context.zip";

    public string MetadataFileName { get; set; } = "athlete-score-v1-athlete-context.metadata.json";

    public string ModelVersion { get; set; } = "athlete-score-v1-athlete-context";
}
