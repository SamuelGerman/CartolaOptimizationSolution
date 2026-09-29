using CartolaOptimization.Application.Models;

namespace CartolaOptimization.ML;

/// <summary>
/// Define opções de sistema de arquivos e de treino para os artefatos do modelo de pontuação de atletas full-context v1.
/// </summary>
public sealed class FullContextAthleteScoreModelOptions
{
    public string ArtifactsDirectory { get; set; } = "artifacts\\ml";

    public string ModelFileName { get; set; } = "athlete-score-v1-full-context.zip";

    public string MetadataFileName { get; set; } = "athlete-score-v1-full-context.metadata.json";

    public string ModelVersion { get; set; } = "athlete-score-v1-full-context";

    /// <summary>
    /// Número padrão de features numéricas mais bem ranqueadas (por importância de permutação) a usar
    /// durante o treino. Defina como <c>null</c> para treinar em todas as features numéricas disponíveis.
    /// Determinado empiricamente: topNFeatures=10 rendeu a melhor generalização (R²=0.896, MAE=0.789)
    /// no conjunto de dados do Brasileirão 2025 em uma busca em grade de 5 a 262 features.
    /// </summary>
    public int? DefaultTopNFeatures { get; set; } = 10;

    /// <summary>
    /// Hiperparâmetros do LightGBM usados ao treinar o modelo full-context.
    /// Atualize esta seção com os valores retornados pelo endpoint de busca de hiperparâmetros
    /// para persistir a configuração ajustada entre reinicializações da aplicação.
    /// </summary>
    public LightGbmHyperparameters Hyperparameters { get; set; } = new();
}
