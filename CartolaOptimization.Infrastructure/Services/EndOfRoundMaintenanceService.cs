using CartolaOptimization.Application.Interfaces;
using CartolaOptimization.Application.Models;
using CartolaOptimization.Domain.Constants;
using CartolaOptimization.Infrastructure.Context;
using CartolaOptimization.Infrastructure.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace CartolaOptimization.Infrastructure.Services;

/// <summary>
/// Orchestrates the recurring end-of-round workflow required to refresh historical data and retrain the primary full-context model.
/// </summary>
public sealed class EndOfRoundMaintenanceService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ICartolaSyncService cartolaSyncService,
    IGatoMestreSyncService gatoMestreSyncService,
    ITrainingDatasetPreparationService trainingDatasetPreparationService,
    IFullContextAthleteScoreModelTrainingService fullContextAthleteScoreModelTrainingService,
    IPositionalAthleteScoreModelTrainingService positionalAthleteScoreModelTrainingService) : IEndOfRoundMaintenanceService
{
    private const string StepStatusExecuted = "Executed";
    private const string StepStatusSkipped = "Skipped";
    private const string CartolaClosedRoundProcessName = "CartolaSyncService.SyncClosedRoundAsync";
    private const string GatoMestreRoundProcessName = "GatoMestreSyncService.SyncRoundAsync";

    public async Task<EndOfRoundMaintenanceResult> RunAsync(
        int validationRounds = 1,
        bool forceLatestClosedRound = false,
        CancellationToken cancellationToken = default)
    {
        validationRounds = Math.Max(1, validationRounds);

        var steps = new List<EndOfRoundMaintenanceStepResult>();
        var currentMarketSync = await cartolaSyncService.SyncAsync(cancellationToken: cancellationToken);
        var season = currentMarketSync.Season;
        var currentRound = currentMarketSync.CurrentRound;
        var lastClosedRound = currentRound - 1;

        steps.Add(new EndOfRoundMaintenanceStepResult
        {
            Step = "Sync current Cartola market",
            Status = StepStatusExecuted,
            Reason = $"Synchronized the current Cartola market for season {season}, round {currentRound}.",
            Rounds = currentRound > 0 ? [currentRound] : Array.Empty<int>(),
        });

        var existingModelInfo = await fullContextAthleteScoreModelTrainingService.GetCurrentModelInfoAsync(cancellationToken);
        if (lastClosedRound <= 0)
        {
            steps.Add(CreateSkippedStep("Finalize closed Cartola rounds", "There is no closed round available yet because the market is still at round 1."));
            steps.Add(CreateSkippedStep("Enrich Gato Mestre historical rounds", "There is no closed round available yet because the market is still at round 1."));
            steps.Add(CreateSkippedStep("Recompute training readiness", "No historical closed round was refreshed in this execution."));
            steps.Add(CreateSkippedStep("Train full-context model", "At least two historical closed rounds are required before the primary full-context model can be trained."));
            steps.Add(CreateSkippedStep("Train positional models", "At least two historical closed rounds are required before positional models can be trained."));

            return new EndOfRoundMaintenanceResult
            {
                Message = "The current Cartola market is still at round 1, so there is no closed round to finalize or retrain yet.",
                Season = season,
                CurrentRound = currentRound,
                LastClosedRound = lastClosedRound,
                ValidationRounds = validationRounds,
                HistoricalDataUpdated = false,
                ModelRetrained = false,
                CurrentMarketSync = currentMarketSync,
                PrimaryModelInfo = existingModelInfo,
                Steps = steps,
            };
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var completedCartolaRounds = await GetCompletedCartolaClosedRoundsAsync(dbContext, lastClosedRound, cancellationToken);
        var completedGatoMestreRounds = await GetCompletedGatoMestreRoundsAsync(dbContext, season, lastClosedRound, cancellationToken);

        var cartolaRoundsToProcess = BuildRoundsToProcess(lastClosedRound, completedCartolaRounds, forceLatestClosedRound);
        var gatoMestreRoundsToProcess = BuildRoundsToProcess(lastClosedRound, completedGatoMestreRounds, forceLatestClosedRound);

        var cartolaBackfill = await ExecuteCartolaClosedRoundsAsync(cartolaRoundsToProcess, cancellationToken);
        if (cartolaBackfill is null)
        {
            steps.Add(CreateSkippedStep(
                "Finalize closed Cartola rounds",
                $"All closed Cartola rounds up to round {lastClosedRound} already have a successful or partial finalization recorded."));
        }
        else
        {
            steps.Add(new EndOfRoundMaintenanceStepResult
            {
                Step = "Finalize closed Cartola rounds",
                Status = StepStatusExecuted,
                Reason = $"Processed {cartolaBackfill.RequestedRounds} closed Cartola round(s) up to round {lastClosedRound}.",
                Rounds = cartolaBackfill.Rounds.Select(item => item.Round).OrderBy(item => item).ToArray(),
            });
        }

        var gatoMestreBackfill = await ExecuteGatoMestreRoundsAsync(season, gatoMestreRoundsToProcess, cancellationToken);
        if (gatoMestreBackfill is null)
        {
            steps.Add(CreateSkippedStep(
                "Enrich Gato Mestre historical rounds",
                $"All closed Gato Mestre athlete-context rounds up to round {lastClosedRound} already have a successful or partial enrichment recorded."));
        }
        else
        {
            steps.Add(new EndOfRoundMaintenanceStepResult
            {
                Step = "Enrich Gato Mestre historical rounds",
                Status = StepStatusExecuted,
                Reason = $"Processed {gatoMestreBackfill.RequestedRounds} Gato Mestre round(s) up to round {lastClosedRound}.",
                Rounds = gatoMestreBackfill.Rounds.Select(item => item.Round).OrderBy(item => item).ToArray(),
            });
        }

        var updatedHistoricalRounds = cartolaRoundsToProcess
            .Concat(gatoMestreRoundsToProcess)
            .Distinct()
            .OrderBy(item => item)
            .ToArray();

        TrainingDatasetReadinessResult? readinessResult = null;
        if (updatedHistoricalRounds.Length > 0)
        {
            readinessResult = await trainingDatasetPreparationService.RecomputeReadinessAsync(
                updatedHistoricalRounds[0],
                updatedHistoricalRounds[^1],
                cancellationToken);

            steps.Add(new EndOfRoundMaintenanceStepResult
            {
                Step = "Recompute training readiness",
                Status = StepStatusExecuted,
                Reason = $"Recomputed readiness metadata for the refreshed round window {updatedHistoricalRounds[0]}..{updatedHistoricalRounds[^1]}.",
                Rounds = updatedHistoricalRounds,
            });
        }
        else
        {
            steps.Add(CreateSkippedStep("Recompute training readiness", "No historical closed round was refreshed in this execution."));
        }

        var cartolaBackfillFailed = cartolaBackfill?.FailedRounds > 0;
        var gatoMestreBackfillFailed = gatoMestreBackfill?.FailedRounds > 0;
        var historicalDataUpdated = updatedHistoricalRounds.Length > 0;
        var modelNeedsRefresh = existingModelInfo is null ||
            existingModelInfo.ToRound != lastClosedRound ||
            existingModelInfo.ValidationRounds != validationRounds;

        AthleteScoreModelTrainingResult? trainingResult = null;
        var activeModelInfo = existingModelInfo;

        if (cartolaBackfillFailed || gatoMestreBackfillFailed)
        {
            steps.Add(CreateSkippedStep(
                "Train full-context model",
                "Model training was skipped because at least one historical refresh step failed and the latest training window could not be considered stable."));
        }
        else if (!historicalDataUpdated && !modelNeedsRefresh)
        {
            steps.Add(CreateSkippedStep(
                "Train full-context model",
                $"The primary full-context model is already trained up to round {lastClosedRound} with validationRounds={validationRounds}."));
        }
        else
        {
            try
            {
                trainingResult = await fullContextAthleteScoreModelTrainingService.TrainAsync(
                    toRound: lastClosedRound,
                    validationRounds: validationRounds,
                    cancellationToken: cancellationToken);

                activeModelInfo = trainingResult.ModelInfo;

                steps.Add(new EndOfRoundMaintenanceStepResult
                {
                    Step = "Train full-context model",
                    Status = StepStatusExecuted,
                    Reason = $"Trained the primary full-context model with historical closed rounds up to round {lastClosedRound}.",
                    Rounds = [lastClosedRound],
                });
            }
            catch (InvalidOperationException exception)
            {
                activeModelInfo = await fullContextAthleteScoreModelTrainingService.GetCurrentModelInfoAsync(cancellationToken);
                steps.Add(CreateSkippedStep("Train full-context model", exception.Message));
            }
        }

        PositionalAthleteScoreTrainingResult? positionalTrainingResult = null;

        if (trainingResult is not null)
        {
            try
            {
                positionalTrainingResult = await positionalAthleteScoreModelTrainingService.TrainAllAsync(
                    toRound: lastClosedRound,
                    validationRounds: validationRounds,
                    cancellationToken: cancellationToken);

                steps.Add(new EndOfRoundMaintenanceStepResult
                {
                    Step = "Train positional models",
                    Status = StepStatusExecuted,
                    Reason = $"Trained {positionalTrainingResult.TrainedPositions} positional model(s); {positionalTrainingResult.SkippedPositions} skipped due to insufficient data.",
                    Rounds = [lastClosedRound],
                });
            }
            catch (InvalidOperationException exception)
            {
                steps.Add(CreateSkippedStep("Train positional models", exception.Message));
            }
        }
        else
        {
            steps.Add(CreateSkippedStep(
                "Train positional models",
                "Positional model training is skipped because the primary full-context model was not retrained in this execution."));
        }

        return new EndOfRoundMaintenanceResult
        {
            Message = BuildSummaryMessage(lastClosedRound, historicalDataUpdated, trainingResult is not null, cartolaBackfillFailed || gatoMestreBackfillFailed),
            Season = season,
            CurrentRound = currentRound,
            LastClosedRound = lastClosedRound,
            ValidationRounds = validationRounds,
            HistoricalDataUpdated = historicalDataUpdated,
            ModelRetrained = trainingResult is not null,
            CurrentMarketSync = currentMarketSync,
            CartolaClosedRoundBackfill = cartolaBackfill,
            GatoMestreBackfill = gatoMestreBackfill,
            ReadinessRecompute = readinessResult,
            PrimaryModelTraining = trainingResult,
            PrimaryModelInfo = activeModelInfo,
            PositionalModelTraining = positionalTrainingResult,
            Steps = steps,
        };
    }

    private async Task<CartolaBackfillResult?> ExecuteCartolaClosedRoundsAsync(
        IReadOnlyCollection<int> rounds,
        CancellationToken cancellationToken)
    {
        if (rounds.Count == 0)
        {
            return null;
        }

        var orderedRounds = rounds.OrderBy(item => item).ToArray();
        var roundResults = new List<CartolaBackfillRoundResult>(orderedRounds.Length);
        var successfulRounds = 0;
        var partialRounds = 0;
        var failedRounds = 0;

        foreach (var round in orderedRounds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var result = await cartolaSyncService.SyncClosedRoundAsync(round, cancellationToken);
                if (result.Status == SyncExecutionStatus.Partial)
                {
                    partialRounds++;
                }
                else
                {
                    successfulRounds++;
                }

                roundResults.Add(new CartolaBackfillRoundResult
                {
                    Round = round,
                    Status = result.Status,
                    SyncExecutionId = result.SyncExecutionId,
                    CorrelationId = result.CorrelationId,
                    Notes = result.Notes,
                });
            }
            catch (DataCollectionProcessException exception)
            {
                failedRounds++;
                roundResults.Add(new CartolaBackfillRoundResult
                {
                    Round = round,
                    Status = SyncExecutionStatus.Failed,
                    CorrelationId = exception.CorrelationId,
                    ErrorMessage = exception.Message,
                    ErrorLogId = exception.ErrorLogId,
                });
            }
        }

        return new CartolaBackfillResult
        {
            FromRound = orderedRounds[0],
            ToRound = orderedRounds[^1],
            RequestedRounds = orderedRounds.Length,
            SuccessfulRounds = successfulRounds,
            PartialRounds = partialRounds,
            FailedRounds = failedRounds,
            Rounds = roundResults,
        };
    }

    private async Task<GatoMestreBackfillResult?> ExecuteGatoMestreRoundsAsync(
        int season,
        IReadOnlyCollection<int> rounds,
        CancellationToken cancellationToken)
    {
        if (rounds.Count == 0)
        {
            return null;
        }

        var orderedRounds = rounds.OrderBy(item => item).ToArray();
        var roundResults = new List<GatoMestreBackfillRoundResult>(orderedRounds.Length);
        var successfulRounds = 0;
        var partialRounds = 0;
        var failedRounds = 0;

        foreach (var round in orderedRounds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var result = await gatoMestreSyncService.SyncRoundAsync(season, round, cancellationToken);
                if (result.Status == SyncExecutionStatus.Partial || result.IsPartialSync)
                {
                    partialRounds++;
                }
                else
                {
                    successfulRounds++;
                }

                roundResults.Add(new GatoMestreBackfillRoundResult
                {
                    Round = round,
                    Status = result.Status,
                    SyncExecutionId = result.SyncExecutionId,
                    CorrelationId = result.CorrelationId,
                    Notes = result.Notes,
                });
            }
            catch (DataCollectionProcessException exception)
            {
                failedRounds++;
                roundResults.Add(new GatoMestreBackfillRoundResult
                {
                    Round = round,
                    Status = SyncExecutionStatus.Failed,
                    CorrelationId = exception.CorrelationId,
                    ErrorMessage = exception.Message,
                    ErrorLogId = exception.ErrorLogId,
                });
            }
        }

        return new GatoMestreBackfillResult
        {
            Season = season,
            FromRound = orderedRounds[0],
            ToRound = orderedRounds[^1],
            RequestedRounds = orderedRounds.Length,
            SuccessfulRounds = successfulRounds,
            PartialRounds = partialRounds,
            FailedRounds = failedRounds,
            Rounds = roundResults,
        };
    }

    private static int[] BuildRoundsToProcess(
        int lastClosedRound,
        IReadOnlySet<int> completedRounds,
        bool forceLatestClosedRound)
    {
        var rounds = Enumerable.Range(1, lastClosedRound)
            .Where(round => !completedRounds.Contains(round))
            .ToHashSet();

        if (forceLatestClosedRound)
        {
            rounds.Add(lastClosedRound);
        }

        return rounds
            .OrderBy(item => item)
            .ToArray();
    }

    private static EndOfRoundMaintenanceStepResult CreateSkippedStep(string step, string reason)
        => new()
        {
            Step = step,
            Status = StepStatusSkipped,
            Reason = reason,
        };

    private static string BuildSummaryMessage(
        int lastClosedRound,
        bool historicalDataUpdated,
        bool modelRetrained,
        bool historicalRefreshFailed)
    {
        if (historicalRefreshFailed)
        {
            return $"End-of-round maintenance reached round {lastClosedRound}, but at least one historical refresh step failed. Review the step summary before trusting a new training cycle.";
        }

        if (modelRetrained)
        {
            return $"End-of-round maintenance completed successfully and retrained the primary full-context model through round {lastClosedRound}.";
        }

        if (historicalDataUpdated)
        {
            return $"End-of-round maintenance refreshed historical data through round {lastClosedRound}. Model retraining was skipped because the training preconditions were not satisfied yet.";
        }

        return $"End-of-round maintenance confirmed that historical data and the primary full-context model are already up to date through round {lastClosedRound}.";
    }

    private static async Task<HashSet<int>> GetCompletedCartolaClosedRoundsAsync(
        AppDbContext dbContext,
        int lastClosedRound,
        CancellationToken cancellationToken)
    {
        var syncedRounds = await dbContext.SyncExecutions
            .AsNoTracking()
            .Where(item =>
                item.SourceSystem == "Cartola" &&
                item.ProcessName == CartolaClosedRoundProcessName &&
                item.Round.HasValue &&
                item.Round.Value <= lastClosedRound &&
                (item.Status == SyncExecutionStatus.Success || item.Status == SyncExecutionStatus.Partial))
            .Select(item => item.Round!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        var historicalClosedRounds = await dbContext.AthleteRounds
            .AsNoTracking()
            .Where(item =>
                item.RoundId <= lastClosedRound &&
                item.DataPhase == RoundDataPhaseValues.HistoricalClosed)
            .Select(item => item.RoundId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return syncedRounds
            .Intersect(historicalClosedRounds)
            .ToHashSet();
    }

    private static async Task<HashSet<int>> GetCompletedGatoMestreRoundsAsync(
        AppDbContext dbContext,
        int season,
        int lastClosedRound,
        CancellationToken cancellationToken)
    {
        var syncedRounds = await dbContext.SyncExecutions
            .AsNoTracking()
            .Where(item =>
                item.SourceSystem == "GatoMestre" &&
                item.ProcessName == GatoMestreRoundProcessName &&
                item.Season == season &&
                item.Round.HasValue &&
                item.Round.Value <= lastClosedRound &&
                (item.Status == SyncExecutionStatus.Success || item.Status == SyncExecutionStatus.Partial))
            .Select(item => item.Round!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        var contextRounds = await dbContext.AthleteRoundContexts
            .AsNoTracking()
            .Where(item => item.AthleteRound.RoundId <= lastClosedRound)
            .Select(item => item.AthleteRound.RoundId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return syncedRounds
            .Intersect(contextRounds)
            .ToHashSet();
    }
}
