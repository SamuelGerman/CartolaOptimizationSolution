using CartolaOptimization.Application.Interfaces;
using CartolaOptimization.Application.Models;
using CartolaOptimization.Domain.Constants;
using CartolaOptimization.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.ML;

namespace CartolaOptimization.ML;

public sealed class AthleteContextAthleteScoreModelTrainingService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    AthleteContextAthleteScoreModelArtifactStore artifactStore,
    Microsoft.Extensions.Options.IOptions<AthleteContextAthleteScoreModelOptions> options) : IAthleteContextAthleteScoreModelTrainingService
{
    private const string DatasetKey = "athlete-context";
    private readonly AthleteContextAthleteScoreModelOptions options = options.Value;

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
            throw new InvalidOperationException("At least two distinct historical closed rounds with valid match context are required to train and validate the v1 athlete-context model.");
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
            throw new InvalidOperationException("The selected round window does not leave enough rows for temporal training and validation of the athlete-context model.");
        }

        var mlContext = new MLContext(seed: 42);
        var trainData = mlContext.Data.LoadFromEnumerable(trainingRows);
        var holdoutData = mlContext.Data.LoadFromEnumerable(holdoutRows);

        var pipeline = BuildPipeline(mlContext);
        var model = pipeline.Fit(trainData);
        var predictions = model.Transform(holdoutData);
        var metrics = mlContext.Regression.Evaluate(predictions, labelColumnName: nameof(AthleteContextAthleteScoreTrainingRow.Label), scoreColumnName: "Score");

        Directory.CreateDirectory(Path.GetDirectoryName(artifactStore.GetModelPath())!);
        mlContext.Model.Save(model, trainData.Schema, artifactStore.GetModelPath());

        var metadata = new AthleteScoreModelArtifactMetadata
        {
            ModelVersion = options.ModelVersion,
            DatasetKey = DatasetKey,
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
            nameof(AthleteContextAthleteScoreTrainingRow.RoundId),
            nameof(AthleteContextAthleteScoreTrainingRow.GamesNum),
            nameof(AthleteContextAthleteScoreTrainingRow.HomeGamesNum),
            nameof(AthleteContextAthleteScoreTrainingRow.AwayGamesNum),
            nameof(AthleteContextAthleteScoreTrainingRow.GamesAtVenueNum),
            nameof(AthleteContextAthleteScoreTrainingRow.MovingAverage),
            nameof(AthleteContextAthleteScoreTrainingRow.AverageScore),
            nameof(AthleteContextAthleteScoreTrainingRow.HomeAverageScore),
            nameof(AthleteContextAthleteScoreTrainingRow.AwayAverageScore),
            nameof(AthleteContextAthleteScoreTrainingRow.ContextualAverageScore),
            nameof(AthleteContextAthleteScoreTrainingRow.PriceNum),
            nameof(AthleteContextAthleteScoreTrainingRow.VariationNum),
            nameof(AthleteContextAthleteScoreTrainingRow.HasAthleteContext),
            nameof(AthleteContextAthleteScoreTrainingRow.TeamAverageScore),
            nameof(AthleteContextAthleteScoreTrainingRow.TeamHomeAverageCurrent),
            nameof(AthleteContextAthleteScoreTrainingRow.TeamHomeAveragePrevious),
            nameof(AthleteContextAthleteScoreTrainingRow.TeamAwayAverageCurrent),
            nameof(AthleteContextAthleteScoreTrainingRow.TeamAwayAveragePrevious),
            nameof(AthleteContextAthleteScoreTrainingRow.TeamVenueAverageCurrent),
            nameof(AthleteContextAthleteScoreTrainingRow.TeamVenueAveragePrevious),
            nameof(AthleteContextAthleteScoreTrainingRow.PointsPerCartoletaCurrent),
            nameof(AthleteContextAthleteScoreTrainingRow.PointsPerCartoletaPrevious),
            nameof(AthleteContextAthleteScoreTrainingRow.GrossOverallAverageCurrent),
            nameof(AthleteContextAthleteScoreTrainingRow.GrossOverallMovingAverageCurrent),
            nameof(AthleteContextAthleteScoreTrainingRow.GrossOverallAveragePrevious),
            nameof(AthleteContextAthleteScoreTrainingRow.GrossOverallMovingAveragePrevious),
            nameof(AthleteContextAthleteScoreTrainingRow.NetOverallAverageCurrent),
            nameof(AthleteContextAthleteScoreTrainingRow.NetOverallMovingAverageCurrent),
            nameof(AthleteContextAthleteScoreTrainingRow.NetOverallAveragePrevious),
            nameof(AthleteContextAthleteScoreTrainingRow.NetOverallMovingAveragePrevious),
            nameof(AthleteContextAthleteScoreTrainingRow.ValuationCurrent),
            nameof(AthleteContextAthleteScoreTrainingRow.MinimumToAppreciateCurrent),
            nameof(AthleteContextAthleteScoreTrainingRow.ValuationPrevious),
            nameof(AthleteContextAthleteScoreTrainingRow.MinimumToAppreciatePrevious),
            nameof(AthleteContextAthleteScoreTrainingRow.IndexPointsPerCartoleta),
            nameof(AthleteContextAthleteScoreTrainingRow.IndexOffensiveUsage),
            nameof(AthleteContextAthleteScoreTrainingRow.IndexOffensiveRepresentation),
            nameof(AthleteContextAthleteScoreTrainingRow.IndexPointsRepresentation),
            nameof(AthleteContextAthleteScoreTrainingRow.IndexOffensiveEfficiency),
            nameof(AthleteContextAthleteScoreTrainingRow.IndexDefensiveEfficiency),
            nameof(AthleteContextAthleteScoreTrainingRow.IndexTackleDuelPercent),
            nameof(AthleteContextAthleteScoreTrainingRow.IndexShotsPerGame),
            nameof(AthleteContextAthleteScoreTrainingRow.IndexShotsPerGoal),
            nameof(AthleteContextAthleteScoreTrainingRow.IsHomeMatch),
            nameof(AthleteContextAthleteScoreTrainingRow.ClubTablePosition),
            nameof(AthleteContextAthleteScoreTrainingRow.OpponentClubTablePosition),
            nameof(AthleteContextAthleteScoreTrainingRow.TablePositionDelta),
            nameof(AthleteContextAthleteScoreTrainingRow.ClubRecentFormPoints),
            nameof(AthleteContextAthleteScoreTrainingRow.OpponentRecentFormPoints),
            nameof(AthleteContextAthleteScoreTrainingRow.RecentFormPointsDelta),
        };

        return mlContext.Transforms.Categorical.OneHotEncoding(new[]
            {
                new InputOutputColumnPair("ClubEncoded", nameof(AthleteContextAthleteScoreTrainingRow.ClubCartolaId)),
                new InputOutputColumnPair("OpponentClubEncoded", nameof(AthleteContextAthleteScoreTrainingRow.OpponentClubCartolaId)),
                new InputOutputColumnPair("PositionEncoded", nameof(AthleteContextAthleteScoreTrainingRow.PositionCartolaId)),
                new InputOutputColumnPair("StatusEncoded", nameof(AthleteContextAthleteScoreTrainingRow.StatusCartolaId)),
            })
            .Append(mlContext.Transforms.Concatenate(
                "Features",
                numericFeatures.Concat(["ClubEncoded", "OpponentClubEncoded", "PositionEncoded", "StatusEncoded"]).ToArray()))
            .Append(mlContext.Transforms.NormalizeMeanVariance("Features"))
            .Append(mlContext.Regression.Trainers.Sdca(
                labelColumnName: nameof(AthleteContextAthleteScoreTrainingRow.Label),
                featureColumnName: "Features"));
    }

    private static async Task<List<AthleteContextAthleteScoreTrainingRow>> QueryTrainingRowsAsync(
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

        var matchByRoundAndClubId = await AthleteContextFeatureResolver.LoadMatchesByRoundAndClubIdAsync(dbContext, rounds, cancellationToken);

        var rows = new List<AthleteContextAthleteScoreTrainingRow>(athleteRounds.Count);
        foreach (var athleteRound in athleteRounds)
        {
            if (AthleteContextFeatureResolver.TryBuildTrainingRow(athleteRound, matchByRoundAndClubId, out var row))
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
