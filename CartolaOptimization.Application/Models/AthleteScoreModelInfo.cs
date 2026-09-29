namespace CartolaOptimization.Application.Models;

/// <summary>
/// Representa os metadados persistidos sobre um artefato de modelo de pontuação de atletas treinado.
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

    /// <summary>Número total de features numéricas disponíveis antes de aplicar qualquer filtro top-N.</summary>
    public int TotalNumericFeatureCount { get; init; }

    /// <summary>Número de features numéricas efetivamente usadas no modelo final treinado.</summary>
    public int ActiveNumericFeatureCount { get; init; }

    /// <summary>
    /// Importância por permutação de cada feature, ordenada da mais para a menos importante.
    /// Preenchido após cada execução de treino. Vazio para artefatos treinados antes desta feature ser introduzida.
    /// </summary>
    public IReadOnlyList<AthleteScoreFeatureImportance> FeatureImportances { get; init; } = [];

    /// <summary>
    /// Hiperparâmetros do LightGBM que estavam ativos quando este artefato de modelo foi treinado.
    /// </summary>
    public LightGbmHyperparameters Hyperparameters { get; init; } = new();
}
