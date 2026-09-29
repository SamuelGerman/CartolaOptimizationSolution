namespace CartolaOptimization.Application.Models;

/// <summary>
/// Representa o resultado completo de uma execução de busca aleatória de hiperparâmetros, incluindo, por trial,
/// resultados, a melhor combinação encontrada e o modelo final retreinado com essa combinação.
/// </summary>
public sealed class HyperparameterSearchResult
{
    /// <summary>Resumo legível do resultado da busca.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Número de trials aleatórios avaliados.</summary>
    public int NumberOfTrials { get; init; }

    /// <summary>Número de folds de validação cruzada walk-forward usados por trial.</summary>
    public int ValidationFolds { get; init; }

    /// <summary>Combinação de hiperparâmetros com o menor MAE de validação cruzada.</summary>
    public LightGbmHyperparameters BestHyperparameters { get; init; } = new();

    /// <summary>MAE de validação cruzada do melhor trial (menor é melhor).</summary>
    public double BestCrossValidationMae { get; init; }

    /// <summary>R² de validação cruzada do melhor trial (maior é melhor).</summary>
    public double BestCrossValidationRSquared { get; init; }

    /// <summary>
    /// Todos os trials avaliados, ordenados pelo MAE de validação cruzada crescente (melhor primeiro).
    /// Útil para inspecionar todo o panorama da busca.
    /// </summary>
    public IReadOnlyList<HyperparameterTrialResult> Trials { get; init; } = [];

    /// <summary>
    /// Metadados do modelo retreinado com os melhores hiperparâmetros sobre todo o conjunto de dados de treino,
    /// ou <c>null</c> quando <c>applyBest</c> era <c>false</c>.
    /// </summary>
    public AthleteScoreModelInfo? RetrainedModelInfo { get; init; }
}
