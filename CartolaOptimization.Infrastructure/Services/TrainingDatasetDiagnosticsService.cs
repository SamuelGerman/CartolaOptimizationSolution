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

    public async Task<TrainingAthleteDatasetResult> GetAthleteOnlyDatasetAsync(
        int? fromRound = null,
        int? toRound = null,
        int take = 1000,
        CancellationToken cancellationToken = default)
    {
        NormalizeOptionalRange(ref fromRound, ref toRound);
        take = Math.Clamp(take, 1, 5000);

        var query = ApplyAthleteTrainingWindow(
            dbContext.AthleteRounds
                .AsNoTracking()
                .Include(item => item.Athlete)
                .Include(item => item.Club)
                .Include(item => item.Position)
                .Include(item => item.Status),
            fromRound,
            toRound);

        var totalHistoricalRowsConsidered = await query.CountAsync(cancellationToken);
        var distinctAthletes = await query
            .Select(item => item.AthleteId)
            .Distinct()
            .CountAsync(cancellationToken);

        var rows = await query
            .OrderBy(item => item.RoundId)
            .ThenBy(item => item.Athlete.CartolaId)
            .Take(take)
            .Select(item => new TrainingAthleteDatasetRow
            {
                AthleteRoundId = item.Id,
                RoundId = item.RoundId,
                AthleteCartolaId = item.Athlete.CartolaId,
                AthleteName = item.Athlete.Name,
                AthleteNickname = item.Athlete.Nickname,
                ClubCartolaId = item.Club.CartolaId,
                ClubName = item.Club.Nickname,
                PositionCartolaId = item.Position.CartolaId,
                PositionName = item.Position.Name,
                StatusCartolaId = item.Status.CartolaId,
                StatusName = item.Status.Name,
                GamesNum = item.GamesNum,
                HomeGamesNum = item.HomeGamesNum,
                AwayGamesNum = item.AwayGamesNum,
                MovingAverage = item.MovingAverage,
                AverageScore = item.AverageScoreNum,
                HomeAverageScore = item.HomeAverageScore,
                AwayAverageScore = item.AwayAverageScore,
                PriceNum = item.PriceNum,
                VariationNum = item.VariationNum,
                TargetScore = item.ScoreNum,
                TargetWasInGame = item.WasInGame,
            })
            .ToListAsync(cancellationToken);

        return new TrainingAthleteDatasetResult
        {
            FromRound = fromRound,
            ToRound = toRound,
            RequestedTake = take,
            TotalHistoricalRowsConsidered = totalHistoricalRowsConsidered,
            TotalExportableRows = totalHistoricalRowsConsidered,
            ExcludedRows = 0,
            DistinctAthletes = distinctAthletes,
            ReturnedRows = rows.Count,
            EligibilityRule = "Historical closed athlete rounds with structural references. Club-level context is optional.",
            Rows = rows,
        };
    }

    public async Task<TrainingFullContextDatasetResult> GetFullContextDatasetAsync(
        int? fromRound = null,
        int? toRound = null,
        int take = 1000,
        CancellationToken cancellationToken = default)
    {
        NormalizeOptionalRange(ref fromRound, ref toRound);
        take = Math.Clamp(take, 1, 5000);

        var athleteRounds = await ApplyAthleteTrainingWindow(
                dbContext.AthleteRounds
                    .AsNoTracking()
                    .Include(item => item.Athlete)
                    .Include(item => item.Club)
                    .Include(item => item.Position)
                    .Include(item => item.Status),
                fromRound,
                toRound)
            .OrderBy(item => item.RoundId)
            .ThenBy(item => item.Athlete.CartolaId)
            .ToListAsync(cancellationToken);

        var totalHistoricalRowsConsidered = athleteRounds.Count;
        if (athleteRounds.Count == 0)
        {
            return new TrainingFullContextDatasetResult
            {
                FromRound = fromRound,
                ToRound = toRound,
                RequestedTake = take,
                EligibilityRule = "Historical closed athlete rounds with structural references, valid Cartola match context, complete club-round enrichment for both clubs and position-level opponent context.",
            };
        }

        var rounds = athleteRounds
            .Select(item => item.RoundId)
            .Distinct()
            .ToArray();

        var clubRounds = await dbContext.ClubRounds
            .AsNoTracking()
            .Include(item => item.Club)
            .Include(item => item.PositionStats)
            .Where(item => rounds.Contains(item.Round))
            .ToListAsync(cancellationToken);

        var completeClubRounds = clubRounds
            .Where(item => item.PositionStats.Count > 0)
            .ToDictionary(item => (item.Round, item.ClubId));

        var matches = await dbContext.Matches
            .AsNoTracking()
            .Include(item => item.HomeClub)
            .Include(item => item.AwayClub)
            .Where(item => rounds.Contains(item.Round) && item.ValidForCartola)
            .ToListAsync(cancellationToken);

        var matchByRoundAndClubId = new Dictionary<(int Round, int ClubId), Match>();
        foreach (var match in matches)
        {
            matchByRoundAndClubId[(match.Round, match.HomeClubId)] = match;
            matchByRoundAndClubId[(match.Round, match.AwayClubId)] = match;
        }

        var exportableRows = new List<TrainingFullContextDatasetRow>(athleteRounds.Count);

        foreach (var athleteRound in athleteRounds)
        {
            if (!completeClubRounds.TryGetValue((athleteRound.RoundId, athleteRound.ClubId), out var clubRound))
            {
                continue;
            }

            if (!matchByRoundAndClubId.TryGetValue((athleteRound.RoundId, athleteRound.ClubId), out var match))
            {
                continue;
            }

            var isHomeMatch = match.HomeClubId == athleteRound.ClubId;
            var opponentClubId = isHomeMatch ? match.AwayClubId : match.HomeClubId;

            if (!completeClubRounds.TryGetValue((athleteRound.RoundId, opponentClubId), out var opponentClubRound))
            {
                continue;
            }

            var clubPositionStats = clubRound.PositionStats.FirstOrDefault(item => item.PositionId == athleteRound.PositionId);
            var opponentPositionStats = opponentClubRound.PositionStats.FirstOrDefault(item => item.PositionId == athleteRound.PositionId);

            if (clubPositionStats is null || opponentPositionStats is null)
            {
                continue;
            }

            var clubTablePosition = isHomeMatch ? match.HomeClubPosition : match.AwayClubPosition;
            var opponentTablePosition = isHomeMatch ? match.AwayClubPosition : match.HomeClubPosition;
            var clubRecentForm = isHomeMatch ? match.HomeForm : match.AwayForm;
            var opponentRecentForm = isHomeMatch ? match.AwayForm : match.HomeForm;
            var opponentClubCartolaId = isHomeMatch ? match.AwayClub.CartolaId : match.HomeClub.CartolaId;
            var opponentClubName = isHomeMatch ? match.AwayClub.Nickname : match.HomeClub.Nickname;

            exportableRows.Add(new TrainingFullContextDatasetRow
            {
                AthleteRoundId = athleteRound.Id,
                RoundId = athleteRound.RoundId,
                AthleteCartolaId = athleteRound.Athlete.CartolaId,
                AthleteName = athleteRound.Athlete.Name,
                AthleteNickname = athleteRound.Athlete.Nickname,
                ClubCartolaId = athleteRound.Club.CartolaId,
                ClubName = athleteRound.Club.Nickname,
                PositionCartolaId = athleteRound.Position.CartolaId,
                PositionName = athleteRound.Position.Name,
                StatusCartolaId = athleteRound.Status.CartolaId,
                StatusName = athleteRound.Status.Name,
                GamesNum = athleteRound.GamesNum,
                HomeGamesNum = athleteRound.HomeGamesNum,
                AwayGamesNum = athleteRound.AwayGamesNum,
                MovingAverage = athleteRound.MovingAverage,
                AverageScore = athleteRound.AverageScoreNum,
                HomeAverageScore = athleteRound.HomeAverageScore,
                AwayAverageScore = athleteRound.AwayAverageScore,
                PriceNum = athleteRound.PriceNum,
                VariationNum = athleteRound.VariationNum,
                IsHomeMatch = isHomeMatch,
                ClubTablePosition = clubTablePosition,
                OpponentClubCartolaId = opponentClubCartolaId,
                OpponentClubName = opponentClubName,
                OpponentClubTablePosition = opponentTablePosition,
                ClubRecentForm = clubRecentForm,
                OpponentRecentForm = opponentRecentForm,
                ClubWinProbability = clubRound.WinProbability,
                ClubDrawProbability = clubRound.DrawProbability,
                ClubLoseProbability = clubRound.LoseProbability,
                ClubOverallAverage = clubRound.OverallAverage,
                ClubMovingAverage = clubRound.MovingAverage,
                ClubExpectedPoints = clubRound.ExpectedPoints,
                ClubPositionPointsConcededAverage = clubPositionStats.PointsConcededAverage,
                ClubPositionPointsConqueredAverage = clubPositionStats.PointsConqueredAverage,
                OpponentWinProbability = opponentClubRound.WinProbability,
                OpponentDrawProbability = opponentClubRound.DrawProbability,
                OpponentLoseProbability = opponentClubRound.LoseProbability,
                OpponentOverallAverage = opponentClubRound.OverallAverage,
                OpponentMovingAverage = opponentClubRound.MovingAverage,
                OpponentExpectedPoints = opponentClubRound.ExpectedPoints,
                OpponentPositionPointsConcededAverage = opponentPositionStats.PointsConcededAverage,
                OpponentPositionPointsConqueredAverage = opponentPositionStats.PointsConqueredAverage,
                TargetScore = athleteRound.ScoreNum,
                TargetWasInGame = athleteRound.WasInGame,
            });
        }

        var returnedRows = exportableRows
            .Take(take)
            .ToList();

        return new TrainingFullContextDatasetResult
        {
            FromRound = fromRound,
            ToRound = toRound,
            RequestedTake = take,
            TotalHistoricalRowsConsidered = totalHistoricalRowsConsidered,
            TotalExportableRows = exportableRows.Count,
            ExcludedRows = totalHistoricalRowsConsidered - exportableRows.Count,
            DistinctAthletes = exportableRows
                .Select(item => item.AthleteCartolaId)
                .Distinct()
                .Count(),
            ReturnedRows = returnedRows.Count,
            EligibilityRule = "Historical closed athlete rounds with structural references, valid Cartola match context, complete club-round enrichment for both clubs and position-level opponent context.",
            Rows = returnedRows,
        };
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

    private static IQueryable<AthleteRound> ApplyAthleteTrainingWindow(
        IQueryable<AthleteRound> query,
        int? fromRound,
        int? toRound)
    {
        return ApplyAthleteRoundWindow(query, fromRound, toRound)
            .Where(item =>
                item.ClubId != 0 &&
                item.PositionId != 0 &&
                item.StatusId != 0 &&
                item.DataPhase == RoundDataPhaseValues.HistoricalClosed);
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
