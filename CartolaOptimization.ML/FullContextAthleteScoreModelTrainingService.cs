using CartolaOptimization.Application.Interfaces;
using CartolaOptimization.Application.Models;
using CartolaOptimization.Domain.Constants;
using CartolaOptimization.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.ML;
using Microsoft.ML.Trainers.LightGbm;

namespace CartolaOptimization.ML;

public sealed class FullContextAthleteScoreModelTrainingService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    FullContextAthleteScoreModelArtifactStore artifactStore,
    Microsoft.Extensions.Options.IOptions<FullContextAthleteScoreModelOptions> options) : IFullContextAthleteScoreModelTrainingService
{
    private const string DatasetKey = "full-context";
    private const string TrainerName = "LightGBM";
    private readonly FullContextAthleteScoreModelOptions options = options.Value;

    public async Task<AthleteScoreModelTrainingResult> TrainAsync(
        int? fromRound = null,
        int? toRound = null,
        int validationRounds = 1,
        CancellationToken cancellationToken = default)
    {
        NormalizeOptionalRange(ref fromRound, ref toRound);
        validationRounds = Math.Max(1, validationRounds);

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

        var pipeline = BuildPipeline(mlContext);
        var model = pipeline.Fit(trainData);
        var predictions = model.Transform(holdoutData);
        var metrics = mlContext.Regression.Evaluate(predictions, labelColumnName: nameof(FullContextAthleteScoreTrainingRow.Label), scoreColumnName: "Score");

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

    private static IEstimator<ITransformer> BuildPipeline(MLContext mlContext)
    {
        var excludedNumericFeatures = new HashSet<string>(StringComparer.Ordinal)
        {
            nameof(FullContextAthleteScoreTrainingRow.Label),
            nameof(FullContextAthleteScoreTrainingRow.ClubCartolaId),
            nameof(FullContextAthleteScoreTrainingRow.OpponentClubCartolaId),
            nameof(FullContextAthleteScoreTrainingRow.PositionCartolaId),
            nameof(FullContextAthleteScoreTrainingRow.StatusCartolaId),
        };

        var numericFeatures = typeof(FullContextAthleteScoreTrainingRow)
            .GetProperties()
            .Where(item => item.PropertyType == typeof(float) && !excludedNumericFeatures.Contains(item.Name))
            .Select(item => item.Name)
            .ToArray();

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
}
