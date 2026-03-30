using CartolaOptimization.Application.Interfaces;
using CartolaOptimization.Application.Models;
using CartolaOptimization.Domain.Constants;
using CartolaOptimization.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers;

namespace CartolaOptimization.ML;

public sealed class AthleteScoreModelTrainingService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    AthleteScoreModelArtifactStore artifactStore,
    Microsoft.Extensions.Options.IOptions<AthleteScoreModelOptions> options) : IAthleteScoreModelTrainingService
{
    private const string DatasetKey = "athlete-only";
    private readonly AthleteScoreModelOptions options = options.Value;

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
            throw new InvalidOperationException("At least two distinct historical closed rounds are required to train and validate the v0 athlete-score model.");
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
            throw new InvalidOperationException("The selected round window does not leave enough rows for temporal training and validation.");
        }

        var mlContext = new MLContext(seed: 42);
        var trainData = mlContext.Data.LoadFromEnumerable(trainingRows);
        var holdoutData = mlContext.Data.LoadFromEnumerable(holdoutRows);

        var pipeline = BuildPipeline(mlContext);
        var model = pipeline.Fit(trainData);
        var predictions = model.Transform(holdoutData);
        var metrics = mlContext.Regression.Evaluate(predictions, labelColumnName: nameof(AthleteScoreTrainingRow.Label), scoreColumnName: "Score");

        Directory.CreateDirectory(Path.GetDirectoryName(artifactStore.GetModelPath())!);
        mlContext.Model.Save(model, trainData.Schema, artifactStore.GetModelPath());

        var metadata = new AthleteScoreModelArtifactMetadata
        {
            ModelVersion = options.ModelVersion,
            DatasetKey = DatasetKey,
            TrainerName = "SDCA",
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
            Message = $"Model '{options.ModelVersion}' trained successfully with temporal validation across the last {validationRounds} round(s).",
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
        var numericFeatures = new[]
        {
            nameof(AthleteScoreTrainingRow.RoundId),
            nameof(AthleteScoreTrainingRow.GamesNum),
            nameof(AthleteScoreTrainingRow.HomeGamesNum),
            nameof(AthleteScoreTrainingRow.AwayGamesNum),
            nameof(AthleteScoreTrainingRow.MovingAverage),
            nameof(AthleteScoreTrainingRow.AverageScore),
            nameof(AthleteScoreTrainingRow.HomeAverageScore),
            nameof(AthleteScoreTrainingRow.AwayAverageScore),
            nameof(AthleteScoreTrainingRow.PriceNum),
            nameof(AthleteScoreTrainingRow.VariationNum),
        };

        return mlContext.Transforms.Categorical.OneHotEncoding(new[]
            {
                new InputOutputColumnPair("ClubEncoded", nameof(AthleteScoreTrainingRow.ClubCartolaId)),
                new InputOutputColumnPair("PositionEncoded", nameof(AthleteScoreTrainingRow.PositionCartolaId)),
                new InputOutputColumnPair("StatusEncoded", nameof(AthleteScoreTrainingRow.StatusCartolaId)),
            })
            .Append(mlContext.Transforms.Concatenate(
                "Features",
                numericFeatures.Concat(["ClubEncoded", "PositionEncoded", "StatusEncoded"]).ToArray()))
            .Append(mlContext.Regression.Trainers.Sdca(
                labelColumnName: nameof(AthleteScoreTrainingRow.Label),
                featureColumnName: "Features"));
    }

    private static async Task<List<AthleteScoreTrainingRow>> QueryTrainingRowsAsync(
        AppDbContext dbContext,
        int? fromRound,
        int? toRound,
        CancellationToken cancellationToken)
    {
        var query = dbContext.AthleteRounds
            .AsNoTracking()
            .Include(item => item.Club)
            .Include(item => item.Position)
            .Include(item => item.Status)
            .Where(item =>
                item.DataPhase == RoundDataPhaseValues.HistoricalClosed &&
                item.ClubId != 0 &&
                item.PositionId != 0 &&
                item.StatusId != 0);

        if (fromRound.HasValue)
        {
            query = query.Where(item => item.RoundId >= fromRound.Value);
        }

        if (toRound.HasValue)
        {
            query = query.Where(item => item.RoundId <= toRound.Value);
        }

        return await query
            .OrderBy(item => item.RoundId)
            .ThenBy(item => item.AthleteId)
            .Select(item => new AthleteScoreTrainingRow
            {
                RoundId = item.RoundId,
                ClubCartolaId = item.Club.CartolaId,
                PositionCartolaId = item.Position.CartolaId,
                StatusCartolaId = item.Status.CartolaId,
                GamesNum = item.GamesNum,
                HomeGamesNum = item.HomeGamesNum,
                AwayGamesNum = item.AwayGamesNum,
                MovingAverage = (float)item.MovingAverage,
                AverageScore = (float)item.AverageScoreNum,
                HomeAverageScore = (float)item.HomeAverageScore,
                AwayAverageScore = (float)item.AwayAverageScore,
                PriceNum = (float)item.PriceNum,
                VariationNum = (float)item.VariationNum,
                Label = (float)item.ScoreNum,
            })
            .ToListAsync(cancellationToken);
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
