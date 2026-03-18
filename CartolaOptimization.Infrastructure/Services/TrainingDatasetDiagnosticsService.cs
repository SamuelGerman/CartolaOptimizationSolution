using CartolaOptimization.Application.Interfaces;
using CartolaOptimization.Application.Models;
using CartolaOptimization.Domain.Constants;
using CartolaOptimization.Domain.Entities;
using CartolaOptimization.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CartolaOptimization.Infrastructure.Services;

/// <summary>
/// Provides administrative read access to the persisted training dataset readiness state.
/// </summary>
public sealed class TrainingDatasetDiagnosticsService(AppDbContext dbContext) : ITrainingDatasetDiagnosticsService
{
    public async Task<TrainingDatasetCoverageSummary> GetCoverageSummaryAsync(
        int? fromRound = null,
        int? toRound = null,
        CancellationToken cancellationToken = default)
    {
        NormalizeOptionalRange(ref fromRound, ref toRound);

        var athleteQuery = ApplyAthleteRoundWindow(dbContext.AthleteRounds.AsNoTracking(), fromRound, toRound);
        var clubQuery = ApplyClubRoundWindow(dbContext.ClubRounds.AsNoTracking(), fromRound, toRound);

        var athleteRoundsTotal = await athleteQuery.CountAsync(cancellationToken);
        var athleteRoundsEligible = await athleteQuery.CountAsync(item => item.IsTrainingEligible, cancellationToken);
        var athleteRoundsComplete = await athleteQuery.CountAsync(item => item.TrainingReadinessStatus == TrainingReadinessStatusValues.Complete, cancellationToken);
        var athleteRoundsPartial = await athleteQuery.CountAsync(item => item.TrainingReadinessStatus == TrainingReadinessStatusValues.Partial, cancellationToken);
        var athleteRoundsUnfit = await athleteQuery.CountAsync(item => item.TrainingReadinessStatus == TrainingReadinessStatusValues.UnfitForTraining, cancellationToken);
        var athleteRoundsHistoricalClosed = await athleteQuery.CountAsync(item => item.DataPhase == RoundDataPhaseValues.HistoricalClosed, cancellationToken);
        var athleteRoundsCurrentOpen = await athleteQuery.CountAsync(item => item.DataPhase == RoundDataPhaseValues.CurrentOpen, cancellationToken);
        var athleteRoundsUnknownPhase = await athleteQuery.CountAsync(item => item.DataPhase == RoundDataPhaseValues.Unknown, cancellationToken);

        var clubRoundsTotal = await clubQuery.CountAsync(cancellationToken);
        var clubRoundsEligible = await clubQuery.CountAsync(item => item.IsTrainingEligible, cancellationToken);
        var clubRoundsComplete = await clubQuery.CountAsync(item => item.TrainingReadinessStatus == TrainingReadinessStatusValues.Complete, cancellationToken);
        var clubRoundsPartial = await clubQuery.CountAsync(item => item.TrainingReadinessStatus == TrainingReadinessStatusValues.Partial, cancellationToken);
        var clubRoundsUnfit = await clubQuery.CountAsync(item => item.TrainingReadinessStatus == TrainingReadinessStatusValues.UnfitForTraining, cancellationToken);

        var athleteTopBlockingReasons = await athleteQuery
            .Where(item => !item.IsTrainingEligible && item.TrainingReadinessReason != string.Empty)
            .GroupBy(item => item.TrainingReadinessReason)
            .OrderByDescending(group => group.Count())
            .Take(5)
            .Select(group => new TrainingReadinessReasonCountItem
            {
                Reason = group.Key,
                Count = group.Count(),
            })
            .ToListAsync(cancellationToken);

        var clubTopBlockingReasons = await clubQuery
            .Where(item => !item.IsTrainingEligible && item.TrainingReadinessReason != string.Empty)
            .GroupBy(item => item.TrainingReadinessReason)
            .OrderByDescending(group => group.Count())
            .Take(5)
            .Select(group => new TrainingReadinessReasonCountItem
            {
                Reason = group.Key,
                Count = group.Count(),
            })
            .ToListAsync(cancellationToken);

        return new TrainingDatasetCoverageSummary
        {
            FromRound = fromRound,
            ToRound = toRound,
            AthleteRoundsTotal = athleteRoundsTotal,
            AthleteRoundsEligible = athleteRoundsEligible,
            AthleteRoundsIneligible = athleteRoundsTotal - athleteRoundsEligible,
            AthleteRoundsComplete = athleteRoundsComplete,
            AthleteRoundsPartial = athleteRoundsPartial,
            AthleteRoundsUnfit = athleteRoundsUnfit,
            AthleteRoundsHistoricalClosed = athleteRoundsHistoricalClosed,
            AthleteRoundsCurrentOpen = athleteRoundsCurrentOpen,
            AthleteRoundsUnknownPhase = athleteRoundsUnknownPhase,
            AthleteEligibilityRate = CalculateRate(athleteRoundsEligible, athleteRoundsTotal),
            ClubRoundsTotal = clubRoundsTotal,
            ClubRoundsEligible = clubRoundsEligible,
            ClubRoundsIneligible = clubRoundsTotal - clubRoundsEligible,
            ClubRoundsComplete = clubRoundsComplete,
            ClubRoundsPartial = clubRoundsPartial,
            ClubRoundsUnfit = clubRoundsUnfit,
            ClubEligibilityRate = CalculateRate(clubRoundsEligible, clubRoundsTotal),
            AthleteTopBlockingReasons = athleteTopBlockingReasons,
            ClubTopBlockingReasons = clubTopBlockingReasons,
        };
    }

    public async Task<IReadOnlyCollection<TrainingAthleteRoundListItem>> GetAthleteRoundsAsync(
        int? fromRound = null,
        int? toRound = null,
        string? readinessStatus = null,
        string? dataPhase = null,
        bool? isTrainingEligible = null,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        NormalizeOptionalRange(ref fromRound, ref toRound);
        take = Math.Clamp(take, 1, 200);

        var query = ApplyAthleteRoundWindow(dbContext.AthleteRounds.AsNoTracking(), fromRound, toRound);

        if (!string.IsNullOrWhiteSpace(readinessStatus))
        {
            query = query.Where(item => item.TrainingReadinessStatus == readinessStatus);
        }

        if (!string.IsNullOrWhiteSpace(dataPhase))
        {
            query = query.Where(item => item.DataPhase == dataPhase);
        }

        if (isTrainingEligible.HasValue)
        {
            query = query.Where(item => item.IsTrainingEligible == isTrainingEligible.Value);
        }

        return await query
            .OrderByDescending(item => item.RoundId)
            .ThenBy(item => item.TrainingReadinessStatus)
            .ThenByDescending(item => item.ScoreNum)
            .Take(take)
            .Select(item => new TrainingAthleteRoundListItem
            {
                Id = item.Id,
                RoundId = item.RoundId,
                AthleteCartolaId = item.Athlete.CartolaId,
                AthleteName = item.Athlete.Name,
                AthleteNickname = item.Athlete.Nickname,
                ClubCartolaId = item.Club.CartolaId,
                ClubName = item.Club.Nickname,
                PositionName = item.Position.Name,
                ScoreNum = item.ScoreNum,
                PriceNum = item.PriceNum,
                WasInGame = item.WasInGame,
                DataPhase = item.DataPhase,
                TrainingReadinessStatus = item.TrainingReadinessStatus,
                TrainingReadinessReason = item.TrainingReadinessReason,
                IsTrainingEligible = item.IsTrainingEligible,
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<TrainingClubRoundListItem>> GetClubRoundsAsync(
        int? fromRound = null,
        int? toRound = null,
        string? readinessStatus = null,
        bool? isTrainingEligible = null,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        NormalizeOptionalRange(ref fromRound, ref toRound);
        take = Math.Clamp(take, 1, 200);

        var query = ApplyClubRoundWindow(dbContext.ClubRounds.AsNoTracking(), fromRound, toRound);

        if (!string.IsNullOrWhiteSpace(readinessStatus))
        {
            query = query.Where(item => item.TrainingReadinessStatus == readinessStatus);
        }

        if (isTrainingEligible.HasValue)
        {
            query = query.Where(item => item.IsTrainingEligible == isTrainingEligible.Value);
        }

        return await query
            .OrderByDescending(item => item.Round)
            .ThenBy(item => item.TrainingReadinessStatus)
            .ThenBy(item => item.Club.Nickname)
            .Take(take)
            .Select(item => new TrainingClubRoundListItem
            {
                Id = item.Id,
                Round = item.Round,
                ClubCartolaId = item.Club.CartolaId,
                ClubName = item.Club.Nickname,
                WinProbability = item.WinProbability,
                DrawProbability = item.DrawProbability,
                LoseProbability = item.LoseProbability,
                OverallAverage = item.OverallAverage,
                MovingAverage = item.MovingAverage,
                ExpectedPoints = item.ExpectedPoints,
                PositionStatsCount = item.PositionStats.Count,
                TrainingReadinessStatus = item.TrainingReadinessStatus,
                TrainingReadinessReason = item.TrainingReadinessReason,
                IsTrainingEligible = item.IsTrainingEligible,
            })
            .ToListAsync(cancellationToken);
    }

    private static IQueryable<AthleteRound> ApplyAthleteRoundWindow(
        IQueryable<AthleteRound> query,
        int? fromRound,
        int? toRound)
    {
        if (fromRound.HasValue)
        {
            query = query.Where(item => item.RoundId >= fromRound.Value);
        }

        if (toRound.HasValue)
        {
            query = query.Where(item => item.RoundId <= toRound.Value);
        }

        return query;
    }

    private static IQueryable<ClubRound> ApplyClubRoundWindow(
        IQueryable<ClubRound> query,
        int? fromRound,
        int? toRound)
    {
        if (fromRound.HasValue)
        {
            query = query.Where(item => item.Round >= fromRound.Value);
        }

        if (toRound.HasValue)
        {
            query = query.Where(item => item.Round <= toRound.Value);
        }

        return query;
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

    private static decimal CalculateRate(int numerator, int denominator)
    {
        if (denominator == 0)
        {
            return 0m;
        }

        return Math.Round((decimal)numerator / denominator * 100m, 2, MidpointRounding.AwayFromZero);
    }
}
