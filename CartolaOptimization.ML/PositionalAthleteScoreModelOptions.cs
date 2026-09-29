using CartolaOptimization.Application.Models;

namespace CartolaOptimization.ML;

/// <summary>
/// Configuração dos artefatos e do comportamento de treino do modelo de pontuação de atletas por posição.
/// </summary>
public sealed class PositionalAthleteScoreModelOptions
{
    /// <summary>Diretório onde os artefatos .zip e .metadata.json específicos de cada posição são armazenados.</summary>
    public string ArtifactsDirectory { get; set; } = "artifacts\\ml";

    /// <summary>
    /// Número mínimo de linhas de treino exigidas para que uma posição seja treinada.
    /// Posições com menos linhas produzem modelos subajustados e são descartadas.
    /// </summary>
    public int MinimumTrainingRows { get; set; } = 30;

    /// <summary>
    /// Número padrão de features numéricas mais bem ranqueadas (por PFI) a usar no modelo final.
    /// Defina como <c>null</c> para treinar em todas as features numéricas disponíveis.
    /// </summary>
    public int? DefaultTopNFeatures { get; set; }

    /// <summary>Hiperparâmetros do LightGBM aplicados ao treinar cada modelo posicional.</summary>
    public LightGbmHyperparameters Hyperparameters { get; set; } = new();
}
