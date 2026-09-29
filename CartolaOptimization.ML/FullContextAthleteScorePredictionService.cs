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

        // Pontua todos os candidatos com o modelo global full-context.
        // O roteamento por modelo posicional foi avaliado empiricamente: os modelos específicos de posição
        // tiveram desempenho consistentemente inferior ao do modelo global em todas as posições devido aos seus
        // conjuntos de treino por posição menores (269–1 460 linhas vs 4 313 do modelo global),
        // causando um MAE maior mesmo com a seleção de features desativada.
        using var globalModelStream = File.OpenRead(artifactStore.GetModelPath());
        var globalModel = mlContext.Model.Load(globalModelStream, out _);

        var dataView = mlContext.Data.LoadFromEnumerable(candidates.Select(c => c.Input));
        var transformed = globalModel.Transform(dataView);
        var rawScores = mlContext.Data
            .CreateEnumerable<AthleteScorePredictionOutput>(transformed, reuseRowObject: false)
            .ToList();

        var scoredItems = candidates
            .Zip(rawScores, (candidate, score) => (Candidate: candidate, Score: score.Score, EffectiveMetadata: metadata))
            .ToList();

        var itemPredictionNotes = usingCurrentRoundBaselines
            ? "Expected score (conditional model output weighted by the athlete's estimated play probability) generated from the current pre-round athlete baseline with context derived from local historical Cartola matches, athlete rounds and scouts."
            : $"Expected score (conditional model output weighted by the athlete's estimated play probability) generated from the latest persisted athlete baseline before round {round}, combined with context derived from local historical Cartola matches, athlete rounds and scouts because no current pre-round athlete rows were available for that round.";

        var athletes = scoredItems
            .Select(item => new AthleteScorePredictionItem
            {
                AthleteCartolaId = item.Candidate.Baseline.Athlete.CartolaId,
                AthleteName = item.Candidate.Baseline.Athlete.Name,
                AthleteNickname = item.Candidate.Baseline.Athlete.Nickname,
                ClubCartolaId = item.Candidate.Baseline.Club.CartolaId,
                ClubName = item.Candidate.Baseline.Club.Nickname,
                PositionCartolaId = item.Candidate.Baseline.Position.CartolaId,
                PositionName = item.Candidate.Baseline.Position.Name,
                StatusCartolaId = item.Candidate.Baseline.Status.CartolaId,
                StatusName = item.Candidate.Baseline.Status.Name,
                BaselineRound = item.Candidate.Baseline.RoundId,
                BaselineDataPhase = item.Candidate.Baseline.DataPhase,
                PriceNum = item.Candidate.Baseline.PriceNum,
                PlayedMatchesSampleSize = (int)item.Candidate.Input.PlayedMatchesSampleSize,
                MovingAverage = Math.Round((decimal)item.Candidate.Input.MovingAverage, 2, MidpointRounding.AwayFromZero),
                AverageScore = Math.Round((decimal)item.Candidate.Input.AverageScore, 2, MidpointRounding.AwayFromZero),
                IsHomeMatch = item.Candidate.IsHomeMatch,
                ClubTablePosition = item.Candidate.ClubTablePosition,
                OpponentClubCartolaId = item.Candidate.OpponentClubCartolaId,
                OpponentClubName = item.Candidate.OpponentClubName,
                OpponentClubTablePosition = item.Candidate.OpponentClubTablePosition,
                ClubWinProbability = item.Candidate.ClubWinProbability,
                ClubExpectedPoints = item.Candidate.ClubExpectedPoints,
                OpponentWinProbability = item.Candidate.OpponentWinProbability,
                OpponentExpectedPoints = item.Candidate.OpponentExpectedPoints,
                // O modelo é treinado apenas com atletas que jogaram (WasInGame = true), então sua saída bruta
                // é a pontuação CONDICIONAL à participação. Multiplicar pela probabilidade
                // de jogar converte-a na pontuação ESPERADA incondicional (0 pontos se
                // o atleta não entra em campo), que é o critério pelo qual o otimizador de escalação deve ranquear.
                PredictedScore = Math.Round(
                    ApplyPositionCalibration(item.Score, item.Candidate.Baseline.Position.CartolaId, item.EffectiveMetadata)
                        * ComputePlayProbability(item.Candidate.Input),
                    2, MidpointRounding.AwayFromZero),
                PredictionConfidence = ComputePredictionConfidence(item.Candidate.Input),
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

    private static decimal ApplyPositionCalibration(
        float rawScore,
        int positionCartolaId,
        AthleteScoreModelArtifactMetadata metadata)
    {
        var diagnostics = metadata.PositionDiagnostics
            .FirstOrDefault(item => item.PositionCartolaId == positionCartolaId);

        var calibratedScore = diagnostics is null
            ? (decimal)rawScore
            : (decimal)((rawScore * diagnostics.CalibrationSlope) + diagnostics.CalibrationOffset);

        return Clamp(calibratedScore, -10m, 30m);
    }

    /// <summary>
    /// Estima a probabilidade de o atleta participar da rodada-alvo.
    /// O modelo é treinado com atletas que jogaram (WasInGame = true) e, portanto,
    /// prevê a pontuação condicional à participação. Multiplicar pela probabilidade de jogar
    /// converte a previsão condicional em um valor esperado incondicional.
    /// </summary>
    private static decimal ComputePlayProbability(FullContextAthleteScorePredictionInput input)
    {
        var rateLast5 = Clamp((decimal)input.PlayRateLast5, 0m, 1m);
        var empiricalRate = Clamp((decimal)input.EmpiricalPlayRateForCurrentStatus, 0m, 1m);

        // Pondera a forma recente com mais peso (60%) do que a taxa empírica de status (40%).
        // Piso em 0,10 — mesmo atletas incertos têm alguma chance de participar.
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
    /// Quando a rodada atual não tem contexto do GatoMestre (por exemplo, dados ainda não publicados para uma rodada futura),
    /// injeta o contexto histórico mais recente de cada atleta para que o modelo receba
    /// features de estatística de time em vez de entradas todas-zero que ficam fora da distribuição de treino.
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
