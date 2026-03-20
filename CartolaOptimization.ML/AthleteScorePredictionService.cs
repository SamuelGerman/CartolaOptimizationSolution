using CartolaOptimization.Application.Interfaces;
using CartolaOptimization.Application.Models;
using CartolaOptimization.Domain.Constants;
using CartolaOptimization.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.ML;

namespace CartolaOptimization.ML;

public sealed class AthleteScorePredictionService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    AthleteScoreModelArtifactStore artifactStore) : IAthleteScorePredictionService
{
    private const string DatasetKey = "athlete-only";

    public async Task<AthleteScorePredictionResult> GetPredictedScoresAsync(
        int round,
        int? statusCartolaId = null,
        int? clubCartolaId = null,
        int? positionCartolaId = null,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        if (round <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(round), "Round numbers must be greater than zero.");
        }

        take = Math.Clamp(take, 1, 5000);

        var metadata = await artifactStore.LoadMetadataAsync(cancellationToken);
        if (metadata is null || !artifactStore.ModelExists())
        {
            throw new InvalidOperationException("No trained athlete-score model artifact was found. Train the v0 model before requesting machine-learning predictions.");
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var currentRoundQuery = dbContext.AthleteRounds
            .AsNoTracking()
            .Include(item => item.Athlete)
            .Include(item => item.Club)
            .Include(item => item.Position)
            .Include(item => item.Status)
            .Where(item =>
                item.RoundId == round &&
                item.ClubId != 0 &&
                item.PositionId != 0 &&
                item.StatusId != 0 &&
                item.DataPhase != RoundDataPhaseValues.HistoricalClosed);

        if (statusCartolaId.HasValue)
        {
            currentRoundQuery = currentRoundQuery.Where(item => item.Status.CartolaId == statusCartolaId.Value);
        }

        if (clubCartolaId.HasValue)
        {
            currentRoundQuery = currentRoundQuery.Where(item => item.Club.CartolaId == clubCartolaId.Value);
        }

        if (positionCartolaId.HasValue)
        {
            currentRoundQuery = currentRoundQuery.Where(item => item.Position.CartolaId == positionCartolaId.Value);
        }

        var currentRoundBaselines = await currentRoundQuery
            .OrderBy(item => item.Athlete.Name)
            .ToListAsync(cancellationToken);

        var usingCurrentRoundBaselines = currentRoundBaselines.Count > 0;

        var baselines = usingCurrentRoundBaselines
            ? currentRoundBaselines
            : await LoadLatestPersistedBaselinesAsync(
                dbContext,
                round,
                statusCartolaId,
                clubCartolaId,
                positionCartolaId,
                cancellationToken);

        if (baselines.Count == 0)
        {
            throw new InvalidOperationException(
                $"No athlete baseline rows were found for round {round}. Synchronize the current Cartola round or ensure at least one earlier persisted athlete round exists.");
        }

        var mlContext = new MLContext(seed: 42);
        using var modelStream = File.OpenRead(artifactStore.GetModelPath());
        var model = mlContext.Model.Load(modelStream, out _);

        var inputs = baselines.Select(item => new AthleteScorePredictionInput
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
        }).ToList();

        var dataView = mlContext.Data.LoadFromEnumerable(inputs);
        var transformed = model.Transform(dataView);
        var scores = mlContext.Data.CreateEnumerable<AthleteScorePredictionOutput>(transformed, reuseRowObject: false).ToList();

        var athletes = baselines
            .Zip(scores, (baseline, prediction) => new AthleteScorePredictionItem
            {
                AthleteCartolaId = baseline.Athlete.CartolaId,
                AthleteName = baseline.Athlete.Name,
                AthleteNickname = baseline.Athlete.Nickname,
                ClubCartolaId = baseline.Club.CartolaId,
                ClubName = baseline.Club.Nickname,
                PositionCartolaId = baseline.Position.CartolaId,
                PositionName = baseline.Position.Name,
                StatusCartolaId = baseline.Status.CartolaId,
                StatusName = baseline.Status.Name,
                BaselineRound = baseline.RoundId,
                BaselineDataPhase = baseline.DataPhase,
                PriceNum = baseline.PriceNum,
                MovingAverage = baseline.MovingAverage,
                AverageScore = baseline.AverageScoreNum,
                PredictedScore = Math.Round(Clamp((decimal)prediction.Score, -10m, 30m), 2, MidpointRounding.AwayFromZero),
                PredictionNotes = usingCurrentRoundBaselines
                    ? "Prediction generated from the current pre-round athlete baseline using the trained v0 athlete-only regression model."
                    : $"Prediction generated from the latest persisted athlete baseline before round {round} because no current pre-round rows were available for that round.",
            })
            .OrderByDescending(item => item.PredictedScore)
            .ThenBy(item => item.AthleteName)
            .Take(take)
            .ToList();

        return new AthleteScorePredictionResult
        {
            Round = round,
            ModelVersion = metadata.ModelVersion,
            DatasetKey = DatasetKey,
            GeneratedAtUtc = DateTime.UtcNow,
            ModelTrainedAtUtc = metadata.TrainedAtUtc,
            TotalAthletesConsidered = baselines.Count,
            ExcludedAthletesWithoutContext = 0,
            TotalAthletesPredicted = athletes.Count,
            StatusCartolaIdFilter = statusCartolaId,
            ClubCartolaIdFilter = clubCartolaId,
            PositionCartolaIdFilter = positionCartolaId,
            ValidationMeanAbsoluteError = metadata.MeanAbsoluteError,
            ValidationRootMeanSquaredError = metadata.RootMeanSquaredError,
            PredictionNotes = usingCurrentRoundBaselines
                ? "Predictions used current pre-round athlete baselines and the trained v0 athlete-only regression model."
                : $"Predictions used the latest persisted athlete baselines before round {round} because current pre-round rows were not available for that round.",
            Athletes = athletes,
        };
    }

    private static decimal Clamp(decimal value, decimal min, decimal max)
        => value < min ? min : value > max ? max : value;

    private static async Task<List<Domain.Entities.AthleteRound>> LoadLatestPersistedBaselinesAsync(
        AppDbContext dbContext,
        int round,
        int? statusCartolaId,
        int? clubCartolaId,
        int? positionCartolaId,
        CancellationToken cancellationToken)
    {
        var historicalBaselines = await dbContext.AthleteRounds
            .AsNoTracking()
            .Include(item => item.Athlete)
            .Include(item => item.Club)
            .Include(item => item.Position)
            .Include(item => item.Status)
            .Where(item =>
                item.RoundId < round &&
                item.ClubId != 0 &&
                item.PositionId != 0 &&
                item.StatusId != 0)
            .OrderBy(item => item.Athlete.Name)
            .ToListAsync(cancellationToken);

        var latestBaselines = historicalBaselines
            .GroupBy(item => item.AthleteId)
            .Select(group => group
                .OrderByDescending(item => item.RoundId)
                .ThenByDescending(item => item.Id)
                .First())
            .ToList();

        if (statusCartolaId.HasValue)
        {
            latestBaselines = latestBaselines
                .Where(item => item.Status.CartolaId == statusCartolaId.Value)
                .ToList();
        }

        if (clubCartolaId.HasValue)
        {
            latestBaselines = latestBaselines
                .Where(item => item.Club.CartolaId == clubCartolaId.Value)
                .ToList();
        }

        if (positionCartolaId.HasValue)
        {
            latestBaselines = latestBaselines
                .Where(item => item.Position.CartolaId == positionCartolaId.Value)
                .ToList();
        }

        return latestBaselines
            .OrderBy(item => item.Athlete.Name)
            .ToList();
    }
}
