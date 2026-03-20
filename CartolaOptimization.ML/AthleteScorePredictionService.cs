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

        take = Math.Clamp(take, 1, 500);

        var metadata = await artifactStore.LoadMetadataAsync(cancellationToken);
        if (metadata is null || !artifactStore.ModelExists())
        {
            throw new InvalidOperationException("No trained athlete-score model artifact was found. Train the v0 model before requesting machine-learning predictions.");
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var query = dbContext.AthleteRounds
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
            query = query.Where(item => item.Status.CartolaId == statusCartolaId.Value);
        }

        if (clubCartolaId.HasValue)
        {
            query = query.Where(item => item.Club.CartolaId == clubCartolaId.Value);
        }

        if (positionCartolaId.HasValue)
        {
            query = query.Where(item => item.Position.CartolaId == positionCartolaId.Value);
        }

        var baselines = await query
            .OrderBy(item => item.Athlete.Name)
            .ToListAsync(cancellationToken);

        if (baselines.Count == 0)
        {
            throw new InvalidOperationException($"No current athlete baseline rows were found for round {round}. Synchronize the current Cartola round before requesting predictions.");
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
                PredictionNotes = "Prediction generated from the current pre-round athlete baseline using the trained v0 athlete-only regression model.",
            })
            .OrderByDescending(item => item.PredictedScore)
            .ThenBy(item => item.AthleteName)
            .Take(take)
            .ToList();

        return new AthleteScorePredictionResult
        {
            Round = round,
            ModelVersion = metadata.ModelVersion,
            GeneratedAtUtc = DateTime.UtcNow,
            ModelTrainedAtUtc = metadata.TrainedAtUtc,
            TotalAthletesConsidered = baselines.Count,
            TotalAthletesPredicted = athletes.Count,
            StatusCartolaIdFilter = statusCartolaId,
            ClubCartolaIdFilter = clubCartolaId,
            PositionCartolaIdFilter = positionCartolaId,
            ValidationMeanAbsoluteError = metadata.MeanAbsoluteError,
            ValidationRootMeanSquaredError = metadata.RootMeanSquaredError,
            Athletes = athletes,
        };
    }

    private static decimal Clamp(decimal value, decimal min, decimal max)
        => value < min ? min : value > max ? max : value;
}
