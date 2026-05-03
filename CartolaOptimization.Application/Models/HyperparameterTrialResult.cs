namespace CartolaOptimization.Application.Models;

/// <summary>
/// Represents the outcome of a single hyperparameter combination evaluated during a
/// random-search walk-forward cross-validation run.
/// </summary>
public sealed class HyperparameterTrialResult
{
    /// <summary>Sequential trial number, starting at 1.</summary>
    public int TrialNumber { get; init; }

    /// <summary>Hyperparameter combination evaluated in this trial.</summary>
    public LightGbmHyperparameters Hyperparameters { get; init; } = new();

    /// <summary>Mean absolute error averaged across all walk-forward cross-validation folds.</summary>
    public double CrossValidationMae { get; init; }

    /// <summary>R² averaged across all walk-forward cross-validation folds.</summary>
    public double CrossValidationRSquared { get; init; }

    /// <summary>Per-fold mean absolute error values, ordered by fold index (earliest holdout first).</summary>
    public IReadOnlyList<double> FoldMaes { get; init; } = [];

    /// <summary>Per-fold R² values, ordered by fold index (earliest holdout first).</summary>
    public IReadOnlyList<double> FoldRSquareds { get; init; } = [];

    /// <summary>Whether this trial produced the lowest cross-validation MAE across the entire search.</summary>
    public bool IsBest { get; init; }
}
