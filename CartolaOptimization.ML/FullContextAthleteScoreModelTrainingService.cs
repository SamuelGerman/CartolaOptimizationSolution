using System.Reflection;
using CartolaOptimization.Application.Interfaces;
using CartolaOptimization.Application.Models;
using CartolaOptimization.Domain.Constants;
using CartolaOptimization.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.ML;
using Microsoft.ML.Trainers.LightGbm;

namespace CartolaOptimization.ML;

public sealed class FullContextAthleteScoreModelTrainingService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    FullContextAthleteScoreModelArtifactStore artifactStore,
    Microsoft.Extensions.Options.IOptions<FullContextAthleteScoreModelOptions> options,
    ILogger<FullContextAthleteScoreModelTrainingService> logger) : IFullContextAthleteScoreModelTrainingService
{
    private const string DatasetKey = "full-context";
    private const string TrainerName = "LightGBM";
    private readonly FullContextAthleteScoreModelOptions options = options.Value;

    /// <summary>
    /// One-hot encoded column names. Their slots in the concatenated Features vector are identified
    /// by prefix (e.g. "ClubEncoded.0", "ClubEncoded.1", …) and are always included in the model
    /// regardless of the <paramref name="topNFeatures"/> cutoff.
    /// </summary>
    private static readonly HashSet<string> EncodedPrefixes = new(StringComparer.Ordinal)
    {
        "ClubEncoded",
        "OpponentClubEncoded",
        "PositionEncoded",
        "StatusEncoded",
    };

    public async Task<AthleteScoreModelTrainingResult> TrainAsync(
        int? fromRound = null,
        int? toRound = null,
        int validationRounds = 1,
        int? topNFeatures = null,
        CancellationToken cancellationToken = default)
    {
        NormalizeOptionalRange(ref fromRound, ref toRound);
        validationRounds = Math.Max(1, validationRounds);

        // Fall back to the configured default when the caller does not supply an explicit value.
        topNFeatures ??= options.DefaultTopNFeatures;

        if (topNFeatures.HasValue)
        {
            topNFeatures = Math.Max(1, topNFeatures.Value);
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var rows = await QueryTrainingRowsAsync(dbContext, fromRound, toRound, cancellationToken);
        var distinctRounds = rows
            .Select(item => item.RoundId)
            .Distinct()
            .OrderBy(item => item)
            .ToArray();

        if (distinctRounds.Length < 2)
        {
            throw new InvalidOperationException("At least two distinct historical closed rounds with valid local full-context derived from athlete and match history are required to train and validate the v1 full-context athlete-score model.");
        }

        if (validationRounds >= distinctRounds.Length)
        {
            validationRounds = distinctRounds.Length - 1;
        }

        var validationRoundSet = distinctRounds
            .Skip(distinctRounds.Length - validationRounds)
            .ToHashSet();

        var trainingRows = rows
            .Where(item => !validationRoundSet.Contains((int)item.RoundId))
            .ToList();
        var holdoutRows = rows
            .Where(item => validationRoundSet.Contains((int)item.RoundId))
            .ToList();

        if (trainingRows.Count == 0 || holdoutRows.Count == 0)
        {
            throw new InvalidOperationException("The selected round window does not leave enough rows for temporal training and validation of the full-context model.");
        }

        var mlContext = new MLContext(seed: 42);
        var trainData = mlContext.Data.LoadFromEnumerable(trainingRows);
        var holdoutData = mlContext.Data.LoadFromEnumerable(holdoutRows);

        var allNumericFeatureNames = GetNumericFeatureNames(allowedFeatures: null);

        // Phase 1: train on all features so PFI evaluates relative importance on the full model.
        var fullPipeline = BuildPipeline(mlContext, allowedNumericFeatures: null);
        var fullModel = fullPipeline.Fit(trainData);

        // Compute permutation feature importance by manually shuffling each feature and measuring R² drop.
        // This avoids the ML.NET PFI API which requires a specific transformer type.
        List<(string FeatureName, double MeanRSquaredDrop, double RSquaredDropStdDev, bool IsNumeric)> importanceRanking;
        try
        {
            importanceRanking = ComputeFeatureImportances(mlContext, fullModel, holdoutRows, allNumericFeatureNames);
            logger.LogInformation("Permutation feature importance computed for {FeatureCount} feature groups.", importanceRanking.Count);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Permutation feature importance computation failed ({ExceptionType}: {Message}). " +
                "Training will complete without feature importance data.",
                ex.GetType().Name, ex.Message);
            importanceRanking = [];
        }

        // Phase 2 (optional): retrain using only the top-N numeric features when requested.
        ITransformer model = fullModel; // default: use the full model trained in phase 1
        IReadOnlySet<string>? allowedNumericFeatures = null;

        if (topNFeatures.HasValue)
        {
            if (importanceRanking.Count == 0)
            {
                logger.LogWarning(
                    "topNFeatures={TopNFeatures} was requested but the feature importance ranking is empty. " +
                    "Falling back to all {Count} numeric features.",
                    topNFeatures.Value, allNumericFeatureNames.Length);
            }
            else
            {
                var selectedNumericFeatures = importanceRanking
                    .Where(item => item.IsNumeric)
                    .Take(topNFeatures.Value)
                    .Select(item => item.FeatureName)
                    .ToHashSet(StringComparer.Ordinal);

                if (selectedNumericFeatures.Count > 0)
                {
                    allowedNumericFeatures = selectedNumericFeatures;
                    var filteredPipeline = BuildPipeline(mlContext, allowedNumericFeatures);
                    model = filteredPipeline.Fit(trainData);
                    logger.LogInformation(
                        "Retrained with top {Selected}/{Total} numeric features (topNFeatures={TopN}).",
                        selectedNumericFeatures.Count, allNumericFeatureNames.Length, topNFeatures.Value);
                }
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
        var positionDiagnostics = BuildPositionDiagnostics(trainingRows, holdoutRows, holdoutScores);

        // Build the final feature importance list, tagging which features were kept in the model.
        var activeNumericFeatureSet = allowedNumericFeatures
            ?? new HashSet<string>(allNumericFeatureNames, StringComparer.Ordinal);

        var featureImportances = importanceRanking
            .Select((item, idx) => new AthleteScoreFeatureImportance
            {
                FeatureName = item.FeatureName,
                MeanRSquaredDrop = item.MeanRSquaredDrop,
                RSquaredDropStdDev = item.RSquaredDropStdDev,
                Rank = idx + 1,
                // Categorical encoded groups are always selected; numeric features only when in the top-N set.
                IsSelected = !item.IsNumeric || activeNumericFeatureSet.Contains(item.FeatureName),
            })
            .ToList();

        Directory.CreateDirectory(Path.GetDirectoryName(artifactStore.GetModelPath())!);
        mlContext.Model.Save(model, trainData.Schema, artifactStore.GetModelPath());

        var metadata = new AthleteScoreModelArtifactMetadata
        {
            ModelVersion = options.ModelVersion,
            DatasetKey = DatasetKey,
            TrainerName = TrainerName,
            TrainedAtUtc = DateTime.UtcNow,
            FromRound = fromRound,
            ToRound = toRound,
            ValidationRounds = validationRounds,
            TrainingRows = trainingRows.Count,
            ValidationRows = holdoutRows.Count,
            TrainingDistinctRounds = trainingRows.Select(item => item.RoundId).Distinct().Count(),
            ValidationDistinctRounds = holdoutRows.Select(item => item.RoundId).Distinct().Count(),
            MeanAbsoluteError = metrics.MeanAbsoluteError,
            RootMeanSquaredError = metrics.RootMeanSquaredError,
            RSquared = metrics.RSquared,
            ModelArtifactPath = artifactStore.GetModelPath(),
            MetadataArtifactPath = artifactStore.GetMetadataPath(),
            PositionDiagnostics = positionDiagnostics,
            TotalNumericFeatureCount = allNumericFeatureNames.Length,
            ActiveNumericFeatureCount = allowedNumericFeatures?.Count ?? allNumericFeatureNames.Length,
            FeatureImportances = featureImportances,
        };

        await artifactStore.SaveMetadataAsync(metadata, cancellationToken);

        return new AthleteScoreModelTrainingResult
        {
            Message = $"Model '{options.ModelVersion}' trained successfully with the {TrainerName} regressor and temporal validation across the last {validationRounds} round(s).",
            ModelInfo = artifactStore.ToModelInfo(metadata),
        };
    }

    public async Task<AthleteScoreModelInfo?> GetCurrentModelInfoAsync(CancellationToken cancellationToken = default)
    {
        var metadata = await artifactStore.LoadMetadataAsync(cancellationToken);
        return metadata is null ? null : artifactStore.ToModelInfo(metadata);
    }

    /// <summary>
    /// Returns the names of numeric float feature columns, optionally restricted to an allowed set.
    /// The five ID/label columns are always excluded.
    /// </summary>
    private static string[] GetNumericFeatureNames(IReadOnlySet<string>? allowedFeatures)
    {
        var excluded = new HashSet<string>(StringComparer.Ordinal)
        {
            nameof(FullContextAthleteScoreTrainingRow.Label),
            nameof(FullContextAthleteScoreTrainingRow.ClubCartolaId),
            nameof(FullContextAthleteScoreTrainingRow.OpponentClubCartolaId),
            nameof(FullContextAthleteScoreTrainingRow.PositionCartolaId),
            nameof(FullContextAthleteScoreTrainingRow.StatusCartolaId),
        };

        var all = typeof(FullContextAthleteScoreTrainingRow)
            .GetProperties()
            .Where(prop => prop.PropertyType == typeof(float) && !excluded.Contains(prop.Name))
            .Select(prop => prop.Name)
            .ToArray();

        return allowedFeatures is null
            ? all
            : all.Where(name => allowedFeatures.Contains(name)).ToArray();
    }

    private static IEstimator<ITransformer> BuildPipeline(
        MLContext mlContext,
        IReadOnlySet<string>? allowedNumericFeatures)
    {
        var numericFeatures = GetNumericFeatureNames(allowedNumericFeatures);

        return mlContext.Transforms.Categorical.OneHotEncoding(new[]
            {
                new InputOutputColumnPair("ClubEncoded", nameof(FullContextAthleteScoreTrainingRow.ClubCartolaId)),
                new InputOutputColumnPair("OpponentClubEncoded", nameof(FullContextAthleteScoreTrainingRow.OpponentClubCartolaId)),
                new InputOutputColumnPair("PositionEncoded", nameof(FullContextAthleteScoreTrainingRow.PositionCartolaId)),
                new InputOutputColumnPair("StatusEncoded", nameof(FullContextAthleteScoreTrainingRow.StatusCartolaId)),
            })
            .Append(mlContext.Transforms.Concatenate(
                "Features",
                numericFeatures.Concat(
                    [
                        "ClubEncoded",
                        "OpponentClubEncoded",
                        "PositionEncoded",
                        "StatusEncoded",
                    ]).ToArray()))
            .Append(mlContext.Regression.Trainers.LightGbm(new LightGbmRegressionTrainer.Options
            {
                LabelColumnName = nameof(FullContextAthleteScoreTrainingRow.Label),
                FeatureColumnName = "Features",
                NumberOfIterations = 300,
                LearningRate = 0.05,
                NumberOfLeaves = 63,
                MinimumExampleCountPerLeaf = 10,
            }));
    }

    /// <summary>
    /// Computes permutation feature importance by shuffling each feature's values in a holdout sample
    /// and measuring the resulting drop in R². Works with any <see cref="ITransformer"/> without
    /// requiring ML.NET's internal PFI API, which imposes type constraints on the model.
    /// </summary>
    /// <remarks>
    /// Algorithm: for each feature, randomly permute its values across sample rows, re-score with the
    /// full model and measure R² change (permuted − baseline). A more negative delta means the feature
    /// was important: scrambling it hurt the model more.
    /// Categorical features (club, opponent, position, status) are permuted by shuffling their raw
    /// CartolaId column so the one-hot encoding is reshuffled consistently.
    /// </remarks>
    private static List<(string FeatureName, double MeanRSquaredDrop, double RSquaredDropStdDev, bool IsNumeric)>
        ComputeFeatureImportances(
            MLContext mlContext,
            ITransformer model,
            IReadOnlyList<FullContextAthleteScoreTrainingRow> holdoutRows,
            string[] numericFeatureNames)
    {
        if (holdoutRows.Count == 0)
        {
            return [];
        }

        // Cap the sample size so PFI completes in seconds rather than minutes.
        var rng = new Random(42);
        var sample = holdoutRows.Count > 200
            ? holdoutRows.OrderBy(_ => rng.Next()).Take(200).ToList()
            : holdoutRows.ToList();

        // Pre-cache PropertyInfo objects — one reflection lookup per feature name, not per row.
        var propByName = typeof(FullContextAthleteScoreTrainingRow)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .ToDictionary(p => p.Name, StringComparer.Ordinal);

        // MemberwiseClone is protected on System.Object; call it via reflection to clone immutable rows.
        var cloneMethod = typeof(object)
            .GetMethod("MemberwiseClone", BindingFlags.NonPublic | BindingFlags.Instance)!;

        // Baseline R² on the unmodified sample.
        var baselineR2 = EvaluateR2(mlContext, model, sample);

        var results = new List<(string FeatureName, double MeanRSquaredDrop, double RSquaredDropStdDev, bool IsNumeric)>(numericFeatureNames.Length + 4);

        // ── Numeric features ──────────────────────────────────────────────────────────────────────
        foreach (var featureName in numericFeatureNames)
        {
            var prop = propByName[featureName];

            // Single permutation run (sufficient for relative ranking with the current data volume).
            var values = sample.Select(r => (float)prop.GetValue(r)!).ToArray();
            FisherYatesShuffle(values, rng);

            var permuted = new List<FullContextAthleteScoreTrainingRow>(sample.Count);
            for (var i = 0; i < sample.Count; i++)
            {
                var clone = (FullContextAthleteScoreTrainingRow)cloneMethod.Invoke(sample[i], null)!;
                prop.SetValue(clone, values[i]);
                permuted.Add(clone);
            }

            var permutedR2 = EvaluateR2(mlContext, model, permuted);
            var r2Drop = permutedR2 - baselineR2; // negative → feature was important
            results.Add((featureName, r2Drop, 0d, IsNumeric: true));
        }

        // ── Categorical encoded groups ─────────────────────────────────────────────────────────────
        // Permute the raw CartolaId column; the pipeline's OneHotEncoding reshuffles the encoding
        // consistently, so the measured drop reflects the true importance of the categorical group.
        var categoricalItems = new[]
        {
            ("ClubEncoded", nameof(FullContextAthleteScoreTrainingRow.ClubCartolaId)),
            ("OpponentClubEncoded", nameof(FullContextAthleteScoreTrainingRow.OpponentClubCartolaId)),
            ("PositionEncoded", nameof(FullContextAthleteScoreTrainingRow.PositionCartolaId)),
            ("StatusEncoded", nameof(FullContextAthleteScoreTrainingRow.StatusCartolaId)),
        };

        foreach (var (groupName, rawColName) in categoricalItems)
        {
            var prop = propByName[rawColName];
            var values = sample.Select(r => (float)prop.GetValue(r)!).ToArray();
            FisherYatesShuffle(values, rng);

            var permuted = new List<FullContextAthleteScoreTrainingRow>(sample.Count);
            for (var i = 0; i < sample.Count; i++)
            {
                var clone = (FullContextAthleteScoreTrainingRow)cloneMethod.Invoke(sample[i], null)!;
                prop.SetValue(clone, values[i]);
                permuted.Add(clone);
            }

            var permutedR2 = EvaluateR2(mlContext, model, permuted);
            results.Add((groupName, permutedR2 - baselineR2, 0d, IsNumeric: false));
        }

        return results
            .OrderBy(item => item.Item2) // most negative MeanRSquaredDrop first = most important
            .ToList();
    }

    /// <summary>Scores <paramref name="rows"/> with <paramref name="model"/> and returns the R² metric.</summary>
    private static double EvaluateR2(
        MLContext mlContext,
        ITransformer model,
        IEnumerable<FullContextAthleteScoreTrainingRow> rows)
    {
        var dataView = mlContext.Data.LoadFromEnumerable(rows);
        var predictions = model.Transform(dataView);
        return mlContext.Regression.Evaluate(
            predictions,
            labelColumnName: nameof(FullContextAthleteScoreTrainingRow.Label),
            scoreColumnName: "Score").RSquared;
    }

    /// <summary>In-place Fisher-Yates shuffle.</summary>
    private static void FisherYatesShuffle<T>(T[] array, Random rng)
    {
        for (var i = array.Length - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (array[i], array[j]) = (array[j], array[i]);
        }
    }

    private static async Task<List<FullContextAthleteScoreTrainingRow>> QueryTrainingRowsAsync(
        AppDbContext dbContext,
        int? fromRound,
        int? toRound,
        CancellationToken cancellationToken)
    {
        var athleteQuery = dbContext.AthleteRounds
            .AsNoTracking()
            .Include(item => item.Club)
            .Include(item => item.Context)
            .Include(item => item.Position)
            .Include(item => item.Status)
            .Where(item =>
                item.DataPhase == RoundDataPhaseValues.HistoricalClosed &&
                item.WasInGame &&
                item.ClubId != 0 &&
                item.PositionId != 0 &&
                item.StatusId != 0);

        if (fromRound.HasValue)
        {
            athleteQuery = athleteQuery.Where(item => item.RoundId >= fromRound.Value);
        }

        if (toRound.HasValue)
        {
            athleteQuery = athleteQuery.Where(item => item.RoundId <= toRound.Value);
        }

        var athleteRounds = await athleteQuery
            .OrderBy(item => item.RoundId)
            .ThenBy(item => item.AthleteId)
            .ToListAsync(cancellationToken);

        if (athleteRounds.Count == 0)
        {
            return [];
        }

        var rounds = athleteRounds
            .Select(item => item.RoundId)
            .Distinct()
            .ToArray();

        var lookup = await FullContextFeatureResolver.LoadFeatureLookupAsync(dbContext, rounds, cancellationToken);

        var rows = new List<FullContextAthleteScoreTrainingRow>(athleteRounds.Count);
        foreach (var athleteRound in athleteRounds)
        {
            if (FullContextFeatureResolver.TryBuildTrainingRow(athleteRound, lookup, out var row))
            {
                rows.Add(row);
            }
        }

        return rows;
    }

    private static void NormalizeOptionalRange(ref int? fromRound, ref int? toRound)
    {
        if (fromRound.HasValue && fromRound <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fromRound), "Round numbers must be greater than zero.");
        }

        if (toRound.HasValue && toRound <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(toRound), "Round numbers must be greater than zero.");
        }

        if (fromRound.HasValue && toRound.HasValue && fromRound > toRound)
        {
            (fromRound, toRound) = (toRound, fromRound);
        }
    }

    private static IReadOnlyList<AthleteScorePositionDiagnostics> BuildPositionDiagnostics(
        IReadOnlyCollection<FullContextAthleteScoreTrainingRow> trainingRows,
        IReadOnlyList<FullContextAthleteScoreTrainingRow> holdoutRows,
        IReadOnlyList<float> holdoutScores)
    {
        var trainingLookup = trainingRows
            .GroupBy(item => item.PositionCartolaId)
            .ToDictionary(
                group => group.Key,
                group => new
                {
                    Rows = group.Count(),
                    MeanScore = group.Average(item => item.Label),
                });

        return holdoutRows
            .Zip(holdoutScores, (row, score) => new { Row = row, PredictedScore = (double)score })
            .GroupBy(item => item.Row.PositionCartolaId)
            .OrderBy(group => group.Key)
            .Select(group =>
            {
                var actualScores = group.Select(item => (double)item.Row.Label).ToArray();
                var predictedScores = group.Select(item => item.PredictedScore).ToArray();
                var validationRows = actualScores.Length;
                var meanActualScore = actualScores.Average();
                var meanPredictedScore = predictedScores.Average();
                var actualStd = ComputeStandardDeviation(actualScores, meanActualScore);
                var predictedStd = ComputeStandardDeviation(predictedScores, meanPredictedScore);
                var slopeWeight = Math.Clamp(validationRows / 100d, 0d, 1d);
                var targetSlope = predictedStd > 0.001d
                    ? Math.Clamp(actualStd / predictedStd, 0.7d, 1.5d)
                    : 1d;
                var calibrationSlope = 1d + ((targetSlope - 1d) * slopeWeight);
                var targetOffset = meanActualScore - (calibrationSlope * meanPredictedScore);
                var calibrationOffset = targetOffset * slopeWeight;
                var calibratedScores = predictedScores
                    .Select(item => (calibrationSlope * item) + calibrationOffset)
                    .ToArray();
                var trainingStats = trainingLookup.GetValueOrDefault(group.Key);
                var positionCartolaId = Convert.ToInt32(group.Key);
                var (positionCode, positionName) = GetPositionLabelsSafe(positionCartolaId);

                return new AthleteScorePositionDiagnostics
                {
                    PositionCartolaId = positionCartolaId,
                    PositionCode = positionCode,
                    PositionName = positionName,
                    TrainingRows = trainingStats?.Rows ?? 0,
                    ValidationRows = validationRows,
                    TrainingMeanScore = trainingStats?.MeanScore ?? 0d,
                    ValidationMeanActualScore = meanActualScore,
                    ValidationMeanPredictedScore = meanPredictedScore,
                    ValidationMeanPredictedScoreCalibrated = calibratedScores.Average(),
                    ValidationMeanAbsoluteError = actualScores
                        .Zip(predictedScores, (actual, predicted) => Math.Abs(actual - predicted))
                        .Average(),
                    ValidationRootMeanSquaredError = Math.Sqrt(actualScores
                        .Zip(predictedScores, (actual, predicted) => Math.Pow(actual - predicted, 2))
                        .Average()),
                    ValidationBias = meanPredictedScore - meanActualScore,
                    CalibrationSlope = calibrationSlope,
                    CalibrationOffset = calibrationOffset,
                };
            })
            .ToList();
    }

    private static double ComputeStandardDeviation(IReadOnlyCollection<double> values, double mean)
    {
        if (values.Count == 0)
        {
            return 0d;
        }

        return Math.Sqrt(values
            .Select(item => Math.Pow(item - mean, 2))
            .Average());
    }

    private static (string PositionCode, string PositionName) GetPositionLabelsSafe(int positionCartolaId)
        => positionCartolaId switch
        {
            1 => ("GOL", "Goleiro"),
            2 => ("LAT", "Lateral"),
            3 => ("ZAG", "Zagueiro"),
            4 => ("MEI", "Meia"),
            5 => ("ATA", "Atacante"),
            6 => ("TEC", "Tecnico"),
            _ => ($"P{positionCartolaId}", $"Posicao {positionCartolaId}"),
        };
}
