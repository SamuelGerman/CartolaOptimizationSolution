using CartolaOptimization.Application.Models;

namespace CartolaOptimization.Application.Interfaces;

/// <summary>
/// Defines the hyperparameter search operation for the v1 full-context athlete-score model.
/// Performs random search with walk-forward cross-validation to identify the best LightGBM
/// hyperparameter combination, then optionally retrains the persisted model with those values.
/// </summary>
public interface IFullContextAthleteScoreModelHyperparameterTuningService
{
    /// <summary>
    /// Runs a random-search hyperparameter optimisation using temporal walk-forward
    /// cross-validation over the available training rounds.
    /// </summary>
    /// <param name="fromRound">Optional earliest round to include in the search dataset.</param>
    /// <param name="toRound">Optional latest round to include in the search dataset.</param>
    /// <param name="numberOfTrials">
    /// Number of random hyperparameter combinations to evaluate. More trials improve the
    /// chance of finding the global optimum but increase runtime proportionally.
    /// </param>
    /// <param name="validationFolds">
    /// Number of walk-forward folds. Each fold uses all rounds up to fold <em>k</em> for
    /// training and round <em>k+1</em> as the holdout, progressing from the end of the dataset.
    /// Must be at least 1 and less than the total number of distinct training rounds minus 1.
    /// </param>
    /// <param name="applyBest">
    /// When <c>true</c>, retrains and persists the full-context model with the best
    /// hyperparameter combination found, using the same <c>topNFeatures</c> setting configured
    /// in <c>MachineLearning:FullContext:DefaultTopNFeatures</c>.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Search results including all trial scores, the best combination, and the final model info.</returns>
    Task<HyperparameterSearchResult> SearchAsync(
        int? fromRound = null,
        int? toRound = null,
        int numberOfTrials = 30,
        int validationFolds = 3,
        bool applyBest = true,
        CancellationToken cancellationToken = default);
}
