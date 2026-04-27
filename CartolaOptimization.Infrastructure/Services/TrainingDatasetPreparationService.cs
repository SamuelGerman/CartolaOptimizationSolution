using CartolaOptimization.Application.Interfaces;
using CartolaOptimization.Application.Models;
using CartolaOptimization.Domain.Constants;
using CartolaOptimization.Infrastructure.Context;
using CartolaOptimization.Infrastructure.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace CartolaOptimization.Infrastructure.Services;

/// <summary>
/// Prepares the persisted round data for downstream model-training workflows.
/// </summary>
public sealed class TrainingDatasetPreparationService(
    AppDbContext dbContext,
    ICartolaSyncService cartolaSyncService,
    IGatoMestreSyncService gatoMestreSyncService) : ITrainingDatasetPreparationService
{
    public async Task<TrainingDatasetReadinessResult> RecomputeReadinessAsync(
        int fromRound,
        int toRound,
        CancellationToken cancellationToken = default)
    {
        NormalizeRange(ref fromRound, ref toRound);

        // Resolve the current open round from the most recent Cartola baseline sync so that
        // rounds below it that still carry Unknown or empty DataPhase can be promoted to
        // HistoricalClosed.  This handles AthleteRound rows that were created by the Gato
        // Mestre sync for athletes absent from the Cartola pontuados payload.
        var latestKnownCurrentRound = await dbContext.AthleteRounds
            .AsNoTracking()
            .Where(r => r.DataPhase == RoundDataPhaseValues.CurrentOpen)
            .Select(r => r.RoundId)
            .OrderByDescending(r => r)
            .FirstOrDefaultAsync(cancellationToken);

        var clubRounds = await dbContext.ClubRounds
            .Include(round => round.PositionStats)
            .Where(round => round.Round >= fromRound && round.Round <= toRound)
            .ToListAsync(cancellationToken);

        foreach (var clubRound in clubRounds)
        {
            TrainingReadinessEvaluator.ApplyClubRoundReadiness(clubRound);
        }

        var completeClubRoundKeys = clubRounds
            .Where(round => round.IsTrainingEligible)
            .Select(round => (round.Round, round.ClubId))
            .ToHashSet();

        var athleteRounds = await dbContext.AthleteRounds
            .Where(round => round.RoundId >= fromRound && round.RoundId <= toRound)
            .ToListAsync(cancellationToken);

        foreach (var athleteRound in athleteRounds)
        {
            var phaseUnknownOrEmpty = string.IsNullOrWhiteSpace(athleteRound.DataPhase) ||
                                      string.Equals(athleteRound.DataPhase, RoundDataPhaseValues.Unknown, StringComparison.OrdinalIgnoreCase);

            if (phaseUnknownOrEmpty)
            {
                // Promote to HistoricalClosed when a reliable current-round baseline exists
                // and confirms this round has already been finalized.
                athleteRound.DataPhase = latestKnownCurrentRound > 0 && athleteRound.RoundId < latestKnownCurrentRound
                    ? RoundDataPhaseValues.HistoricalClosed
                    : RoundDataPhaseValues.Unknown;
            }

            TrainingReadinessEvaluator.ApplyAthleteReadiness(
                athleteRound,
                completeClubRoundKeys.Contains((athleteRound.RoundId, athleteRound.ClubId)));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new TrainingDatasetReadinessResult
        {
            FromRound = fromRound,
            ToRound = toRound,
            AthleteRoundsEvaluated = athleteRounds.Count,
            AthleteRoundsComplete = athleteRounds.Count(round => round.TrainingReadinessStatus == TrainingReadinessStatusValues.Complete),
            AthleteRoundsPartial = athleteRounds.Count(round => round.TrainingReadinessStatus == TrainingReadinessStatusValues.Partial),
            AthleteRoundsUnfit = athleteRounds.Count(round => round.TrainingReadinessStatus == TrainingReadinessStatusValues.UnfitForTraining),
            ClubRoundsEvaluated = clubRounds.Count,
            ClubRoundsComplete = clubRounds.Count(round => round.TrainingReadinessStatus == TrainingReadinessStatusValues.Complete),
            ClubRoundsPartial = clubRounds.Count(round => round.TrainingReadinessStatus == TrainingReadinessStatusValues.Partial),
            ClubRoundsUnfit = clubRounds.Count(round => round.TrainingReadinessStatus == TrainingReadinessStatusValues.UnfitForTraining),
        };
    }

    public async Task<CartolaBackfillResult> BackfillCartolaResultsAsync(
        int fromRound,
        int toRound,
        CancellationToken cancellationToken = default)
    {
        NormalizeRange(ref fromRound, ref toRound);

        var rounds = new List<CartolaBackfillRoundResult>();
        var successfulRounds = 0;
        var partialRounds = 0;
        var failedRounds = 0;

        for (var round = fromRound; round <= toRound; round++)
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

                rounds.Add(new CartolaBackfillRoundResult
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
                rounds.Add(new CartolaBackfillRoundResult
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
            FromRound = fromRound,
            ToRound = toRound,
            RequestedRounds = toRound - fromRound + 1,
            SuccessfulRounds = successfulRounds,
            PartialRounds = partialRounds,
            FailedRounds = failedRounds,
            Rounds = rounds,
        };
    }

    public async Task<GatoMestreBackfillResult> BackfillGatoMestreAsync(
        int season,
        int fromRound,
        int toRound,
        CancellationToken cancellationToken = default)
    {
        NormalizeRange(ref fromRound, ref toRound);

        var rounds = new List<GatoMestreBackfillRoundResult>();
        var successfulRounds = 0;
        var partialRounds = 0;
        var failedRounds = 0;

        for (var round = fromRound; round <= toRound; round++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var result = await gatoMestreSyncService.SyncRoundAsync(season, round, cancellationToken);

                if (result.IsPartialSync)
                {
                    partialRounds++;
                }
                else
                {
                    successfulRounds++;
                }

                rounds.Add(new GatoMestreBackfillRoundResult
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
                rounds.Add(new GatoMestreBackfillRoundResult
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
            FromRound = fromRound,
            ToRound = toRound,
            RequestedRounds = toRound - fromRound + 1,
            SuccessfulRounds = successfulRounds,
            PartialRounds = partialRounds,
            FailedRounds = failedRounds,
            Rounds = rounds,
        };
    }

    private static void NormalizeRange(ref int fromRound, ref int toRound)
    {
        if (fromRound <= 0 || toRound <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fromRound), "Round numbers must be greater than zero.");
        }

        if (fromRound > toRound)
        {
            (fromRound, toRound) = (toRound, fromRound);
        }
    }
}
