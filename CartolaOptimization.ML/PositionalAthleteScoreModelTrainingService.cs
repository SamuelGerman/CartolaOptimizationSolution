using CartolaOptimization.Application.Interfaces;
using CartolaOptimization.Application.Models;
using CartolaOptimization.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ML;

namespace CartolaOptimization.ML;

/// <summary>
/// Trains one LightGBM model per Cartola position (GOL, LAT, ZAG, MEI, ATA, TEC) using the same
/// full-context feature set as the global model, but restricted to athletes of each position.
/// Position-specific models learn the particular scoring patterns of each role, reducing the
/// noise introduced by cross-position variance in the shared global model.
/// </summary>
public sealed class PositionalAthleteScoreModelTrainingService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    PositionalAthleteScoreModelArtifactStore artifactStore,
    IOptions<PositionalAthleteScoreModelOptions> options,
    ILogger<PositionalAthleteScoreModelTrainingService> logger)
    : IPositionalAthleteScoreModelTrainingService
{
    private const string TrainerName = "LightGBM";
    private readonly PositionalAthleteScoreModelOptions options = options.Value;

    private static readonly IReadOnlyDictionary<int, (string Code, string Name)> PositionLabels =
        new Dictionary<int, (string, string)>
        {
            [1] = ("GOL", "Goleiro"),
            [2] = ("LAT", "Lateral"),
            [3] = ("ZAG", "Zagueiro"),
            [4] = ("MEI", "Meia"),
            [5] = ("ATA", "Atacante"),
            [6] = ("TEC", "Tecnico"),
        };

    public async Task<PositionalAthleteScoreTrainingResult> TrainAllAsync(
        int? fromRound = null,
        int? toRound = null,
        int validationRounds = 1,
        CancellationToken cancellationToken = default)
    {
        validationRounds = Math.Max(1, validationRounds);
        var hyperparameters = options.Hyperparameters;
        var topNFeatures = options.DefaultTopNFeatures;

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        // Load the full dataset once; filter per position in memory to avoid N database queries.
        var allRows = await FullContextAthleteScoreModelTrainingService.QueryTrainingRowsAsync(
            dbContext, fromRound, toRound, cancellationToken);

        logger.LogInformation(
            "Positional training: {TotalRows} full-context rows available across all positions.",
            allRows.Count);

        var positionResults = new List<PositionalAthleteScoreModelResult>();

        foreach (var positionId in artifactStore.SupportedPositionIds.OrderBy(id => id))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (code, name) = PositionLabels.TryGetValue(positionId, out var labels)
                ? labels
                : ($"P{positionId}", $"Posicao {positionId}");

            var result = await TrainPositionAsync(
                positionId, code, name,
                allRows, fromRound, toRound,
                validationRounds, topNFeatures, hyperparameters,
                cancellationToken);

            positionResults.Add(result);
        }

        var trainedCount = positionResults.Count(r => r.Trained);
        var skippedCount = positionResults.Count(r => !r.Trained);

        return new PositionalAthleteScoreTrainingResult
        {
            Message = $"Positional model training complete: {trainedCount} position(s) trained, {skippedCount} skipped due to insufficient data.",
            FromRound = fromRound,
            ToRound = toRound,
            ValidationRounds = validationRounds,
            TrainedPositions = trainedCount,
            SkippedPositions = skippedCount,
            Positions = positionResults,
        };
    }

    public async Task<IReadOnlyList<AthleteScoreModelInfo>> GetAllModelInfosAsync(
        CancellationToken cancellationToken = default)
    {
        var infos = new List<AthleteScoreModelInfo>();

        foreach (var positionId in artifactStore.SupportedPositionIds.OrderBy(id => id))
        {
            var metadata = await artifactStore.LoadMetadataAsync(positionId, cancellationToken);
            if (metadata is not null)
            {
                infos.Add(artifactStore.ToModelInfo(metadata));
            }
        }

        return infos;
    }

    // ── Private helpers ────────────────────────────────────────────────────────────────────────

    private async Task<PositionalAthleteScoreModelResult> TrainPositionAsync(
        int positionId,
        string positionCode,
        string positionName,
        IReadOnlyList<FullContextAthleteScoreTrainingRow> allRows,
        int? fromRound,
        int? toRound,
        int validationRounds,
        int? topNFeatures,
        LightGbmHyperparameters hyperparameters,
        CancellationToken cancellationToken)
    {
        var positionRows = allRows
            .Where(r => (int)r.PositionCartolaId == positionId)
            .ToList();

        if (positionRows.Count < options.MinimumTrainingRows)
        {
            logger.LogWarning(
                "Position {Code} ({Id}): only {Rows} rows available, minimum required is {Min}. Skipping.",
                positionCode, positionId, positionRows.Count, options.MinimumTrainingRows);

            return new PositionalAthleteScoreModelResult
            {
                PositionCartolaId = positionId,
                PositionCode = positionCode,
                PositionName = positionName,
                Trained = false,
                SkipReason = $"Only {positionRows.Count} row(s) available; minimum required is {options.MinimumTrainingRows}.",
                TrainingRows = positionRows.Count,
            };
        }

        var distinctRounds = positionRows
            .Select(r => (int)r.RoundId)
            .Distinct()
            .OrderBy(r => r)
            .ToArray();

        if (distinctRounds.Length < 2)
        {
            logger.LogWarning(
                "Position {Code} ({Id}): only {Rounds} distinct round(s), need at least 2. Skipping.",
                positionCode, positionId, distinctRounds.Length);

            return new PositionalAthleteScoreModelResult
            {
                PositionCartolaId = positionId,
                PositionCode = positionCode,
                PositionName = positionName,
                Trained = false,
                SkipReason = $"Only {distinctRounds.Length} distinct round(s) available; at least 2 are required for temporal training and validation.",
                TrainingRows = positionRows.Count,
            };
        }

        var effectiveValidationRounds = Math.Min(validationRounds, distinctRounds.Length - 1);

        // The holdout window starts at this index into distinctRounds (ascending order).
        var holdoutStartIdx = distinctRounds.Length - effectiveValidationRounds;
        var holdoutStartRound = distinctRounds[holdoutStartIdx];

        // Feature-selection split: train on everything before the holdout window.
        var featureSelectionRows = positionRows
            .Where(r => (int)r.RoundId < holdoutStartRound)
            .ToList();
        var holdoutRows = positionRows
            .Where(r => (int)r.RoundId >= holdoutStartRound)
            .ToList();

        if (featureSelectionRows.Count == 0 || holdoutRows.Count == 0)
        {
            return new PositionalAthleteScoreModelResult
            {
                PositionCartolaId = positionId,
                PositionCode = positionCode,
                PositionName = positionName,
                Trained = false,
                SkipReason = "Insufficient rows after splitting into training and validation sets.",
                TrainingRows = featureSelectionRows.Count,
                ValidationRows = holdoutRows.Count,
            };
        }

        logger.LogInformation(
            "Training positional model for {Code} ({Id}): {TotalRows} total rows, {Rounds} distinct round(s), {ValRounds} walk-forward fold(s).",
            positionCode, positionId, positionRows.Count, distinctRounds.Length, effectiveValidationRounds);

        var mlContext = new MLContext(seed: 42);
        var featureSelectionData = mlContext.Data.LoadFromEnumerable(featureSelectionRows);

        var allNumericFeatureNames = FullContextAthleteScoreModelTrainingService
            .GetNumericFeatureNames(allowedFeatures: null);

        // Phase 1: train on all features so PFI can rank them.
        var fullPipeline = FullContextAthleteScoreModelTrainingService
            .BuildPipeline(mlContext, allowedNumericFeatures: null, hyperparameters);
        var fullModel = fullPipeline.Fit(featureSelectionData);

        List<(string FeatureName, double MeanRSquaredDrop, double RSquaredDropStdDev, bool IsNumeric)> importanceRanking;
        try
        {
            importanceRanking = FullContextAthleteScoreModelTrainingService
                .ComputeFeatureImportances(mlContext, fullModel, holdoutRows, allNumericFeatureNames);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "PFI failed for position {Code}: {Message}. Falling back to all features.",
                positionCode, ex.Message);
            importanceRanking = [];
        }

        // Phase 2 (optional): select the top-N numeric features from the PFI ranking.
        IReadOnlySet<string>? allowedNumericFeatures = null;

        if (topNFeatures.HasValue && importanceRanking.Count > 0)
        {
            var selectedNumericFeatures = importanceRanking
                .Where(item => item.IsNumeric)
                .Take(topNFeatures.Value)
                .Select(item => item.FeatureName)
                .ToHashSet(StringComparer.Ordinal);

            if (selectedNumericFeatures.Count > 0)
            {
                allowedNumericFeatures = selectedNumericFeatures;
                logger.LogInformation(
                    "Position {Code}: selected top {Selected}/{Total} numeric features.",
                    positionCode, selectedNumericFeatures.Count, allNumericFeatureNames.Length);
            }
        }

        // Phase 3: walk-forward (expanding-window) cross-validation.
        var cvMaes = new List<double>(effectiveValidationRounds);
        var cvRmses = new List<double>(effectiveValidationRounds);
        var cvR2s = new List<double>(effectiveValidationRounds);
        var cvTotalValidationRows = 0;

        List<FullContextAthleteScoreTrainingRow>? lastFoldTrainingRows = null;
        List<FullContextAthleteScoreTrainingRow>? lastFoldHoldoutRows = null;
        List<float>? lastFoldScores = null;

        for (var fold = 0; fold < effectiveValidationRounds; fold++)
        {
            var foldValidationRound = distinctRounds[holdoutStartIdx + fold];
            var foldTrainingRows = positionRows.Where(r => (int)r.RoundId < foldValidationRound).ToList();
            var foldHoldoutRows = positionRows.Where(r => (int)r.RoundId == foldValidationRound).ToList();

            if (foldTrainingRows.Count == 0 || foldHoldoutRows.Count == 0)
            {
                logger.LogWarning(
                    "Position {Code} fold {Fold}/{Total}: skipping (empty set).",
                    positionCode, fold + 1, effectiveValidationRounds);
                continue;
            }

            var foldPipeline = FullContextAthleteScoreModelTrainingService
                .BuildPipeline(mlContext, allowedNumericFeatures, hyperparameters);
            var foldTrainData = mlContext.Data.LoadFromEnumerable(foldTrainingRows);
            var foldModel = foldPipeline.Fit(foldTrainData);

            var foldHoldoutData = mlContext.Data.LoadFromEnumerable(foldHoldoutRows);
            var foldPredictions = foldModel.Transform(foldHoldoutData);
            var foldMetrics = mlContext.Regression.Evaluate(
                foldPredictions,
                labelColumnName: nameof(FullContextAthleteScoreTrainingRow.Label),
                scoreColumnName: "Score");

            cvMaes.Add(foldMetrics.MeanAbsoluteError);
            cvRmses.Add(foldMetrics.RootMeanSquaredError);
            cvR2s.Add(foldMetrics.RSquared);
            cvTotalValidationRows += foldHoldoutRows.Count;

            lastFoldTrainingRows = foldTrainingRows;
            lastFoldHoldoutRows = foldHoldoutRows;
            lastFoldScores = mlContext.Data
                .CreateEnumerable<AthleteScorePredictionOutput>(foldPredictions, reuseRowObject: false)
                .Select(item => item.Score)
                .ToList();
        }

        var cvMae = cvMaes.Count > 0 ? cvMaes.Average() : 0d;
        var cvRmse = cvRmses.Count > 0 ? cvRmses.Average() : 0d;
        var cvR2 = cvR2s.Count > 0 ? cvR2s.Average() : 0d;

        // Phase 4: train the persisted model on ALL position rows with the selected features.
        var allPositionData = mlContext.Data.LoadFromEnumerable(positionRows);
        var finalPipeline = FullContextAthleteScoreModelTrainingService
            .BuildPipeline(mlContext, allowedNumericFeatures, hyperparameters);
        var finalModel = finalPipeline.Fit(allPositionData);

        // Calibration diagnostics come from the last (most recent) CV fold.
        var positionDiagnostics = lastFoldTrainingRows is not null && lastFoldHoldoutRows is not null && lastFoldScores is not null
            ? FullContextAthleteScoreModelTrainingService.BuildPositionDiagnostics(lastFoldTrainingRows, lastFoldHoldoutRows, lastFoldScores)
            : [];

        // Build feature importance list with selected flags.
        var activeNumericFeatureSet = allowedNumericFeatures
            ?? new HashSet<string>(allNumericFeatureNames, StringComparer.Ordinal);

        var featureImportances = importanceRanking
            .Select((item, idx) => new AthleteScoreFeatureImportance
            {
                FeatureName = item.FeatureName,
                MeanRSquaredDrop = item.MeanRSquaredDrop,
                RSquaredDropStdDev = item.RSquaredDropStdDev,
                Rank = idx + 1,
                IsSelected = !item.IsNumeric || activeNumericFeatureSet.Contains(item.FeatureName),
            })
            .ToList();

        var modelVersion = $"athlete-score-v1-positional-{positionCode.ToLowerInvariant()}";
        var datasetKey = $"positional-{positionCode.ToLowerInvariant()}";

        Directory.CreateDirectory(Path.GetDirectoryName(artifactStore.GetModelPath(positionId))!);
        mlContext.Model.Save(finalModel, allPositionData.Schema, artifactStore.GetModelPath(positionId));

        var metadata = new AthleteScoreModelArtifactMetadata
        {
            ModelVersion = modelVersion,
            DatasetKey = datasetKey,
            TrainerName = TrainerName,
            TrainedAtUtc = DateTime.UtcNow,
            FromRound = fromRound,
            ToRound = toRound,
            ValidationRounds = effectiveValidationRounds,
            TrainingRows = positionRows.Count,                          // final model trains on all position rows
            ValidationRows = cvTotalValidationRows,                     // total rows across all CV folds
            TrainingDistinctRounds = distinctRounds.Length,             // all distinct rounds
            ValidationDistinctRounds = effectiveValidationRounds,       // one validation round per fold
            MeanAbsoluteError = cvMae,
            RootMeanSquaredError = cvRmse,
            RSquared = cvR2,
            ModelArtifactPath = artifactStore.GetModelPath(positionId),
            MetadataArtifactPath = artifactStore.GetMetadataPath(positionId),
            PositionDiagnostics = positionDiagnostics,
            TotalNumericFeatureCount = allNumericFeatureNames.Length,
            ActiveNumericFeatureCount = allowedNumericFeatures?.Count ?? allNumericFeatureNames.Length,
            FeatureImportances = featureImportances,
            Hyperparameters = hyperparameters,
        };

        await artifactStore.SaveMetadataAsync(positionId, metadata, cancellationToken);

        logger.LogInformation(
            "Position {Code} ({Id}) model trained — CV R²={R2:F4}, CV MAE={Mae:F4} ({Folds} fold(s)).",
            positionCode, positionId, cvR2, cvMae, cvMaes.Count);

        return new PositionalAthleteScoreModelResult
        {
            PositionCartolaId = positionId,
            PositionCode = positionCode,
            PositionName = positionName,
            Trained = true,
            TrainingRows = positionRows.Count,
            ValidationRows = cvTotalValidationRows,
            ModelInfo = artifactStore.ToModelInfo(metadata),
        };
    }
}
