namespace CartolaOptimization.Application.Models;

/// <summary>
/// Representa o resultado de uma única combinação de hiperparâmetros avaliada durante uma
/// random-search walk-forward cross-validation run.
/// </summary>
public sealed class HyperparameterTrialResult
{
    /// <summary>Número sequencial do trial, começando em 1.</summary>
    public int TrialNumber { get; init; }

    /// <summary>Combinação de hiperparâmetros avaliada neste trial.</summary>
    public LightGbmHyperparameters Hyperparameters { get; init; } = new();

    /// <summary>Erro absoluto médio calculado sobre todos os folds da validação cruzada walk-forward.</summary>
    public double CrossValidationMae { get; init; }

    /// <summary>R² médio calculado sobre todos os folds da validação cruzada walk-forward.</summary>
    public double CrossValidationRSquared { get; init; }

    /// <summary>Valores de erro absoluto médio por fold, ordenados pelo índice do fold (holdout mais antigo primeiro).</summary>
    public IReadOnlyList<double> FoldMaes { get; init; } = [];

    /// <summary>Valores de R² por fold, ordenados pelo índice do fold (holdout mais antigo primeiro).</summary>
    public IReadOnlyList<double> FoldRSquareds { get; init; } = [];

    /// <summary>Se este trial produziu o menor MAE de validação cruzada em toda a busca.</summary>
    public bool IsBest { get; init; }
}
