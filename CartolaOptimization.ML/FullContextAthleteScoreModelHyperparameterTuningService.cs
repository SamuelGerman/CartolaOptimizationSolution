using CartolaOptimization.Application.Interfaces;
using CartolaOptimization.Application.Models;
using CartolaOptimization.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ML;

namespace CartolaOptimization.ML;

/// <summary>
/// Realiza otimização de hiperparâmetros por busca aleatória para o modelo LightGBM full-context usando
/// temporal walk-forward cross-validation.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Search strategy — random search</strong>: randomly samples hyperparameter combinations
/// a partir de grades discretas de candidatos. A busca aleatória encontra configurações quase ótimas mais rápido que
/// a busca em grade em espaços de alta dimensão porque cobre mais do panorama de parâmetros por
/// trial (Bergstra &amp; Bengio, 2012).
/// </para>
/// <para>
/// <strong>Estratégia de validação — CV walk-forward</strong>: para <em>k</em> folds, as últimas <em>k</em>
/// rodadas distintas são usadas como holdouts sucessivos, a cada vez expandindo a janela de treino.
/// Isto preserva a ordenação temporal dos dados e evita viés de look-ahead.
/// </para>
/// <para>
/// <strong>Seleção de features durante a CV</strong>: todas as features numéricas disponíveis (sem filtro de PFI)
/// são usadas dentro de cada fold da CV para que cada trial seja avaliado em um espaço de features consistente.
/// Aplicar PFI por fold dobraria o tempo de treino e introduziria seleção específica de cada fold
/// viés. O modelo final retreinado com <c>applyBest=true</c> usa a
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
    // Valores escolhidos para cobrir uma ampla faixa mantendo o tempo total de busca razoável.
    // As contagens de iterações são limitadas pelo tamanho dos dados disponíveis; os valores de regularização abrangem duas ordens de grandeza.
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

        // É preciso pelo menos 2 rodadas para treino + 1 por fold.
        var minRoundsRequired = validationFolds + 2;
        if (distinctRounds.Length < minRoundsRequired)
        {
            throw new InvalidOperationException(
                $"Walk-forward cross-validation with {validationFolds} fold(s) requires at least " +
                $"{minRoundsRequired} distinct rounds, but only {distinctRounds.Length} are available " +
                $"in the selected window.");
        }

        // Limita os folds para que sempre haja ≥2 rodadas de treino no primeiro fold.
        validationFolds = Math.Min(validationFolds, distinctRounds.Length - 2);

        // Monta as definições dos folds walk-forward: cada fold adiciona mais uma rodada ao conjunto de holdout
        // tiradas da cauda, e o restante forma a janela de treino.
        var foldHoldoutRounds = Enumerable.Range(0, validationFolds)
            .Select(foldIndex => distinctRounds[distinctRounds.Length - validationFolds + foldIndex])
            .ToArray();

        logger.LogInformation(
            "Starting hyperparameter search: {Trials} trials × {Folds} walk-forward folds. " +
            "Holdout rounds: [{HoldoutRounds}].",
            numberOfTrials, validationFolds, string.Join(", ", foldHoldoutRounds));

        // Pré-agrupa as linhas por rodada para uma divisão rápida em folds.
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
                // Os folds acumulam: o fold k usa todas as rodadas até a (k+1)-ésima rodada de holdout.
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

        // Marca o trial vencedor e ordena por CV-MAE crescente.
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

        // Opcionalmente retreina o modelo persistido com os melhores hiperparâmetros.
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
    /// Treina um modelo LightGBM em <paramref name="trainRows"/> e o avalia em
    /// <paramref name="holdoutRows"/>, retornando (MAE, R²). Nenhum PFI ou seleção de features é aplicado
    /// dentro dos folds da CV para que cada trial use um espaço de features idêntico.
    /// </summary>
    private static (double Mae, double R2) EvaluateFold(
        IEnumerable<FullContextAthleteScoreTrainingRow> trainRows,
        IEnumerable<FullContextAthleteScoreTrainingRow> holdoutRows,
        LightGbmHyperparameters hyperparameters)
    {
        var mlContext = new MLContext(seed: 42);

        var trainData = mlContext.Data.LoadFromEnumerable(trainRows);
        var holdoutData = mlContext.Data.LoadFromEnumerable(holdoutRows);

        // Usa todas as features numéricas dentro da CV — consistente entre os trials, evita o custo de PFI por fold.
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
