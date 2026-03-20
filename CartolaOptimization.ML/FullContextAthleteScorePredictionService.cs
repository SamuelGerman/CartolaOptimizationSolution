using CartolaOptimization.Application.Interfaces;
using CartolaOptimization.Application.Models;
using CartolaOptimization.Domain.Constants;
using CartolaOptimization.Domain.Entities;
using CartolaOptimization.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.ML;

namespace CartolaOptimization.ML;

public sealed class FullContextAthleteScorePredictionService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    FullContextAthleteScoreModelArtifactStore artifactStore) : IFullContextAthleteScorePredictionService
{
    private const string DatasetKey = "full-context";

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
            throw new InvalidOperationException("No trained full-context athlete-score model artifact was found. Train the v1 full-context model before requesting predictions.");
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

        var completeClubRounds = await FullContextFeatureResolver.LoadCompleteClubRoundsAsync(dbContext, [round], cancellationToken);
        var matchByRoundAndClubId = await FullContextFeatureResolver.LoadMatchesByRoundAndClubIdAsync(dbContext, [round], cancellationToken);

        var candidates = new List<FullContextPredictionCandidate>(baselines.Count);
        foreach (var baseline in baselines)
        {
            if (FullContextFeatureResolver.TryBuildPredictionCandidate(round, baseline, completeClubRounds, matchByRoundAndClubId, out var candidate))
            {
                candidates.Add(candidate);
            }
        }

        var excludedAthletesWithoutContext = baselines.Count - candidates.Count;
        if (candidates.Count == 0)
        {
            throw new InvalidOperationException(
                $"No full-context prediction candidates were available for round {round}. Synchronize Cartola current baselines and Gato Mestre club enrichment for the target round before requesting full-context predictions.");
        }

        var mlContext = new MLContext(seed: 42);
        using var modelStream = File.OpenRead(artifactStore.GetModelPath());
        var model = mlContext.Model.Load(modelStream, out _);

        var dataView = mlContext.Data.LoadFromEnumerable(candidates.Select(item => item.Input));
        var transformed = model.Transform(dataView);
        var scores = mlContext.Data.CreateEnumerable<AthleteScorePredictionOutput>(transformed, reuseRowObject: false).ToList();

        var itemPredictionNotes = usingCurrentRoundBaselines
            ? "Prediction generated from the current pre-round athlete baseline together with target-round club and opponent context using the trained v1 full-context regression model."
            : $"Prediction generated from the latest persisted athlete baseline before round {round} combined with target-round club and opponent context because no current pre-round athlete rows were available for that round.";

        var athletes = candidates
            .Zip(scores, (candidate, prediction) => new AthleteScorePredictionItem
            {
                AthleteCartolaId = candidate.Baseline.Athlete.CartolaId,
                AthleteName = candidate.Baseline.Athlete.Name,
                AthleteNickname = candidate.Baseline.Athlete.Nickname,
                ClubCartolaId = candidate.Baseline.Club.CartolaId,
                ClubName = candidate.Baseline.Club.Nickname,
                PositionCartolaId = candidate.Baseline.Position.CartolaId,
                PositionName = candidate.Baseline.Position.Name,
                StatusCartolaId = candidate.Baseline.Status.CartolaId,
                StatusName = candidate.Baseline.Status.Name,
                BaselineRound = candidate.Baseline.RoundId,
                BaselineDataPhase = candidate.Baseline.DataPhase,
                PriceNum = candidate.Baseline.PriceNum,
                MovingAverage = candidate.Baseline.MovingAverage,
                AverageScore = candidate.Baseline.AverageScoreNum,
                IsHomeMatch = candidate.IsHomeMatch,
                ClubTablePosition = candidate.ClubTablePosition,
                OpponentClubCartolaId = candidate.OpponentClubCartolaId,
                OpponentClubName = candidate.OpponentClubName,
                OpponentClubTablePosition = candidate.OpponentClubTablePosition,
                ClubWinProbability = candidate.ClubWinProbability,
                ClubExpectedPoints = candidate.ClubExpectedPoints,
                OpponentWinProbability = candidate.OpponentWinProbability,
                OpponentExpectedPoints = candidate.OpponentExpectedPoints,
                PredictedScore = Math.Round(Clamp((decimal)prediction.Score, -10m, 30m), 2, MidpointRounding.AwayFromZero),
                PredictionNotes = itemPredictionNotes,
            })
            .OrderByDescending(item => item.PredictedScore)
            .ThenBy(item => item.AthleteName)
            .Take(take)
            .ToList();

        var predictionNotes = usingCurrentRoundBaselines
            ? $"Predictions used current pre-round athlete baselines and target-round club/opponent context. {excludedAthletesWithoutContext} athlete(s) were excluded because the target-round full-context enrichment was incomplete."
            : $"Predictions used the latest persisted athlete baselines before round {round} together with target-round club/opponent context. {excludedAthletesWithoutContext} athlete(s) were excluded because the target-round full-context enrichment was incomplete.";

        return new AthleteScorePredictionResult
        {
            Round = round,
            ModelVersion = metadata.ModelVersion,
            DatasetKey = DatasetKey,
            GeneratedAtUtc = DateTime.UtcNow,
            ModelTrainedAtUtc = metadata.TrainedAtUtc,
            TotalAthletesConsidered = baselines.Count,
            ExcludedAthletesWithoutContext = excludedAthletesWithoutContext,
            TotalAthletesPredicted = athletes.Count,
            StatusCartolaIdFilter = statusCartolaId,
            ClubCartolaIdFilter = clubCartolaId,
            PositionCartolaIdFilter = positionCartolaId,
            ValidationMeanAbsoluteError = metadata.MeanAbsoluteError,
            ValidationRootMeanSquaredError = metadata.RootMeanSquaredError,
            PredictionNotes = predictionNotes,
            Athletes = athletes,
        };
    }

    private static decimal Clamp(decimal value, decimal min, decimal max)
        => value < min ? min : value > max ? max : value;

    private static async Task<List<AthleteRound>> LoadLatestPersistedBaselinesAsync(
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
