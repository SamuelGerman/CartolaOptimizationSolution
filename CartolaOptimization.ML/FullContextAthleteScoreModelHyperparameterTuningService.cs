using CartolaOptimization.Application.Interfaces;
using CartolaOptimization.Application.Models;
using CartolaOptimization.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ML;

namespace CartolaOptimization.ML;

/// <summary>
/// Performs random-search hyperparameter optimisation for the full-context LightGBM model using
/// temporal walk-forward cross-validation.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Search strategy — random search</strong>: randomly samples hyperparameter combinations
/// from discrete candidate grids. Random search finds near-optimal configurations faster than
/// grid search in high-dimensional spaces because it covers more of the parameter landscape per
/// trial (Bergstra &amp; Bengio, 2012).
/// </para>
/// <para>
/// <strong>Validation strategy — walk-forward CV</strong>: for <em>k</em> folds the last <em>k</em>
/// distinct rounds are used as successive holdouts, each time expanding the training window.
/// This preserves the temporal ordering of the data and avoids look-ahead bias.
/// </para>
/// <para>
/// <strong>Feature selection during CV</strong>: all available numeric features (no PFI filter)
/// are used inside each CV fold so that every trial is evaluated on a consistent feature space.
/// Applying per-fold PFI would double the training time and introduce fold-specific selection
/// bias. The final model retrained with <c>applyBest=true</c> uses the configured
/// <c>DefaultTopNFeatures</c> (including PFI), matching production semantics.
/// </para>
/// </remarks>
public sealed class FullContextAthleteScoreModelHyperparameterTuningService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IFullContextAthleteScoreModelTrainingService trainingService,
    IOptions<FullContextAthleteScoreModelOptions> options,
    ILogger<FullContextAthleteScoreModelHyperparameterTuningService> logger)
    : IFullContextAthleteScoreModelHyperparameterTuningService
{
    private readonly FullContextAthleteScoreModelOptions options = options.Value;

    // ── Discrete candidate grids ───────────────────────────────────────────────────────────────
    // Values chosen to cover a wide range while keeping total search time reasonable.
    // Iteration counts are bounded by available data size; regularisation values span two decades.
    private static readonly int[] IterationsGrid = [200, 300, 400, 600, 800];
    private static readonly double[] LearningRateGrid = [0.01, 0.02, 0.03, 0.05, 0.08, 0.10, 0.15];
    private static readonly int[] LeavesGrid = [15, 31, 63, 127];
    private static readonly int[] MinLeafGrid = [5, 10, 20, 50];
    private static readonly double[] L1Grid = [0.0, 0.01, 0.1, 1.0];
    private static readonly double[] L2Grid = [0.0, 0.01, 0.1, 1.0];

    public async Task<HyperparameterSearchResult> SearchAsync(
        int? fromRound = null,
        int? toRound = null,
        int numberOfTrials = 30,
        int validationFolds = 3,
        bool applyBest = true,
        CancellationToken cancellationToken = default)
    {
        numberOfTrials = Math.Max(1, numberOfTrials);
        validationFolds = Math.Max(1, validationFolds);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var rows = await FullContextAthleteScoreModelTrainingService.QueryTrainingRowsAsync(
            dbContext, fromRound, toRound, cancellationToken);

        var distinctRounds = rows
            .Select(r => (int)r.RoundId)
            .Distinct()
            .OrderBy(r => r)
            .ToArray();

        // Need at least 2 rounds for training + 1 per fold.
        var minRoundsRequired = validationFolds + 2;
        if (distinctRounds.Length < minRoundsRequired)
        {
            throw new InvalidOperationException(
                $"Walk-forward cross-validation with {validationFolds} fold(s) requires at least " +
                $"{minRoundsRequired} distinct rounds, but only {distinctRounds.Length} are available " +
                $"in the selected window.");
        }

        // Clamp folds so we always have ≥2 training rounds in the first fold.
        validationFolds = Math.Min(validationFolds, distinctRounds.Length - 2);

        // Build walk-forward fold definitions: each fold adds one more round to the holdout set
        // taken from the tail, and the rest form the training window.
        var foldHoldoutRounds = Enumerable.Range(0, validationFolds)
            .Select(foldIndex => distinctRounds[distinctRounds.Length - validationFolds + foldIndex])
            .ToArray();

        logger.LogInformation(
            "Starting hyperparameter search: {Trials} trials × {Folds} walk-forward folds. " +
            "Holdout rounds: [{HoldoutRounds}].",
            numberOfTrials, validationFolds, string.Join(", ", foldHoldoutRounds));

        // Pre-group rows by round for fast fold splitting.
        var rowsByRound = rows
            .GroupBy(r => (int)r.RoundId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var rng = new Random(42);
        var trialResults = new List<HyperparameterTrialResult>(numberOfTrials);

        for (var trial = 1; trial <= numberOfTrials; trial++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var hyperparameters = SampleHyperparameters(rng);
            var foldMaes = new List<double>(validationFolds);
            var foldR2s = new List<double>(validationFolds);

            for (var foldIndex = 0; foldIndex < validationFolds; foldIndex++)
            {
                // Folds accumulate: fold k uses all rounds up to the (k+1)-th holdout round.
                var holdoutRound = foldHoldoutRounds[foldIndex];
                var trainingRounds = distinctRounds
                    .Where(r => r < holdoutRound)
                    .ToHashSet();

                var trainRows = trainingRounds
                    .SelectMany(r => rowsByRound.TryGetValue(r, out var list) ? list : [])
                    .ToList();
                var holdoutRows = rowsByRound.TryGetValue(holdoutRound, out var h) ? h : [];

                if (trainRows.Count == 0 || holdoutRows.Count == 0)
                {
                    continue;
                }

                var (mae, r2) = EvaluateFold(trainRows, holdoutRows, hyperparameters);
                foldMaes.Add(mae);
                foldR2s.Add(r2);
            }

            if (foldMaes.Count == 0)
            {
                logger.LogWarning("Trial {Trial} produced no valid fold results and will be skipped.", trial);
                continue;
            }

            var cvMae = foldMaes.Average();
            var cvR2 = foldR2s.Average();

            trialResults.Add(new HyperparameterTrialResult
            {
                TrialNumber = trial,
                Hyperparameters = hyperparameters,
                CrossValidationMae = cvMae,
                CrossValidationRSquared = cvR2,
                FoldMaes = foldMaes,
                FoldRSquareds = foldR2s,
            });

            logger.LogInformation(
                "Trial {Trial}/{Total}: CV-MAE={Mae:F4}  CV-R²={R2:F4}  " +
                "iter={Iter}  lr={Lr}  leaves={Leaves}  minLeaf={MinLeaf}  L1={L1}  L2={L2}",
                trial, numberOfTrials, cvMae, cvR2,
                hyperparameters.NumberOfIterations,
                hyperparameters.LearningRate,
                hyperparameters.NumberOfLeaves,
                hyperparameters.MinimumExampleCountPerLeaf,
                hyperparameters.L1Regularization,
                hyperparameters.L2Regularization);
        }

        if (trialResults.Count == 0)
        {
            throw new InvalidOperationException(
                "No valid trial results were produced. Verify that the dataset has enough rows per round.");
        }

        var bestTrial = trialResults.MinBy(t => t.CrossValidationMae)!;
        var bestHyperparameters = bestTrial.Hyperparameters;

        // Mark the winning trial and sort by CV-MAE ascending.
        var finalTrials = trialResults
            .Select(t => new HyperparameterTrialResult
            {
                TrialNumber = t.TrialNumber,
                Hyperparameters = t.Hyperparameters,
                CrossValidationMae = t.CrossValidationMae,
                CrossValidationRSquared = t.CrossValidationRSquared,
                FoldMaes = t.FoldMaes,
                FoldRSquareds = t.FoldRSquareds,
                IsBest = t.TrialNumber == bestTrial.TrialNumber,
            })
            .OrderBy(t => t.CrossValidationMae)
            .ToList();

        logger.LogInformation(
            "Search complete. Best CV-MAE={Mae:F4} (trial {Trial}): " +
            "iter={Iter}  lr={Lr}  leaves={Leaves}  minLeaf={MinLeaf}  L1={L1}  L2={L2}",
            bestTrial.CrossValidationMae, bestTrial.TrialNumber,
            bestHyperparameters.NumberOfIterations,
            bestHyperparameters.LearningRate,
            bestHyperparameters.NumberOfLeaves,
            bestHyperparameters.MinimumExampleCountPerLeaf,
            bestHyperparameters.L1Regularization,
            bestHyperparameters.L2Regularization);

        // Optionally retrain the persisted model with the best hyperparameters.
        AthleteScoreModelInfo? retrainedModelInfo = null;
        if (applyBest)
        {
            logger.LogInformation(
                "Retraining the full-context model with the best hyperparameters (applyBest=true).");

            var trainingResult = await trainingService.TrainAsync(
                fromRound: fromRound,
                toRound: toRound,
                hyperparameters: bestHyperparameters,
                cancellationToken: cancellationToken);

            retrainedModelInfo = trainingResult.ModelInfo;

            logger.LogInformation(
                "Final model retrained — R²={R2:F4}  MAE={Mae:F4}.",
                retrainedModelInfo.RSquared,
                retrainedModelInfo.MeanAbsoluteError);
        }

        return new HyperparameterSearchResult
        {
            Message = BuildSummaryMessage(bestTrial, retrainedModelInfo),
            NumberOfTrials = trialResults.Count,
            ValidationFolds = validationFolds,
            BestHyperparameters = bestHyperparameters,
            BestCrossValidationMae = bestTrial.CrossValidationMae,
            BestCrossValidationRSquared = bestTrial.CrossValidationRSquared,
            Trials = finalTrials,
            RetrainedModelInfo = retrainedModelInfo,
        };
    }

    // ── Private helpers ────────────────────────────────────────────────────────────────────────

    private static LightGbmHyperparameters SampleHyperparameters(Random rng) => new()
    {
        NumberOfIterations = IterationsGrid[rng.Next(IterationsGrid.Length)],
        LearningRate = LearningRateGrid[rng.Next(LearningRateGrid.Length)],
        NumberOfLeaves = LeavesGrid[rng.Next(LeavesGrid.Length)],
        MinimumExampleCountPerLeaf = MinLeafGrid[rng.Next(MinLeafGrid.Length)],
        L1Regularization = L1Grid[rng.Next(L1Grid.Length)],
        L2Regularization = L2Grid[rng.Next(L2Grid.Length)],
    };

    /// <summary>
    /// Trains a LightGBM model on <paramref name="trainRows"/> and evaluates it on
    /// <paramref name="holdoutRows"/>, returning (MAE, R²). No PFI or feature selection is applied
    /// inside CV folds so that every trial uses an identical feature space.
    /// </summary>
    private static (double Mae, double R2) EvaluateFold(
        IEnumerable<FullContextAthleteScoreTrainingRow> trainRows,
        IEnumerable<FullContextAthleteScoreTrainingRow> holdoutRows,
        LightGbmHyperparameters hyperparameters)
    {
        var mlContext = new MLContext(seed: 42);

        var trainData = mlContext.Data.LoadFromEnumerable(trainRows);
        var holdoutData = mlContext.Data.LoadFromEnumerable(holdoutRows);

        // Use all numeric features inside CV — consistent across trials, avoids per-fold PFI cost.
        var pipeline = FullContextAthleteScoreModelTrainingService.BuildPipeline(
            mlContext, allowedNumericFeatures: null, hyperparameters);

        var model = pipeline.Fit(trainData);
        var predictions = model.Transform(holdoutData);

        var metrics = mlContext.Regression.Evaluate(
            predictions,
            labelColumnName: nameof(FullContextAthleteScoreTrainingRow.Label),
            scoreColumnName: "Score");

        return (metrics.MeanAbsoluteError, metrics.RSquared);
    }

    private static string BuildSummaryMessage(
        HyperparameterTrialResult bestTrial,
        AthleteScoreModelInfo? retrainedModel)
    {
        var hp = bestTrial.Hyperparameters;
        var hpSummary = $"iter={hp.NumberOfIterations}, lr={hp.LearningRate}, " +
                        $"leaves={hp.NumberOfLeaves}, minLeaf={hp.MinimumExampleCountPerLeaf}, " +
                        $"L1={hp.L1Regularization}, L2={hp.L2Regularization}";

        if (retrainedModel is not null)
        {
            return $"Hyperparameter search complete. Best combination ({hpSummary}) achieved " +
                   $"CV-MAE={bestTrial.CrossValidationMae:F4}. " +
                   $"Final model retrained — MAE={retrainedModel.MeanAbsoluteError:F4}, " +
                   $"R²={retrainedModel.RSquared:F4}. " +
                   $"Update MachineLearning:FullContext:Hyperparameters in appsettings.json to persist these values.";
        }

        return $"Hyperparameter search complete. Best combination ({hpSummary}) achieved " +
               $"CV-MAE={bestTrial.CrossValidationMae:F4} (applyBest was false; model not retrained).";
    }
}
