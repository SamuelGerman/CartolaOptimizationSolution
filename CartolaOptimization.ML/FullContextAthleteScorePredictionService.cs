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
            .Include(item => item.Context)
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

        await InjectHistoricalContextFallbackAsync(dbContext, baselines, round, cancellationToken);

        var lookup = await FullContextFeatureResolver.LoadFeatureLookupAsync(dbContext, [round], cancellationToken);

        var candidates = new List<FullContextPredictionCandidate>(baselines.Count);
        foreach (var baseline in baselines)
        {
            if (FullContextFeatureResolver.TryBuildPredictionCandidate(round, baseline, lookup, out var candidate))
            {
                candidates.Add(candidate);
            }
        }

        var excludedAthletesWithoutContext = baselines.Count - candidates.Count;
        if (candidates.Count == 0)
        {
            throw new InvalidOperationException(
                $"No full-context prediction candidates were available for round {round}. Synchronize Cartola baselines and ensure a valid target-round match schedule exists before requesting locally derived full-context predictions.");
        }

        var mlContext = new MLContext(seed: 42);
        using var modelStream = File.OpenRead(artifactStore.GetModelPath());
        var model = mlContext.Model.Load(modelStream, out _);

        var dataView = mlContext.Data.LoadFromEnumerable(candidates.Select(item => item.Input));
        var transformed = model.Transform(dataView);
        var scores = mlContext.Data.CreateEnumerable<AthleteScorePredictionOutput>(transformed, reuseRowObject: false).ToList();

        var itemPredictionNotes = usingCurrentRoundBaselines
            ? "Prediction generated from the current pre-round athlete baseline with club and opponent context derived from local historical Cartola matches, athlete rounds and scouts."
            : $"Prediction generated from the latest persisted athlete baseline before round {round}, combined with club and opponent context derived from local historical Cartola matches, athlete rounds and scouts because no current pre-round athlete rows were available for that round.";

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
                PlayedMatchesSampleSize = (int)candidate.Input.PlayedMatchesSampleSize,
                MovingAverage = Math.Round((decimal)candidate.Input.MovingAverage, 2, MidpointRounding.AwayFromZero),
                AverageScore = Math.Round((decimal)candidate.Input.AverageScore, 2, MidpointRounding.AwayFromZero),
                IsHomeMatch = candidate.IsHomeMatch,
                ClubTablePosition = candidate.ClubTablePosition,
                OpponentClubCartolaId = candidate.OpponentClubCartolaId,
                OpponentClubName = candidate.OpponentClubName,
                OpponentClubTablePosition = candidate.OpponentClubTablePosition,
                ClubWinProbability = candidate.ClubWinProbability,
                ClubExpectedPoints = candidate.ClubExpectedPoints,
                OpponentWinProbability = candidate.OpponentWinProbability,
                OpponentExpectedPoints = candidate.OpponentExpectedPoints,
                PredictedScore = Math.Round(ApplyPositionCalibration(prediction.Score, candidate.Baseline.Position.CartolaId, metadata), 2, MidpointRounding.AwayFromZero),
                PredictionConfidence = ComputePredictionConfidence(candidate.Input),
                PredictionNotes = itemPredictionNotes,
            })
            .OrderByDescending(item => item.PredictedScore)
            .ThenBy(item => item.AthleteName)
            .Take(take)
            .ToList();

        var predictionNotes = usingCurrentRoundBaselines
            ? $"Predictions used current pre-round athlete baselines plus local full-context derived from historical Cartola matches, athlete rounds and scouts. {excludedAthletesWithoutContext} athlete(s) were excluded because a valid target-round match context was unavailable."
            : $"Predictions used the latest persisted athlete baselines before round {round} plus local full-context derived from historical Cartola matches, athlete rounds and scouts. {excludedAthletesWithoutContext} athlete(s) were excluded because a valid target-round match context was unavailable.";

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

    private static decimal ApplyPositionCalibration(float rawScore, int positionCartolaId, AthleteScoreModelArtifactMetadata metadata)
    {
        var diagnostics = metadata.PositionDiagnostics
            .FirstOrDefault(item => item.PositionCartolaId == positionCartolaId);

        var calibratedScore = diagnostics is null
            ? (decimal)rawScore
            : (decimal)((rawScore * diagnostics.CalibrationSlope) + diagnostics.CalibrationOffset);

        return Clamp(calibratedScore, -10m, 30m);
    }

    /// <summary>
    /// Estimates the probability that the athlete will participate in the target round.
    /// The model is trained on athletes who played (WasInGame = true) and therefore
    /// predicts score conditional on participation. Multiplying by play probability
    /// converts the conditional prediction to an unconditional expected value.
    /// </summary>
    private static decimal ComputePlayProbability(FullContextAthleteScorePredictionInput input)
    {
        var rateLast5 = Clamp((decimal)input.PlayRateLast5, 0m, 1m);
        var empiricalRate = Clamp((decimal)input.EmpiricalPlayRateForCurrentStatus, 0m, 1m);

        // Weight recent form more heavily (60%) than status empirical rate (40%).
        // Floor at 0.10 — even uncertain athletes have some chance of participating.
        var blended = (rateLast5 * 0.6m) + (empiricalRate * 0.4m);
        return Clamp(blended, 0.10m, 1.0m);
    }

    private static decimal ComputePredictionConfidence(FullContextAthleteScorePredictionInput input)
    {
        var experience = Clamp((decimal)input.PlayedMatchesSampleSize / 10m, 0m, 1m);
        var recentCoverageLast3 = Clamp((decimal)input.HistoryCoverageLast3, 0m, 1m);
        var recentCoverageLast5 = Clamp((decimal)input.HistoryCoverageLast5, 0m, 1m);
        var venueCoverage = Clamp((decimal)input.VenueHistoryCoverageLast5, 0m, 1m);
        var statusCoverage = Clamp((decimal)input.CurrentStatusCoverageLast5, 0m, 1m);
        var availability = Clamp((((decimal)input.PlayRateLast5 + (decimal)input.EmpiricalPlayRateForCurrentStatus) / 2m), 0m, 1m);
        var clubContextCoverage = Clamp((decimal)input.ClubMatchesSampleSize / 5m, 0m, 1m);
        var opponentContextCoverage = Clamp((decimal)input.OpponentMatchesSampleSize / 5m, 0m, 1m);
        var athleteContextPresence = Clamp((decimal)input.HasAthleteContext, 0m, 1m);

        var confidence =
            0.15m +
            (experience * 0.20m) +
            (recentCoverageLast3 * 0.15m) +
            (recentCoverageLast5 * 0.10m) +
            (venueCoverage * 0.10m) +
            (statusCoverage * 0.10m) +
            (availability * 0.10m) +
            (clubContextCoverage * 0.05m) +
            (opponentContextCoverage * 0.05m) +
            (athleteContextPresence * 0.05m);

        confidence -= Clamp((decimal)input.IsColdStartAthlete, 0m, 1m) * 0.08m;
        confidence -= Clamp((decimal)input.StatusChangeFlag, 0m, 1m) * 0.04m;
        confidence -= Clamp((decimal)input.RoundsSinceLastInGame / 20m, 0m, 0.05m);

        return Math.Round(Clamp(confidence, 0.05m, 0.99m), 4, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// When the current round has no GatoMestre context (e.g. data not yet published for an upcoming round),
    /// inject the most recent historical context for each athlete so the model receives realistic
    /// team-stat features instead of all-zero inputs that fall outside the training distribution.
    /// </summary>
    private static async Task InjectHistoricalContextFallbackAsync(
        AppDbContext dbContext,
        IReadOnlyList<AthleteRound> baselines,
        int round,
        CancellationToken cancellationToken)
    {
        var missingContextAthleteIds = baselines
            .Where(item => item.Context is null)
            .Select(item => item.AthleteId)
            .ToHashSet();

        if (missingContextAthleteIds.Count == 0)
        {
            return;
        }

        var historicalWithContext = await dbContext.AthleteRounds
            .AsNoTracking()
            .Include(item => item.Context)
            .Where(item =>
                missingContextAthleteIds.Contains(item.AthleteId) &&
                item.Context != null &&
                item.RoundId < round &&
                item.DataPhase == RoundDataPhaseValues.HistoricalClosed)
            .OrderByDescending(item => item.RoundId)
            .ThenByDescending(item => item.Id)
            .ToListAsync(cancellationToken);

        var latestContextByAthleteId = historicalWithContext
            .GroupBy(item => item.AthleteId)
            .ToDictionary(group => group.Key, group => group.First().Context!);

        foreach (var baseline in baselines)
        {
            if (baseline.Context is null &&
                latestContextByAthleteId.TryGetValue(baseline.AthleteId, out var fallbackContext))
            {
                baseline.Context = fallbackContext;
            }
        }
    }

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
            .Include(item => item.Context)
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
