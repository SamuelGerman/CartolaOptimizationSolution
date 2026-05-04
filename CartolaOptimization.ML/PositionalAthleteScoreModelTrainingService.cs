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
        var validationRoundSet = distinctRounds
            .Skip(distinctRounds.Length - effectiveValidationRounds)
            .ToHashSet();

        var trainingRows = positionRows
            .Where(r => !validationRoundSet.Contains((int)r.RoundId))
            .ToList();
        var holdoutRows = positionRows
            .Where(r => validationRoundSet.Contains((int)r.RoundId))
            .ToList();

        if (trainingRows.Count == 0 || holdoutRows.Count == 0)
        {
            return new PositionalAthleteScoreModelResult
            {
                PositionCartolaId = positionId,
                PositionCode = positionCode,
                PositionName = positionName,
                Trained = false,
                SkipReason = "Insufficient rows after splitting into training and validation sets.",
                TrainingRows = trainingRows.Count,
                ValidationRows = holdoutRows.Count,
            };
        }

        logger.LogInformation(
            "Training positional model for {Code} ({Id}): {TrainRows} training rows, {HoldoutRows} holdout rows, {Rounds} distinct round(s).",
            positionCode, positionId, trainingRows.Count, holdoutRows.Count, distinctRounds.Length);

        var mlContext = new MLContext(seed: 42);
        var trainData = mlContext.Data.LoadFromEnumerable(trainingRows);
        var holdoutData = mlContext.Data.LoadFromEnumerable(holdoutRows);

        var allNumericFeatureNames = FullContextAthleteScoreModelTrainingService
            .GetNumericFeatureNames(allowedFeatures: null);

        // Phase 1: train on all features so PFI can rank them.
        var fullPipeline = FullContextAthleteScoreModelTrainingService
            .BuildPipeline(mlContext, allowedNumericFeatures: null, hyperparameters);
        var fullModel = fullPipeline.Fit(trainData);

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

        // Phase 2 (optional): retrain with top-N numeric features.
        ITransformer model = fullModel;
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
                var filteredPipeline = FullContextAthleteScoreModelTrainingService
                    .BuildPipeline(mlContext, allowedNumericFeatures, hyperparameters);
                model = filteredPipeline.Fit(trainData);

                logger.LogInformation(
                    "Position {Code}: retrained with top {Selected}/{Total} numeric features.",
                    positionCode, selectedNumericFeatures.Count, allNumericFeatureNames.Length);
            }
        }

        var predictions = model.Transform(holdoutData);
        var metrics = mlContext.Regression.Evaluate(
            predictions,
            labelColumnName: nameof(FullContextAthleteScoreTrainingRow.Label),
            scoreColumnName: "Score");

        var holdoutScores = mlContext.Data
            .CreateEnumerable<AthleteScorePredictionOutput>(predictions, reuseRowObject: false)
            .Select(item => item.Score)
            .ToList();

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

        var positionDiagnostics = FullContextAthleteScoreModelTrainingService
            .BuildPositionDiagnostics(trainingRows, holdoutRows, holdoutScores);

        var modelVersion = $"athlete-score-v1-positional-{positionCode.ToLowerInvariant()}";
        var datasetKey = $"positional-{positionCode.ToLowerInvariant()}";

        Directory.CreateDirectory(Path.GetDirectoryName(artifactStore.GetModelPath(positionId))!);
        mlContext.Model.Save(model, trainData.Schema, artifactStore.GetModelPath(positionId));

        var metadata = new AthleteScoreModelArtifactMetadata
        {
            ModelVersion = modelVersion,
            DatasetKey = datasetKey,
            TrainerName = TrainerName,
            TrainedAtUtc = DateTime.UtcNow,
            FromRound = fromRound,
            ToRound = toRound,
            ValidationRounds = effectiveValidationRounds,
            TrainingRows = trainingRows.Count,
            ValidationRows = holdoutRows.Count,
            TrainingDistinctRounds = trainingRows.Select(r => r.RoundId).Distinct().Count(),
            ValidationDistinctRounds = holdoutRows.Select(r => r.RoundId).Distinct().Count(),
            MeanAbsoluteError = metrics.MeanAbsoluteError,
            RootMeanSquaredError = metrics.RootMeanSquaredError,
            RSquared = metrics.RSquared,
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
            "Position {Code} ({Id}) model trained — R²={R2:F4}, MAE={Mae:F4}.",
            positionCode, positionId, metrics.RSquared, metrics.MeanAbsoluteError);

        return new PositionalAthleteScoreModelResult
        {
            PositionCartolaId = positionId,
            PositionCode = positionCode,
            PositionName = positionName,
            Trained = true,
            TrainingRows = trainingRows.Count,
            ValidationRows = holdoutRows.Count,
            ModelInfo = artifactStore.ToModelInfo(metadata),
        };
    }
}
