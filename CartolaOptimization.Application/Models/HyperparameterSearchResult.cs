namespace CartolaOptimization.Application.Models;

/// <summary>
/// Represents the full outcome of a hyperparameter random-search run, including per-trial
/// results, the best combination found, and the final model retrained with that combination.
/// </summary>
public sealed class HyperparameterSearchResult
{
    /// <summary>Human-readable summary of the search outcome.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Number of random trials evaluated.</summary>
    public int NumberOfTrials { get; init; }

    /// <summary>Number of walk-forward cross-validation folds used per trial.</summary>
    public int ValidationFolds { get; init; }

    /// <summary>Hyperparameter combination with the lowest cross-validation MAE.</summary>
    public LightGbmHyperparameters BestHyperparameters { get; init; } = new();

    /// <summary>Cross-validation MAE of the best trial (lower is better).</summary>
    public double BestCrossValidationMae { get; init; }

    /// <summary>Cross-validation R² of the best trial (higher is better).</summary>
    public double BestCrossValidationRSquared { get; init; }

    /// <summary>
    /// All evaluated trials, ordered by cross-validation MAE ascending (best first).
    /// Useful for inspecting the full search landscape.
    /// </summary>
    public IReadOnlyList<HyperparameterTrialResult> Trials { get; init; } = [];

    /// <summary>
    /// Metadata of the model retrained with the best hyperparameters on the full training dataset,
    /// or <c>null</c> when <c>applyBest</c> was <c>false</c>.
    /// </summary>
    public AthleteScoreModelInfo? RetrainedModelInfo { get; init; }
}
