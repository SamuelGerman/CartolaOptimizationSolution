using CartolaOptimization.Application.Models;
using CartolaOptimization.Domain.Constants;
using CartolaOptimization.Domain.Entities;
using CartolaOptimization.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CartolaOptimization.Infrastructure.Services;

internal static class TrainingFullContextLocalDatasetBuilder
{
    internal static async Task<List<TrainingFullContextDatasetRow>> BuildRowsAsync(
        AppDbContext dbContext,
        IReadOnlyList<AthleteRound> athleteRounds,
        CancellationToken cancellationToken)
    {
        if (athleteRounds.Count == 0)
        {
            return [];
        }

        var maxRound = athleteRounds.Max(item => item.RoundId);
        var matches = await dbContext.Matches
            .AsNoTracking()
            .Include(item => item.HomeClub)
            .Include(item => item.AwayClub)
            .Where(item => item.ValidForCartola && item.Round <= maxRound)
            .ToListAsync(cancellationToken);

        var matchByRoundAndClubId = new Dictionary<(int Round, int ClubId), Match>();
        var clubHistoryByClubId = new Dictionary<int, List<ClubHistoryPoint>>();
        foreach (var match in matches)
        {
            matchByRoundAndClubId[(match.Round, match.HomeClubId)] = match;
            matchByRoundAndClubId[(match.Round, match.AwayClubId)] = match;

            AddClubHistory(clubHistoryByClubId, match.HomeClubId, new ClubHistoryPoint(match.Round, true, ResolveMatchPoints(match.HomeScore, match.AwayScore), match.HomeScore ?? 0, match.AwayScore ?? 0));
            AddClubHistory(clubHistoryByClubId, match.AwayClubId, new ClubHistoryPoint(match.Round, false, ResolveMatchPoints(match.AwayScore, match.HomeScore), match.AwayScore ?? 0, match.HomeScore ?? 0));
        }

        var historicalAthleteRounds = await dbContext.AthleteRounds
            .AsNoTracking()
            .Include(item => item.Scouts)
            .Where(item =>
                item.DataPhase == RoundDataPhaseValues.HistoricalClosed &&
                item.ClubId != 0 &&
                item.PositionId != 0 &&
                item.StatusId != 0 &&
                item.RoundId <= maxRound)
            .OrderByDescending(item => item.RoundId)
            .ThenByDescending(item => item.Id)
            .ToListAsync(cancellationToken);

        var athleteHistoryByAthleteId = new Dictionary<int, List<AthleteObservation>>();
        var positionHistoryByPositionId = new Dictionary<int, List<AthleteObservation>>();
        var globalAthleteHistory = new List<AthleteObservation>();
        var clubFantasyFor = new Dictionary<(int ClubId, int Round), decimal>();
        var clubFantasyAgainst = new Dictionary<(int ClubId, int Round), decimal>();
        var positionFantasyFor = new Dictionary<(int ClubId, int PositionId, int Round), decimal>();
        var positionFantasyAgainst = new Dictionary<(int ClubId, int PositionId, int Round), decimal>();
        var priceSumByPositionAndRound = new Dictionary<(int PositionId, int Round), decimal>();
        var priceCountByPositionAndRound = new Dictionary<(int PositionId, int Round), int>();
        var offensiveScoutsConcededByClubAndRound = new Dictionary<(int ClubId, int Round), decimal>();
        var defensiveScoutsConcededByClubAndRound = new Dictionary<(int ClubId, int Round), decimal>();
        var negativeScoutsConcededByClubAndRound = new Dictionary<(int ClubId, int Round), decimal>();
        var balanceScoutsConcededByClubAndRound = new Dictionary<(int ClubId, int Round), decimal>();
        var goalsScoutsConcededByClubAndRound = new Dictionary<(int ClubId, int Round), decimal>();
        var assistsScoutsConcededByClubAndRound = new Dictionary<(int ClubId, int Round), decimal>();
        var tacklesScoutsConcededByClubAndRound = new Dictionary<(int ClubId, int Round), decimal>();
        var offensiveScoutsConcededByClubPositionAndRound = new Dictionary<(int ClubId, int PositionId, int Round), decimal>();
        var defensiveScoutsConcededByClubPositionAndRound = new Dictionary<(int ClubId, int PositionId, int Round), decimal>();
        var negativeScoutsConcededByClubPositionAndRound = new Dictionary<(int ClubId, int PositionId, int Round), decimal>();
        var balanceScoutsConcededByClubPositionAndRound = new Dictionary<(int ClubId, int PositionId, int Round), decimal>();
        var goalsScoutsConcededByClubPositionAndRound = new Dictionary<(int ClubId, int PositionId, int Round), decimal>();
        var assistsScoutsConcededByClubPositionAndRound = new Dictionary<(int ClubId, int PositionId, int Round), decimal>();
        var tacklesScoutsConcededByClubPositionAndRound = new Dictionary<(int ClubId, int PositionId, int Round), decimal>();

        foreach (var athleteRound in historicalAthleteRounds)
        {
            var scouts = athleteRound.Scouts.ToDictionary(item => item.ScoutKey, item => item.Valor, StringComparer.OrdinalIgnoreCase);
            var isHome = matchByRoundAndClubId.TryGetValue((athleteRound.RoundId, athleteRound.ClubId), out var match)
                ? (bool?)(match.HomeClubId == athleteRound.ClubId)
                : null;

            var observation = new AthleteObservation(
                athleteRound.RoundId,
                athleteRound.PositionId,
                isHome,
                athleteRound.StatusId,
                athleteRound.WasInGame,
                athleteRound.PriceNum,
                athleteRound.ScoreNum,
                Scout(scouts, "G") + Scout(scouts, "A") + Scout(scouts, "FS") + Scout(scouts, "FD") + Scout(scouts, "FF"),
                Scout(scouts, "DS") + Scout(scouts, "SG"),
                Scout(scouts, "GC") + Scout(scouts, "CA") + Scout(scouts, "CV") + Scout(scouts, "PP"),
                Scout(scouts, "G"),
                Scout(scouts, "A"),
                Scout(scouts, "DS"),
                Scout(scouts, "SG") > 0m ? 1m : 0m);

            AddAthleteObservation(athleteHistoryByAthleteId, athleteRound.AthleteId, observation);
            AddAthleteObservation(positionHistoryByPositionId, athleteRound.PositionId, observation);
            globalAthleteHistory.Add(observation);

            if (match is null)
            {
                continue;
            }

            var opponentClubId = match.HomeClubId == athleteRound.ClubId ? match.AwayClubId : match.HomeClubId;
            Accumulate(clubFantasyFor, (athleteRound.ClubId, athleteRound.RoundId), athleteRound.ScoreNum);
            Accumulate(clubFantasyAgainst, (opponentClubId, athleteRound.RoundId), athleteRound.ScoreNum);
            Accumulate(positionFantasyFor, (athleteRound.ClubId, athleteRound.PositionId, athleteRound.RoundId), athleteRound.ScoreNum);
            Accumulate(positionFantasyAgainst, (opponentClubId, athleteRound.PositionId, athleteRound.RoundId), athleteRound.ScoreNum);
            Accumulate(priceSumByPositionAndRound, (athleteRound.PositionId, athleteRound.RoundId), athleteRound.PriceNum);
            Accumulate(priceCountByPositionAndRound, (athleteRound.PositionId, athleteRound.RoundId), 1);
            Accumulate(offensiveScoutsConcededByClubAndRound, (opponentClubId, athleteRound.RoundId), Scout(scouts, "G") + Scout(scouts, "A") + Scout(scouts, "FS") + Scout(scouts, "FD") + Scout(scouts, "FF"));
            Accumulate(defensiveScoutsConcededByClubAndRound, (opponentClubId, athleteRound.RoundId), Scout(scouts, "DS") + Scout(scouts, "SG"));
            Accumulate(negativeScoutsConcededByClubAndRound, (opponentClubId, athleteRound.RoundId), Scout(scouts, "GC") + Scout(scouts, "CA") + Scout(scouts, "CV") + Scout(scouts, "PP"));
            Accumulate(balanceScoutsConcededByClubAndRound, (opponentClubId, athleteRound.RoundId), (Scout(scouts, "G") + Scout(scouts, "A") + Scout(scouts, "FS") + Scout(scouts, "FD") + Scout(scouts, "FF") + Scout(scouts, "DS") + Scout(scouts, "SG")) - (Scout(scouts, "GC") + Scout(scouts, "CA") + Scout(scouts, "CV") + Scout(scouts, "PP")));
            Accumulate(goalsScoutsConcededByClubAndRound, (opponentClubId, athleteRound.RoundId), Scout(scouts, "G"));
            Accumulate(assistsScoutsConcededByClubAndRound, (opponentClubId, athleteRound.RoundId), Scout(scouts, "A"));
            Accumulate(tacklesScoutsConcededByClubAndRound, (opponentClubId, athleteRound.RoundId), Scout(scouts, "DS"));
            Accumulate(offensiveScoutsConcededByClubPositionAndRound, (opponentClubId, athleteRound.PositionId, athleteRound.RoundId), Scout(scouts, "G") + Scout(scouts, "A") + Scout(scouts, "FS") + Scout(scouts, "FD") + Scout(scouts, "FF"));
            Accumulate(defensiveScoutsConcededByClubPositionAndRound, (opponentClubId, athleteRound.PositionId, athleteRound.RoundId), Scout(scouts, "DS") + Scout(scouts, "SG"));
            Accumulate(negativeScoutsConcededByClubPositionAndRound, (opponentClubId, athleteRound.PositionId, athleteRound.RoundId), Scout(scouts, "GC") + Scout(scouts, "CA") + Scout(scouts, "CV") + Scout(scouts, "PP"));
            Accumulate(balanceScoutsConcededByClubPositionAndRound, (opponentClubId, athleteRound.PositionId, athleteRound.RoundId), (Scout(scouts, "G") + Scout(scouts, "A") + Scout(scouts, "FS") + Scout(scouts, "FD") + Scout(scouts, "FF") + Scout(scouts, "DS") + Scout(scouts, "SG")) - (Scout(scouts, "GC") + Scout(scouts, "CA") + Scout(scouts, "CV") + Scout(scouts, "PP")));
            Accumulate(goalsScoutsConcededByClubPositionAndRound, (opponentClubId, athleteRound.PositionId, athleteRound.RoundId), Scout(scouts, "G"));
            Accumulate(assistsScoutsConcededByClubPositionAndRound, (opponentClubId, athleteRound.PositionId, athleteRound.RoundId), Scout(scouts, "A"));
            Accumulate(tacklesScoutsConcededByClubPositionAndRound, (opponentClubId, athleteRound.PositionId, athleteRound.RoundId), Scout(scouts, "DS"));
        }

        var averagePriceByPositionAndRound = priceSumByPositionAndRound.ToDictionary(
            item => item.Key,
            item => item.Value / priceCountByPositionAndRound[item.Key]);

        var rows = new List<TrainingFullContextDatasetRow>(athleteRounds.Count);
        foreach (var athleteRound in athleteRounds)
        {
            if (!matchByRoundAndClubId.TryGetValue((athleteRound.RoundId, athleteRound.ClubId), out var targetMatch))
            {
                continue;
            }

            var isHomeMatch = targetMatch.HomeClubId == athleteRound.ClubId;
            var opponentClub = isHomeMatch ? targetMatch.AwayClub : targetMatch.HomeClub;
            var athleteHistory = BuildAthleteSnapshot(athleteHistoryByAthleteId, positionHistoryByPositionId, globalAthleteHistory, averagePriceByPositionAndRound, athleteRound.AthleteId, athleteRound.StatusId, athleteRound.PriceNum, athleteRound.PositionId, athleteRound.RoundId, isHomeMatch);
            var clubContext = BuildClubSnapshot(clubHistoryByClubId, clubFantasyFor, clubFantasyAgainst, offensiveScoutsConcededByClubAndRound, defensiveScoutsConcededByClubAndRound, negativeScoutsConcededByClubAndRound, balanceScoutsConcededByClubAndRound, goalsScoutsConcededByClubAndRound, assistsScoutsConcededByClubAndRound, tacklesScoutsConcededByClubAndRound, positionFantasyFor, positionFantasyAgainst, offensiveScoutsConcededByClubPositionAndRound, defensiveScoutsConcededByClubPositionAndRound, negativeScoutsConcededByClubPositionAndRound, balanceScoutsConcededByClubPositionAndRound, goalsScoutsConcededByClubPositionAndRound, assistsScoutsConcededByClubPositionAndRound, tacklesScoutsConcededByClubPositionAndRound, athleteRound.ClubId, athleteRound.PositionId, athleteRound.RoundId, isHomeMatch);
            var opponentContext = BuildClubSnapshot(clubHistoryByClubId, clubFantasyFor, clubFantasyAgainst, offensiveScoutsConcededByClubAndRound, defensiveScoutsConcededByClubAndRound, negativeScoutsConcededByClubAndRound, balanceScoutsConcededByClubAndRound, goalsScoutsConcededByClubAndRound, assistsScoutsConcededByClubAndRound, tacklesScoutsConcededByClubAndRound, positionFantasyFor, positionFantasyAgainst, offensiveScoutsConcededByClubPositionAndRound, defensiveScoutsConcededByClubPositionAndRound, negativeScoutsConcededByClubPositionAndRound, balanceScoutsConcededByClubPositionAndRound, goalsScoutsConcededByClubPositionAndRound, assistsScoutsConcededByClubPositionAndRound, tacklesScoutsConcededByClubPositionAndRound, opponentClub.Id, athleteRound.PositionId, athleteRound.RoundId, !isHomeMatch);
            var clubRecentForm = isHomeMatch ? targetMatch.HomeForm : targetMatch.AwayForm;
            var opponentRecentForm = isHomeMatch ? targetMatch.AwayForm : targetMatch.HomeForm;

            rows.Add(new TrainingFullContextDatasetRow
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
                GamesNum = (int)athleteHistory.GamesNumPrior,
                HomeGamesNum = (int)athleteHistory.HomeGamesNumPrior,
                AwayGamesNum = (int)athleteHistory.AwayGamesNumPrior,
                GamesAtVenueNum = (int)athleteHistory.VenueGamesNumPrior,
                PlayedMatchesSampleSize = (int)athleteHistory.PlayedMatchesSampleSize,
                PlayedMatchesSampleSizeLast5 = (int)athleteHistory.PlayedMatchesSampleSizeLast5,
                ObservationCountLast3 = (int)athleteHistory.ObservationCountLast3,
                ObservationCountLast5 = (int)athleteHistory.ObservationCountLast5,
                PreviousObservationCountLast3 = (int)athleteHistory.PreviousObservationCountLast3,
                HomeObservationCountLast5 = (int)athleteHistory.HomeObservationCountLast5,
                AwayObservationCountLast5 = (int)athleteHistory.AwayObservationCountLast5,
                VenueObservationCountLast5 = (int)athleteHistory.VenueObservationCountLast5,
                StatusMatchedObservationCountLast5 = (int)athleteHistory.StatusMatchedObservationCountLast5,
                HistoryCoverageLast3 = athleteHistory.HistoryCoverageLast3,
                HistoryCoverageLast5 = athleteHistory.HistoryCoverageLast5,
                HomeHistoryCoverageLast5 = athleteHistory.HomeHistoryCoverageLast5,
                AwayHistoryCoverageLast5 = athleteHistory.AwayHistoryCoverageLast5,
                VenueHistoryCoverageLast5 = athleteHistory.VenueHistoryCoverageLast5,
                CurrentStatusCoverageLast5 = athleteHistory.CurrentStatusCoverageLast5,
                IsColdStartAthlete = athleteHistory.IsColdStartAthlete > 0m,
                MovingAverage = athleteHistory.MovingAveragePrior,
                AverageScore = athleteHistory.AverageScorePrior,
                HomeAverageScore = athleteHistory.HomeAverageScorePrior,
                AwayAverageScore = athleteHistory.AwayAverageScorePrior,
                ContextualAverageScore = athleteHistory.VenueAverageScorePrior,
                PriceNum = athleteRound.PriceNum,
                VariationNum = athleteRound.VariationNum,
                HasAthleteContext = athleteRound.Context is not null,
                TeamAverageScore = athleteRound.Context?.TeamAverageScore ?? 0m,
                TeamHomeAverageCurrent = athleteRound.Context?.TeamHomeAverageCurrent ?? 0m,
                TeamHomeAveragePrevious = athleteRound.Context?.TeamHomeAveragePrevious ?? 0m,
                TeamAwayAverageCurrent = athleteRound.Context?.TeamAwayAverageCurrent ?? 0m,
                TeamAwayAveragePrevious = athleteRound.Context?.TeamAwayAveragePrevious ?? 0m,
                TeamVenueAverageCurrent = isHomeMatch ? athleteRound.Context?.TeamHomeAverageCurrent ?? 0m : athleteRound.Context?.TeamAwayAverageCurrent ?? 0m,
                TeamVenueAveragePrevious = isHomeMatch ? athleteRound.Context?.TeamHomeAveragePrevious ?? 0m : athleteRound.Context?.TeamAwayAveragePrevious ?? 0m,
                PointsPerCartoletaCurrent = athleteRound.Context?.PointsPerCartoletaCurrent ?? 0m,
                PointsPerCartoletaPrevious = athleteRound.Context?.PointsPerCartoletaPrevious ?? 0m,
                GrossOverallAverageCurrent = athleteRound.Context?.GrossOverallAverageCurrent ?? 0m,
                GrossOverallMovingAverageCurrent = athleteRound.Context?.GrossOverallMovingAverageCurrent ?? 0m,
                GrossOverallAveragePrevious = athleteRound.Context?.GrossOverallAveragePrevious ?? 0m,
                GrossOverallMovingAveragePrevious = athleteRound.Context?.GrossOverallMovingAveragePrevious ?? 0m,
                NetOverallAverageCurrent = athleteRound.Context?.NetOverallAverageCurrent ?? 0m,
                NetOverallMovingAverageCurrent = athleteRound.Context?.NetOverallMovingAverageCurrent ?? 0m,
                NetOverallAveragePrevious = athleteRound.Context?.NetOverallAveragePrevious ?? 0m,
                NetOverallMovingAveragePrevious = athleteRound.Context?.NetOverallMovingAveragePrevious ?? 0m,
                ValuationCurrent = athleteRound.Context?.ValuationCurrent ?? 0m,
                MinimumToAppreciateCurrent = athleteRound.Context?.MinimumToAppreciateCurrent ?? 0m,
                ValuationPrevious = athleteRound.Context?.ValuationPrevious ?? 0m,
                MinimumToAppreciatePrevious = athleteRound.Context?.MinimumToAppreciatePrevious ?? 0m,
                IndexPointsPerCartoleta = athleteRound.Context?.IndexPointsPerCartoleta ?? 0m,
                IndexOffensiveUsage = athleteRound.Context?.IndexOffensiveUsage ?? 0m,
                IndexOffensiveRepresentation = athleteRound.Context?.IndexOffensiveRepresentation ?? 0m,
                IndexPointsRepresentation = athleteRound.Context?.IndexPointsRepresentation ?? 0m,
                IndexOffensiveEfficiency = athleteRound.Context?.IndexOffensiveEfficiency ?? 0m,
                IndexDefensiveEfficiency = athleteRound.Context?.IndexDefensiveEfficiency ?? 0m,
                IndexTackleDuelPercent = athleteRound.Context?.IndexTackleDuelPercent ?? 0m,
                IndexShotsPerGame = athleteRound.Context?.IndexShotsPerGame ?? 0m,
                IndexShotsPerGoal = athleteRound.Context?.IndexShotsPerGoal ?? 0m,
                IsHomeMatch = isHomeMatch,
                ClubTablePosition = isHomeMatch ? targetMatch.HomeClubPosition : targetMatch.AwayClubPosition,
                OpponentClubCartolaId = opponentClub.CartolaId,
                OpponentClubName = opponentClub.Nickname,
                OpponentClubTablePosition = isHomeMatch ? targetMatch.AwayClubPosition : targetMatch.HomeClubPosition,
                TablePositionDelta = (isHomeMatch ? targetMatch.AwayClubPosition : targetMatch.HomeClubPosition) - (isHomeMatch ? targetMatch.HomeClubPosition : targetMatch.AwayClubPosition),
                ClubRecentForm = clubRecentForm,
                OpponentRecentForm = opponentRecentForm,
                ClubRecentFormPoints = ConvertFormToPoints(clubRecentForm),
                OpponentRecentFormPoints = ConvertFormToPoints(opponentRecentForm),
                RecentFormPointsDelta = ConvertFormToPoints(clubRecentForm) - ConvertFormToPoints(opponentRecentForm),
                ScoreLast1 = athleteHistory.ScoreLast1,
                ScoreAverageLast3 = athleteHistory.ScoreAverageLast3,
                ScoreAverageLast5 = athleteHistory.ScoreAverageLast5,
                ScoreEwmaLast3 = athleteHistory.ScoreEwmaLast3,
                ScoreEwmaLast5 = athleteHistory.ScoreEwmaLast5,
                ScoreStdLast5 = athleteHistory.ScoreStdLast5,
                ScoreFloorLast5 = athleteHistory.ScoreFloorLast5,
                ScoreCeilingLast5 = athleteHistory.ScoreCeilingLast5,
                ScoreHitRateOver3Last5 = athleteHistory.ScoreHitRateOver3Last5,
                ScoreHitRateOver5Last5 = athleteHistory.ScoreHitRateOver5Last5,
                ScoreHitRateOver8Last5 = athleteHistory.ScoreHitRateOver8Last5,
                ScoreNegativeRateLast5 = athleteHistory.ScoreNegativeRateLast5,
                ScoreTrendLast3VsPrev3 = athleteHistory.ScoreTrendLast3VsPrev3,
                HomeScoreAverageLast5 = athleteHistory.HomeScoreAverageLast5,
                AwayScoreAverageLast5 = athleteHistory.AwayScoreAverageLast5,
                VenueScoreAverageLast5 = athleteHistory.VenueScoreAverageLast5,
                HomeScoreEwmaLast3 = athleteHistory.HomeScoreEwmaLast3,
                AwayScoreEwmaLast3 = athleteHistory.AwayScoreEwmaLast3,
                VenueScoreEwmaLast3 = athleteHistory.VenueScoreEwmaLast3,
                ScoutOffensiveSumLast3 = athleteHistory.ScoutOffensiveSumLast3,
                ScoutOffensiveSumLast5 = athleteHistory.ScoutOffensiveSumLast5,
                ScoutOffensiveAverageLast3 = athleteHistory.ScoutOffensiveAverageLast3,
                ScoutOffensiveAverageLast5 = athleteHistory.ScoutOffensiveAverageLast5,
                ScoutOffensiveEwmaLast3 = athleteHistory.ScoutOffensiveEwmaLast3,
                ScoutOffensiveEwmaLast5 = athleteHistory.ScoutOffensiveEwmaLast5,
                ScoutDefensiveSumLast3 = athleteHistory.ScoutDefensiveSumLast3,
                ScoutDefensiveSumLast5 = athleteHistory.ScoutDefensiveSumLast5,
                ScoutDefensiveAverageLast3 = athleteHistory.ScoutDefensiveAverageLast3,
                ScoutDefensiveAverageLast5 = athleteHistory.ScoutDefensiveAverageLast5,
                ScoutDefensiveEwmaLast3 = athleteHistory.ScoutDefensiveEwmaLast3,
                ScoutDefensiveEwmaLast5 = athleteHistory.ScoutDefensiveEwmaLast5,
                ScoutNegativeSumLast3 = athleteHistory.ScoutNegativeSumLast3,
                ScoutNegativeSumLast5 = athleteHistory.ScoutNegativeSumLast5,
                ScoutNegativeAverageLast3 = athleteHistory.ScoutNegativeAverageLast3,
                ScoutNegativeAverageLast5 = athleteHistory.ScoutNegativeAverageLast5,
                ScoutNegativeEwmaLast3 = athleteHistory.ScoutNegativeEwmaLast3,
                ScoutNegativeEwmaLast5 = athleteHistory.ScoutNegativeEwmaLast5,
                ScoutBalanceSumLast3 = athleteHistory.ScoutBalanceSumLast3,
                ScoutBalanceSumLast5 = athleteHistory.ScoutBalanceSumLast5,
                ScoutBalanceAverageLast3 = athleteHistory.ScoutBalanceAverageLast3,
                ScoutBalanceAverageLast5 = athleteHistory.ScoutBalanceAverageLast5,
                ScoutBalanceEwmaLast3 = athleteHistory.ScoutBalanceEwmaLast3,
                ScoutBalanceEwmaLast5 = athleteHistory.ScoutBalanceEwmaLast5,
                ScoutGoalsAverageLast3 = athleteHistory.ScoutGoalsAverageLast3,
                ScoutGoalsAverageLast5 = athleteHistory.ScoutGoalsAverageLast5,
                ScoutAssistsAverageLast3 = athleteHistory.ScoutAssistsAverageLast3,
                ScoutAssistsAverageLast5 = athleteHistory.ScoutAssistsAverageLast5,
                ScoutTacklesAverageLast3 = athleteHistory.ScoutTacklesAverageLast3,
                ScoutTacklesAverageLast5 = athleteHistory.ScoutTacklesAverageLast5,
                ScoutCleanSheetRateLast3 = athleteHistory.ScoutCleanSheetRateLast3,
                ScoutCleanSheetRateLast5 = athleteHistory.ScoutCleanSheetRateLast5,
                PlayRateLast5 = athleteHistory.PlayRateLast5,
                RoundsSinceLastInGame = athleteHistory.RoundsSinceLastInGame,
                StatusStabilityLast5 = athleteHistory.StatusStabilityLast5,
                StatusChangeFlag = athleteHistory.StatusChangeFlag,
                EmpiricalPlayRateForCurrentStatus = athleteHistory.EmpiricalPlayRateForCurrentStatus,
                PriceTrendLast3 = athleteHistory.PriceTrendLast3,
                PriceVolatilityLast5 = athleteHistory.PriceVolatilityLast5,
                HistoricalPointsPerCartoletaLast3 = athleteHistory.HistoricalPointsPerCartoletaLast3,
                HistoricalPointsPerCartoletaLast5 = athleteHistory.HistoricalPointsPerCartoletaLast5,
                PositionRelativePriceSignal = athleteHistory.PositionRelativePriceSignal,
                PriceDeltaVsHistoricalAverage = athleteHistory.PriceDeltaVsHistoricalAverage,
                ClubMatchesSampleSize = clubContext.MatchesSampleSize,
                ClubPointsPerMatchLast3 = clubContext.PointsPerMatchLast3,
                ClubPointsPerMatchLast5 = clubContext.PointsPerMatchLast5,
                ClubVenuePointsPerMatchLast3 = clubContext.VenuePointsPerMatchLast3,
                ClubVenuePointsPerMatchLast5 = clubContext.VenuePointsPerMatchLast5,
                ClubGoalsForPerMatchLast3 = clubContext.GoalsForPerMatchLast3,
                ClubGoalsForPerMatchLast5 = clubContext.GoalsForPerMatchLast5,
                ClubGoalsAgainstPerMatchLast3 = clubContext.GoalsAgainstPerMatchLast3,
                ClubGoalsAgainstPerMatchLast5 = clubContext.GoalsAgainstPerMatchLast5,
                ClubGoalDifferencePerMatchLast3 = clubContext.GoalDifferencePerMatchLast3,
                ClubGoalDifferencePerMatchLast5 = clubContext.GoalDifferencePerMatchLast5,
                ClubVenueGoalsForPerMatchLast3 = clubContext.VenueGoalsForPerMatchLast3,
                ClubVenueGoalsForPerMatchLast5 = clubContext.VenueGoalsForPerMatchLast5,
                ClubVenueGoalsAgainstPerMatchLast3 = clubContext.VenueGoalsAgainstPerMatchLast3,
                ClubVenueGoalsAgainstPerMatchLast5 = clubContext.VenueGoalsAgainstPerMatchLast5,
                ClubCleanSheetRateLast3 = clubContext.CleanSheetRateLast3,
                ClubCleanSheetRateLast5 = clubContext.CleanSheetRateLast5,
                ClubFailedToScoreRateLast3 = clubContext.FailedToScoreRateLast3,
                ClubFailedToScoreRateLast5 = clubContext.FailedToScoreRateLast5,
                ClubFantasyPointsForAverageLast3 = clubContext.FantasyPointsForAverageLast3,
                ClubFantasyPointsForAverageLast5 = clubContext.FantasyPointsForAverageLast5,
                ClubFantasyPointsAgainstAverageLast3 = clubContext.FantasyPointsAgainstAverageLast3,
                ClubFantasyPointsAgainstAverageLast5 = clubContext.FantasyPointsAgainstAverageLast5,
                ClubScoutOffensiveConcededAverageLast3 = clubContext.ScoutOffensiveConcededAverageLast3,
                ClubScoutOffensiveConcededAverageLast5 = clubContext.ScoutOffensiveConcededAverageLast5,
                ClubScoutDefensiveConcededAverageLast3 = clubContext.ScoutDefensiveConcededAverageLast3,
                ClubScoutDefensiveConcededAverageLast5 = clubContext.ScoutDefensiveConcededAverageLast5,
                ClubScoutNegativeConcededAverageLast3 = clubContext.ScoutNegativeConcededAverageLast3,
                ClubScoutNegativeConcededAverageLast5 = clubContext.ScoutNegativeConcededAverageLast5,
                ClubScoutBalanceConcededAverageLast3 = clubContext.ScoutBalanceConcededAverageLast3,
                ClubScoutBalanceConcededAverageLast5 = clubContext.ScoutBalanceConcededAverageLast5,
                ClubScoutGoalsConcededAverageLast3 = clubContext.ScoutGoalsConcededAverageLast3,
                ClubScoutGoalsConcededAverageLast5 = clubContext.ScoutGoalsConcededAverageLast5,
                ClubScoutAssistsConcededAverageLast3 = clubContext.ScoutAssistsConcededAverageLast3,
                ClubScoutAssistsConcededAverageLast5 = clubContext.ScoutAssistsConcededAverageLast5,
                ClubScoutTacklesConcededAverageLast3 = clubContext.ScoutTacklesConcededAverageLast3,
                ClubScoutTacklesConcededAverageLast5 = clubContext.ScoutTacklesConcededAverageLast5,
                ClubPositionFantasyPointsForAverageLast3 = clubContext.PositionFantasyPointsForAverageLast3,
                ClubPositionFantasyPointsForAverageLast5 = clubContext.PositionFantasyPointsForAverageLast5,
                ClubPositionFantasyPointsAgainstAverageLast3 = clubContext.PositionFantasyPointsAgainstAverageLast3,
                ClubPositionFantasyPointsAgainstAverageLast5 = clubContext.PositionFantasyPointsAgainstAverageLast5,
                ClubPositionScoutOffensiveConcededAverageLast3 = clubContext.PositionScoutOffensiveConcededAverageLast3,
                ClubPositionScoutOffensiveConcededAverageLast5 = clubContext.PositionScoutOffensiveConcededAverageLast5,
                ClubPositionScoutDefensiveConcededAverageLast3 = clubContext.PositionScoutDefensiveConcededAverageLast3,
                ClubPositionScoutDefensiveConcededAverageLast5 = clubContext.PositionScoutDefensiveConcededAverageLast5,
                ClubPositionScoutNegativeConcededAverageLast3 = clubContext.PositionScoutNegativeConcededAverageLast3,
                ClubPositionScoutNegativeConcededAverageLast5 = clubContext.PositionScoutNegativeConcededAverageLast5,
                ClubPositionScoutBalanceConcededAverageLast3 = clubContext.PositionScoutBalanceConcededAverageLast3,
                ClubPositionScoutBalanceConcededAverageLast5 = clubContext.PositionScoutBalanceConcededAverageLast5,
                ClubPositionScoutGoalsConcededAverageLast3 = clubContext.PositionScoutGoalsConcededAverageLast3,
                ClubPositionScoutGoalsConcededAverageLast5 = clubContext.PositionScoutGoalsConcededAverageLast5,
                ClubPositionScoutAssistsConcededAverageLast3 = clubContext.PositionScoutAssistsConcededAverageLast3,
                ClubPositionScoutAssistsConcededAverageLast5 = clubContext.PositionScoutAssistsConcededAverageLast5,
                ClubPositionScoutTacklesConcededAverageLast3 = clubContext.PositionScoutTacklesConcededAverageLast3,
                ClubPositionScoutTacklesConcededAverageLast5 = clubContext.PositionScoutTacklesConcededAverageLast5,
                ClubExpectedPointsLast3 = clubContext.ExpectedPointsLast3,
                ClubWinRateLast5 = clubContext.WinRateLast5,
                ClubDrawRateLast5 = clubContext.DrawRateLast5,
                ClubLossRateLast5 = clubContext.LossRateLast5,
                ClubExpectedPointsLast5 = clubContext.ExpectedPointsLast5,
                ClubAttackStrengthLast3 = clubContext.AttackStrengthLast3,
                ClubAttackStrengthLast5 = clubContext.AttackStrengthLast5,
                ClubDefenseStrengthLast3 = clubContext.DefenseStrengthLast3,
                ClubDefenseStrengthLast5 = clubContext.DefenseStrengthLast5,
                OpponentMatchesSampleSize = opponentContext.MatchesSampleSize,
                OpponentPointsPerMatchLast3 = opponentContext.PointsPerMatchLast3,
                OpponentPointsPerMatchLast5 = opponentContext.PointsPerMatchLast5,
                OpponentVenuePointsPerMatchLast3 = opponentContext.VenuePointsPerMatchLast3,
                OpponentVenuePointsPerMatchLast5 = opponentContext.VenuePointsPerMatchLast5,
                OpponentGoalsForPerMatchLast3 = opponentContext.GoalsForPerMatchLast3,
                OpponentGoalsForPerMatchLast5 = opponentContext.GoalsForPerMatchLast5,
                OpponentGoalsAgainstPerMatchLast3 = opponentContext.GoalsAgainstPerMatchLast3,
                OpponentGoalsAgainstPerMatchLast5 = opponentContext.GoalsAgainstPerMatchLast5,
                OpponentGoalDifferencePerMatchLast3 = opponentContext.GoalDifferencePerMatchLast3,
                OpponentGoalDifferencePerMatchLast5 = opponentContext.GoalDifferencePerMatchLast5,
                OpponentVenueGoalsForPerMatchLast3 = opponentContext.VenueGoalsForPerMatchLast3,
                OpponentVenueGoalsForPerMatchLast5 = opponentContext.VenueGoalsForPerMatchLast5,
                OpponentVenueGoalsAgainstPerMatchLast3 = opponentContext.VenueGoalsAgainstPerMatchLast3,
                OpponentVenueGoalsAgainstPerMatchLast5 = opponentContext.VenueGoalsAgainstPerMatchLast5,
                OpponentCleanSheetRateLast3 = opponentContext.CleanSheetRateLast3,
                OpponentCleanSheetRateLast5 = opponentContext.CleanSheetRateLast5,
                OpponentFailedToScoreRateLast3 = opponentContext.FailedToScoreRateLast3,
                OpponentFailedToScoreRateLast5 = opponentContext.FailedToScoreRateLast5,
                OpponentFantasyPointsForAverageLast3 = opponentContext.FantasyPointsForAverageLast3,
                OpponentFantasyPointsForAverageLast5 = opponentContext.FantasyPointsForAverageLast5,
                OpponentFantasyPointsAgainstAverageLast3 = opponentContext.FantasyPointsAgainstAverageLast3,
                OpponentFantasyPointsAgainstAverageLast5 = opponentContext.FantasyPointsAgainstAverageLast5,
                OpponentScoutOffensiveConcededAverageLast3 = opponentContext.ScoutOffensiveConcededAverageLast3,
                OpponentScoutOffensiveConcededAverageLast5 = opponentContext.ScoutOffensiveConcededAverageLast5,
                OpponentScoutDefensiveConcededAverageLast3 = opponentContext.ScoutDefensiveConcededAverageLast3,
                OpponentScoutDefensiveConcededAverageLast5 = opponentContext.ScoutDefensiveConcededAverageLast5,
                OpponentScoutNegativeConcededAverageLast3 = opponentContext.ScoutNegativeConcededAverageLast3,
                OpponentScoutNegativeConcededAverageLast5 = opponentContext.ScoutNegativeConcededAverageLast5,
                OpponentScoutBalanceConcededAverageLast3 = opponentContext.ScoutBalanceConcededAverageLast3,
                OpponentScoutBalanceConcededAverageLast5 = opponentContext.ScoutBalanceConcededAverageLast5,
                OpponentScoutGoalsConcededAverageLast3 = opponentContext.ScoutGoalsConcededAverageLast3,
                OpponentScoutGoalsConcededAverageLast5 = opponentContext.ScoutGoalsConcededAverageLast5,
                OpponentScoutAssistsConcededAverageLast3 = opponentContext.ScoutAssistsConcededAverageLast3,
                OpponentScoutAssistsConcededAverageLast5 = opponentContext.ScoutAssistsConcededAverageLast5,
                OpponentScoutTacklesConcededAverageLast3 = opponentContext.ScoutTacklesConcededAverageLast3,
                OpponentScoutTacklesConcededAverageLast5 = opponentContext.ScoutTacklesConcededAverageLast5,
                OpponentPositionFantasyPointsForAverageLast3 = opponentContext.PositionFantasyPointsForAverageLast3,
                OpponentPositionFantasyPointsForAverageLast5 = opponentContext.PositionFantasyPointsForAverageLast5,
                OpponentPositionFantasyPointsAgainstAverageLast3 = opponentContext.PositionFantasyPointsAgainstAverageLast3,
                OpponentPositionFantasyPointsAgainstAverageLast5 = opponentContext.PositionFantasyPointsAgainstAverageLast5,
                OpponentPositionScoutOffensiveConcededAverageLast3 = opponentContext.PositionScoutOffensiveConcededAverageLast3,
                OpponentPositionScoutOffensiveConcededAverageLast5 = opponentContext.PositionScoutOffensiveConcededAverageLast5,
                OpponentPositionScoutDefensiveConcededAverageLast3 = opponentContext.PositionScoutDefensiveConcededAverageLast3,
                OpponentPositionScoutDefensiveConcededAverageLast5 = opponentContext.PositionScoutDefensiveConcededAverageLast5,
                OpponentPositionScoutNegativeConcededAverageLast3 = opponentContext.PositionScoutNegativeConcededAverageLast3,
                OpponentPositionScoutNegativeConcededAverageLast5 = opponentContext.PositionScoutNegativeConcededAverageLast5,
                OpponentPositionScoutBalanceConcededAverageLast3 = opponentContext.PositionScoutBalanceConcededAverageLast3,
                OpponentPositionScoutBalanceConcededAverageLast5 = opponentContext.PositionScoutBalanceConcededAverageLast5,
                OpponentPositionScoutGoalsConcededAverageLast3 = opponentContext.PositionScoutGoalsConcededAverageLast3,
                OpponentPositionScoutGoalsConcededAverageLast5 = opponentContext.PositionScoutGoalsConcededAverageLast5,
                OpponentPositionScoutAssistsConcededAverageLast3 = opponentContext.PositionScoutAssistsConcededAverageLast3,
                OpponentPositionScoutAssistsConcededAverageLast5 = opponentContext.PositionScoutAssistsConcededAverageLast5,
                OpponentPositionScoutTacklesConcededAverageLast3 = opponentContext.PositionScoutTacklesConcededAverageLast3,
                OpponentPositionScoutTacklesConcededAverageLast5 = opponentContext.PositionScoutTacklesConcededAverageLast5,
                OpponentExpectedPointsLast3 = opponentContext.ExpectedPointsLast3,
                OpponentWinRateLast5 = opponentContext.WinRateLast5,
                OpponentDrawRateLast5 = opponentContext.DrawRateLast5,
                OpponentLossRateLast5 = opponentContext.LossRateLast5,
                OpponentExpectedPointsLast5 = opponentContext.ExpectedPointsLast5,
                OpponentAttackStrengthLast3 = opponentContext.AttackStrengthLast3,
                OpponentAttackStrengthLast5 = opponentContext.AttackStrengthLast5,
                OpponentDefenseStrengthLast3 = opponentContext.DefenseStrengthLast3,
                OpponentDefenseStrengthLast5 = opponentContext.DefenseStrengthLast5,
                AttackVsOpponentDefenseDeltaLast3 = clubContext.AttackStrengthLast3 - opponentContext.DefenseStrengthLast3,
                AttackVsOpponentDefenseDeltaLast5 = clubContext.AttackStrengthLast5 - opponentContext.DefenseStrengthLast5,
                PositionForVsOpponentConcededDeltaLast3 = clubContext.PositionFantasyPointsForAverageLast3 - opponentContext.PositionFantasyPointsAgainstAverageLast3,
                PositionForVsOpponentConcededDeltaLast5 = clubContext.PositionFantasyPointsForAverageLast5 - opponentContext.PositionFantasyPointsAgainstAverageLast5,
                GoalDifferenceDeltaLast3 = clubContext.GoalDifferencePerMatchLast3 - opponentContext.GoalDifferencePerMatchLast3,
                GoalDifferenceDeltaLast5 = clubContext.GoalDifferencePerMatchLast5 - opponentContext.GoalDifferencePerMatchLast5,
                TargetScore = athleteRound.ScoreNum,
                TargetWasInGame = athleteRound.WasInGame,
            });
        }

        return rows;
    }

    private static AthleteHistorySnapshot BuildAthleteSnapshot(
        IReadOnlyDictionary<int, List<AthleteObservation>> athleteHistoryByAthleteId,
        IReadOnlyDictionary<int, List<AthleteObservation>> positionHistoryByPositionId,
        IReadOnlyList<AthleteObservation> globalAthleteHistory,
        IReadOnlyDictionary<(int PositionId, int Round), decimal> averagePriceByPositionAndRound,
        int athleteId,
        int currentStatusId,
        decimal currentPrice,
        int positionId,
        int targetRound,
        bool targetIsHomeMatch)
    {
        if (!athleteHistoryByAthleteId.TryGetValue(athleteId, out var observations))
        {
            return default;
        }

        var prior = observations.Where(item => item.RoundId < targetRound).OrderByDescending(item => item.RoundId).ToList();
        var playedPrior = prior.Where(item => item.WasInGame).ToList();
        var playedPriorHome = playedPrior.Where(item => item.IsHomeMatch is true).ToList();
        var playedPriorAway = playedPrior.Where(item => item.IsHomeMatch is false).ToList();
        var playedPriorVenue = playedPrior.Where(item => item.IsHomeMatch == targetIsHomeMatch).ToList();
        var playedPriorLast5 = playedPrior.Take(5).ToList();
        var last3 = prior.Take(3).ToList();
        var last5 = prior.Take(5).ToList();
        var previous3 = prior.Skip(3).Take(3).ToList();
        var homeLast5 = prior.Where(item => item.IsHomeMatch is true).Take(5).ToList();
        var awayLast5 = prior.Where(item => item.IsHomeMatch is false).Take(5).ToList();
        var venueLast5 = prior.Where(item => item.IsHomeMatch == targetIsHomeMatch).Take(5).ToList();
        var homeLast3 = prior.Where(item => item.IsHomeMatch is true).Take(3).ToList();
        var awayLast3 = prior.Where(item => item.IsHomeMatch is false).Take(3).ToList();
        var venueLast3 = prior.Where(item => item.IsHomeMatch == targetIsHomeMatch).Take(3).ToList();

        var scoutLast3 = last3;
        var scoutLast5 = last5;
        var lastInGameRound = prior.FirstOrDefault(item => item.WasInGame).RoundId;
        var roundsSinceLastInGame = lastInGameRound > 0 ? targetRound - lastInGameRound : prior.Count;
        var statusMatchedLast5 = last5.Where(item => item.StatusId == currentStatusId).ToList();
        var empiricalPlayRateForCurrentStatus = statusMatchedLast5.Count > 0
            ? Rate(statusMatchedLast5.Select(item => item.WasInGame))
            : Rate(last5.Select(item => item.WasInGame));
        var positionAveragePriceLast5 = Average(
            averagePriceByPositionAndRound
                .Where(item => item.Key.PositionId == positionId && item.Key.Round < targetRound)
                .OrderByDescending(item => item.Key.Round)
                .Take(5)
                .Select(item => item.Value));
        var historicalAveragePrice = Average(prior.Select(item => item.Price));
        var priceTrendLast3 = ComputePriceTrendLast3(last3.Select(item => item.Price));
        var rawSnapshot = new AthleteHistorySnapshot(
            playedPrior.Count,
            playedPriorHome.Count,
            playedPriorAway.Count,
            playedPriorVenue.Count,
            playedPrior.Count,
            playedPriorLast5.Count,
            last3.Count,
            last5.Count,
            previous3.Count,
            homeLast5.Count,
            awayLast5.Count,
            venueLast5.Count,
            statusMatchedLast5.Count,
            Math.Min(1m, last3.Count / 3m),
            Math.Min(1m, last5.Count / 5m),
            Math.Min(1m, homeLast5.Count / 5m),
            Math.Min(1m, awayLast5.Count / 5m),
            Math.Min(1m, venueLast5.Count / 5m),
            Math.Min(1m, statusMatchedLast5.Count / 5m),
            playedPrior.Count < 3 ? 1m : 0m,
            Ewma(playedPriorLast5.Select(item => item.Score)),
            Average(playedPrior.Select(item => item.Score)),
            Average(playedPriorHome.Select(item => item.Score)),
            Average(playedPriorAway.Select(item => item.Score)),
            Average(playedPriorVenue.Select(item => item.Score)),
            last5.FirstOrDefault().Score,
            Average(last3.Select(item => item.Score)),
            Average(last5.Select(item => item.Score)),
            Ewma(last3.Select(item => item.Score)),
            Ewma(last5.Select(item => item.Score)),
            StandardDeviation(last5.Select(item => item.Score)),
            Min(last5.Select(item => item.Score)),
            Max(last5.Select(item => item.Score)),
            Rate(last5.Select(item => item.Score > 3m)),
            Rate(last5.Select(item => item.Score > 5m)),
            Rate(last5.Select(item => item.Score > 8m)),
            Rate(last5.Select(item => item.Score < 0m)),
            Average(last3.Select(item => item.Score)) - Average(previous3.Select(item => item.Score)),
            Average(homeLast5.Select(item => item.Score)),
            Average(awayLast5.Select(item => item.Score)),
            Average(venueLast5.Select(item => item.Score)),
            Ewma(homeLast3.Select(item => item.Score)),
            Ewma(awayLast3.Select(item => item.Score)),
            Ewma(venueLast3.Select(item => item.Score)),
            Sum(scoutLast3.Select(item => item.OffensiveScoutScore)),
            Sum(scoutLast5.Select(item => item.OffensiveScoutScore)),
            Average(scoutLast3.Select(item => item.OffensiveScoutScore)),
            Average(scoutLast5.Select(item => item.OffensiveScoutScore)),
            Ewma(scoutLast3.Select(item => item.OffensiveScoutScore)),
            Ewma(scoutLast5.Select(item => item.OffensiveScoutScore)),
            Sum(scoutLast3.Select(item => item.DefensiveScoutScore)),
            Sum(scoutLast5.Select(item => item.DefensiveScoutScore)),
            Average(scoutLast3.Select(item => item.DefensiveScoutScore)),
            Average(scoutLast5.Select(item => item.DefensiveScoutScore)),
            Ewma(scoutLast3.Select(item => item.DefensiveScoutScore)),
            Ewma(scoutLast5.Select(item => item.DefensiveScoutScore)),
            Sum(scoutLast3.Select(item => item.NegativeScoutScore)),
            Sum(scoutLast5.Select(item => item.NegativeScoutScore)),
            Average(scoutLast3.Select(item => item.NegativeScoutScore)),
            Average(scoutLast5.Select(item => item.NegativeScoutScore)),
            Ewma(scoutLast3.Select(item => item.NegativeScoutScore)),
            Ewma(scoutLast5.Select(item => item.NegativeScoutScore)),
            Sum(scoutLast3.Select(item => item.BalanceScoutScore)),
            Sum(scoutLast5.Select(item => item.BalanceScoutScore)),
            Average(scoutLast3.Select(item => item.BalanceScoutScore)),
            Average(scoutLast5.Select(item => item.BalanceScoutScore)),
            Ewma(scoutLast3.Select(item => item.BalanceScoutScore)),
            Ewma(scoutLast5.Select(item => item.BalanceScoutScore)),
            Average(scoutLast3.Select(item => item.GoalsCount)),
            Average(scoutLast5.Select(item => item.GoalsCount)),
            Average(scoutLast3.Select(item => item.AssistsCount)),
            Average(scoutLast5.Select(item => item.AssistsCount)),
            Average(scoutLast3.Select(item => item.TacklesCount)),
            Average(scoutLast5.Select(item => item.TacklesCount)),
            Rate(scoutLast3.Select(item => item.CleanSheetCount > 0m)),
            Rate(scoutLast5.Select(item => item.CleanSheetCount > 0m)),
            Rate(last5.Select(item => item.WasInGame)),
            roundsSinceLastInGame,
            Rate(last5.Select(item => item.StatusId == currentStatusId)),
            prior.Count > 0 && prior[0].StatusId != currentStatusId ? 1m : 0m,
            empiricalPlayRateForCurrentStatus,
            priceTrendLast3,
            StandardDeviation(last5.Select(item => item.Price)),
            Average(last3.Select(item => SafeDivide(item.Score, item.Price))),
            Average(last5.Select(item => SafeDivide(item.Score, item.Price))),
            currentPrice - positionAveragePriceLast5,
            currentPrice - historicalAveragePrice);

        var priorSnapshot = BuildAthleteHistoryPrior(positionHistoryByPositionId, globalAthleteHistory, averagePriceByPositionAndRound, currentStatusId, currentPrice, positionId, targetRound, targetIsHomeMatch);

        return ApplyAthleteHistoryShrinkage(
            rawSnapshot,
            priorSnapshot,
            playedPrior.Count,
            playedPriorLast5.Count,
            playedPriorHome.Count,
            playedPriorAway.Count,
            playedPriorVenue.Count,
            last3.Count,
            last5.Count,
            previous3.Count,
            homeLast5.Count,
            awayLast5.Count,
            venueLast5.Count,
            homeLast3.Count,
            awayLast3.Count,
            venueLast3.Count,
            statusMatchedLast5.Count,
            currentPrice,
            historicalAveragePrice);
    }

    private static AthleteHistoryPrior BuildAthleteHistoryPrior(
        IReadOnlyDictionary<int, List<AthleteObservation>> positionHistoryByPositionId,
        IReadOnlyList<AthleteObservation> globalAthleteHistory,
        IReadOnlyDictionary<(int PositionId, int Round), decimal> averagePriceByPositionAndRound,
        int currentStatusId,
        decimal currentPrice,
        int positionId,
        int targetRound,
        bool targetIsHomeMatch)
    {
        var positionObservations = positionHistoryByPositionId.TryGetValue(positionId, out var history)
            ? history.Where(item => item.RoundId < targetRound).OrderByDescending(item => item.RoundId).ToList()
            : [];
        var globalObservations = globalAthleteHistory
            .Where(item => item.RoundId < targetRound)
            .OrderByDescending(item => item.RoundId)
            .ToList();

        return positionObservations.Count > 0
            ? CreateAthleteHistoryPrior(positionObservations, averagePriceByPositionAndRound, currentStatusId, currentPrice, positionId, targetRound, targetIsHomeMatch)
            : CreateAthleteHistoryPrior(globalObservations, averagePriceByPositionAndRound, currentStatusId, currentPrice, positionId, targetRound, targetIsHomeMatch);
    }

    private static AthleteHistoryPrior CreateAthleteHistoryPrior(
        IReadOnlyList<AthleteObservation> observations,
        IReadOnlyDictionary<(int PositionId, int Round), decimal> averagePriceByPositionAndRound,
        int currentStatusId,
        decimal currentPrice,
        int positionId,
        int targetRound,
        bool targetIsHomeMatch)
    {
        var played = observations.Where(item => item.WasInGame).ToList();
        var playedHome = played.Where(item => item.IsHomeMatch is true).ToList();
        var playedAway = played.Where(item => item.IsHomeMatch is false).ToList();
        var playedVenue = played.Where(item => item.IsHomeMatch == targetIsHomeMatch).ToList();
        var playedLast5 = played.Take(5).ToList();
        var last3 = observations.Take(3).ToList();
        var last5 = observations.Take(5).ToList();
        var previous3 = observations.Skip(3).Take(3).ToList();
        var homeLast5 = observations.Where(item => item.IsHomeMatch is true).Take(5).ToList();
        var awayLast5 = observations.Where(item => item.IsHomeMatch is false).Take(5).ToList();
        var venueLast5 = observations.Where(item => item.IsHomeMatch == targetIsHomeMatch).Take(5).ToList();
        var homeLast3 = observations.Where(item => item.IsHomeMatch is true).Take(3).ToList();
        var awayLast3 = observations.Where(item => item.IsHomeMatch is false).Take(3).ToList();
        var venueLast3 = observations.Where(item => item.IsHomeMatch == targetIsHomeMatch).Take(3).ToList();
        var statusMatchedLast5 = last5.Where(item => item.StatusId == currentStatusId).ToList();
        var lastInGameRound = observations.FirstOrDefault(item => item.WasInGame).RoundId;
        var roundsSinceLastInGame = lastInGameRound > 0 ? targetRound - lastInGameRound : observations.Count;
        var positionAveragePriceLast5 = Average(
            averagePriceByPositionAndRound
                .Where(item => item.Key.PositionId == positionId && item.Key.Round < targetRound)
                .OrderByDescending(item => item.Key.Round)
                .Take(5)
                .Select(item => item.Value));
        var historicalAveragePrice = Average(observations.Select(item => item.Price));
        var empiricalPlayRateForCurrentStatus = statusMatchedLast5.Count > 0
            ? Rate(statusMatchedLast5.Select(item => item.WasInGame))
            : Rate(last5.Select(item => item.WasInGame));

        return new AthleteHistoryPrior(
            new AthleteHistorySnapshot(
                played.Count,
                playedHome.Count,
                playedAway.Count,
                playedVenue.Count,
                played.Count,
                playedLast5.Count,
                last3.Count,
                last5.Count,
                previous3.Count,
                homeLast5.Count,
                awayLast5.Count,
                venueLast5.Count,
                statusMatchedLast5.Count,
                Math.Min(1m, last3.Count / 3m),
                Math.Min(1m, last5.Count / 5m),
                Math.Min(1m, homeLast5.Count / 5m),
                Math.Min(1m, awayLast5.Count / 5m),
                Math.Min(1m, venueLast5.Count / 5m),
                Math.Min(1m, statusMatchedLast5.Count / 5m),
                played.Count < 3 ? 1m : 0m,
                Ewma(playedLast5.Select(item => item.Score)),
                Average(played.Select(item => item.Score)),
                Average(playedHome.Select(item => item.Score)),
                Average(playedAway.Select(item => item.Score)),
                Average(playedVenue.Select(item => item.Score)),
                last5.FirstOrDefault().Score,
                Average(last3.Select(item => item.Score)),
                Average(last5.Select(item => item.Score)),
                Ewma(last3.Select(item => item.Score)),
                Ewma(last5.Select(item => item.Score)),
                StandardDeviation(last5.Select(item => item.Score)),
                Min(last5.Select(item => item.Score)),
                Max(last5.Select(item => item.Score)),
                Rate(last5.Select(item => item.Score > 3m)),
                Rate(last5.Select(item => item.Score > 5m)),
                Rate(last5.Select(item => item.Score > 8m)),
                Rate(last5.Select(item => item.Score < 0m)),
                Average(last3.Select(item => item.Score)) - Average(previous3.Select(item => item.Score)),
                Average(homeLast5.Select(item => item.Score)),
                Average(awayLast5.Select(item => item.Score)),
                Average(venueLast5.Select(item => item.Score)),
                Ewma(homeLast3.Select(item => item.Score)),
                Ewma(awayLast3.Select(item => item.Score)),
                Ewma(venueLast3.Select(item => item.Score)),
                Sum(last3.Select(item => item.OffensiveScoutScore)),
                Sum(last5.Select(item => item.OffensiveScoutScore)),
                Average(last3.Select(item => item.OffensiveScoutScore)),
                Average(last5.Select(item => item.OffensiveScoutScore)),
                Ewma(last3.Select(item => item.OffensiveScoutScore)),
                Ewma(last5.Select(item => item.OffensiveScoutScore)),
                Sum(last3.Select(item => item.DefensiveScoutScore)),
                Sum(last5.Select(item => item.DefensiveScoutScore)),
                Average(last3.Select(item => item.DefensiveScoutScore)),
                Average(last5.Select(item => item.DefensiveScoutScore)),
                Ewma(last3.Select(item => item.DefensiveScoutScore)),
                Ewma(last5.Select(item => item.DefensiveScoutScore)),
                Sum(last3.Select(item => item.NegativeScoutScore)),
                Sum(last5.Select(item => item.NegativeScoutScore)),
                Average(last3.Select(item => item.NegativeScoutScore)),
                Average(last5.Select(item => item.NegativeScoutScore)),
                Ewma(last3.Select(item => item.NegativeScoutScore)),
                Ewma(last5.Select(item => item.NegativeScoutScore)),
                Sum(last3.Select(item => item.BalanceScoutScore)),
                Sum(last5.Select(item => item.BalanceScoutScore)),
                Average(last3.Select(item => item.BalanceScoutScore)),
                Average(last5.Select(item => item.BalanceScoutScore)),
                Ewma(last3.Select(item => item.BalanceScoutScore)),
                Ewma(last5.Select(item => item.BalanceScoutScore)),
                Average(last3.Select(item => item.GoalsCount)),
                Average(last5.Select(item => item.GoalsCount)),
                Average(last3.Select(item => item.AssistsCount)),
                Average(last5.Select(item => item.AssistsCount)),
                Average(last3.Select(item => item.TacklesCount)),
                Average(last5.Select(item => item.TacklesCount)),
                Rate(last3.Select(item => item.CleanSheetCount > 0m)),
                Rate(last5.Select(item => item.CleanSheetCount > 0m)),
                Rate(last5.Select(item => item.WasInGame)),
                roundsSinceLastInGame,
                Rate(last5.Select(item => item.StatusId == currentStatusId)),
                observations.Count > 0 && observations[0].StatusId != currentStatusId ? 1m : 0m,
                empiricalPlayRateForCurrentStatus,
                ComputePriceTrendLast3(last3.Select(item => item.Price)),
                StandardDeviation(last5.Select(item => item.Price)),
                Average(last3.Select(item => SafeDivide(item.Score, item.Price))),
                Average(last5.Select(item => SafeDivide(item.Score, item.Price))),
                currentPrice - positionAveragePriceLast5,
                currentPrice - historicalAveragePrice),
            historicalAveragePrice);
    }

    private static AthleteHistorySnapshot ApplyAthleteHistoryShrinkage(
        AthleteHistorySnapshot raw,
        AthleteHistoryPrior prior,
        int playedCount,
        int playedLast5Count,
        int playedHomeCount,
        int playedAwayCount,
        int playedVenueCount,
        int last3Count,
        int last5Count,
        int previous3Count,
        int homeLast5Count,
        int awayLast5Count,
        int venueLast5Count,
        int homeLast3Count,
        int awayLast3Count,
        int venueLast3Count,
        int statusMatchedLast5Count,
        decimal currentPrice,
        decimal historicalAveragePrice)
        => new(
            raw.GamesNumPrior,
            raw.HomeGamesNumPrior,
            raw.AwayGamesNumPrior,
            raw.VenueGamesNumPrior,
            raw.PlayedMatchesSampleSize,
            raw.PlayedMatchesSampleSizeLast5,
            raw.ObservationCountLast3,
            raw.ObservationCountLast5,
            raw.PreviousObservationCountLast3,
            raw.HomeObservationCountLast5,
            raw.AwayObservationCountLast5,
            raw.VenueObservationCountLast5,
            raw.StatusMatchedObservationCountLast5,
            raw.HistoryCoverageLast3,
            raw.HistoryCoverageLast5,
            raw.HomeHistoryCoverageLast5,
            raw.AwayHistoryCoverageLast5,
            raw.VenueHistoryCoverageLast5,
            raw.CurrentStatusCoverageLast5,
            raw.IsColdStartAthlete,
            Shrink(raw.MovingAveragePrior, playedLast5Count, prior.Snapshot.MovingAveragePrior, 4),
            Shrink(raw.AverageScorePrior, playedCount, prior.Snapshot.AverageScorePrior, 6),
            Shrink(raw.HomeAverageScorePrior, playedHomeCount, prior.Snapshot.HomeAverageScorePrior, 4),
            Shrink(raw.AwayAverageScorePrior, playedAwayCount, prior.Snapshot.AwayAverageScorePrior, 4),
            Shrink(raw.VenueAverageScorePrior, playedVenueCount, prior.Snapshot.VenueAverageScorePrior, 4),
            Shrink(raw.ScoreLast1, last5Count > 0 ? 1 : 0, prior.Snapshot.ScoreAverageLast3, 2),
            Shrink(raw.ScoreAverageLast3, last3Count, prior.Snapshot.ScoreAverageLast3, 3),
            Shrink(raw.ScoreAverageLast5, last5Count, prior.Snapshot.ScoreAverageLast5, 5),
            Shrink(raw.ScoreEwmaLast3, last3Count, prior.Snapshot.ScoreEwmaLast3, 3),
            Shrink(raw.ScoreEwmaLast5, last5Count, prior.Snapshot.ScoreEwmaLast5, 5),
            Shrink(raw.ScoreStdLast5, last5Count, prior.Snapshot.ScoreStdLast5, 4),
            Shrink(raw.ScoreFloorLast5, last5Count, prior.Snapshot.ScoreFloorLast5, 4),
            Shrink(raw.ScoreCeilingLast5, last5Count, prior.Snapshot.ScoreCeilingLast5, 4),
            Shrink(raw.ScoreHitRateOver3Last5, last5Count, prior.Snapshot.ScoreHitRateOver3Last5, 5),
            Shrink(raw.ScoreHitRateOver5Last5, last5Count, prior.Snapshot.ScoreHitRateOver5Last5, 5),
            Shrink(raw.ScoreHitRateOver8Last5, last5Count, prior.Snapshot.ScoreHitRateOver8Last5, 5),
            Shrink(raw.ScoreNegativeRateLast5, last5Count, prior.Snapshot.ScoreNegativeRateLast5, 5),
            Shrink(raw.ScoreTrendLast3VsPrev3, Math.Min(last3Count, previous3Count), prior.Snapshot.ScoreTrendLast3VsPrev3, 4),
            Shrink(raw.HomeScoreAverageLast5, homeLast5Count, prior.Snapshot.HomeScoreAverageLast5, 4),
            Shrink(raw.AwayScoreAverageLast5, awayLast5Count, prior.Snapshot.AwayScoreAverageLast5, 4),
            Shrink(raw.VenueScoreAverageLast5, venueLast5Count, prior.Snapshot.VenueScoreAverageLast5, 4),
            Shrink(raw.HomeScoreEwmaLast3, homeLast3Count, prior.Snapshot.HomeScoreEwmaLast3, 3),
            Shrink(raw.AwayScoreEwmaLast3, awayLast3Count, prior.Snapshot.AwayScoreEwmaLast3, 3),
            Shrink(raw.VenueScoreEwmaLast3, venueLast3Count, prior.Snapshot.VenueScoreEwmaLast3, 3),
            raw.ScoutOffensiveSumLast3,
            raw.ScoutOffensiveSumLast5,
            Shrink(raw.ScoutOffensiveAverageLast3, last3Count, prior.Snapshot.ScoutOffensiveAverageLast3, 3),
            Shrink(raw.ScoutOffensiveAverageLast5, last5Count, prior.Snapshot.ScoutOffensiveAverageLast5, 5),
            Shrink(raw.ScoutOffensiveEwmaLast3, last3Count, prior.Snapshot.ScoutOffensiveEwmaLast3, 3),
            Shrink(raw.ScoutOffensiveEwmaLast5, last5Count, prior.Snapshot.ScoutOffensiveEwmaLast5, 5),
            raw.ScoutDefensiveSumLast3,
            raw.ScoutDefensiveSumLast5,
            Shrink(raw.ScoutDefensiveAverageLast3, last3Count, prior.Snapshot.ScoutDefensiveAverageLast3, 3),
            Shrink(raw.ScoutDefensiveAverageLast5, last5Count, prior.Snapshot.ScoutDefensiveAverageLast5, 5),
            Shrink(raw.ScoutDefensiveEwmaLast3, last3Count, prior.Snapshot.ScoutDefensiveEwmaLast3, 3),
            Shrink(raw.ScoutDefensiveEwmaLast5, last5Count, prior.Snapshot.ScoutDefensiveEwmaLast5, 5),
            raw.ScoutNegativeSumLast3,
            raw.ScoutNegativeSumLast5,
            Shrink(raw.ScoutNegativeAverageLast3, last3Count, prior.Snapshot.ScoutNegativeAverageLast3, 3),
            Shrink(raw.ScoutNegativeAverageLast5, last5Count, prior.Snapshot.ScoutNegativeAverageLast5, 5),
            Shrink(raw.ScoutNegativeEwmaLast3, last3Count, prior.Snapshot.ScoutNegativeEwmaLast3, 3),
            Shrink(raw.ScoutNegativeEwmaLast5, last5Count, prior.Snapshot.ScoutNegativeEwmaLast5, 5),
            raw.ScoutBalanceSumLast3,
            raw.ScoutBalanceSumLast5,
            Shrink(raw.ScoutBalanceAverageLast3, last3Count, prior.Snapshot.ScoutBalanceAverageLast3, 3),
            Shrink(raw.ScoutBalanceAverageLast5, last5Count, prior.Snapshot.ScoutBalanceAverageLast5, 5),
            Shrink(raw.ScoutBalanceEwmaLast3, last3Count, prior.Snapshot.ScoutBalanceEwmaLast3, 3),
            Shrink(raw.ScoutBalanceEwmaLast5, last5Count, prior.Snapshot.ScoutBalanceEwmaLast5, 5),
            Shrink(raw.ScoutGoalsAverageLast3, last3Count, prior.Snapshot.ScoutGoalsAverageLast3, 3),
            Shrink(raw.ScoutGoalsAverageLast5, last5Count, prior.Snapshot.ScoutGoalsAverageLast5, 5),
            Shrink(raw.ScoutAssistsAverageLast3, last3Count, prior.Snapshot.ScoutAssistsAverageLast3, 3),
            Shrink(raw.ScoutAssistsAverageLast5, last5Count, prior.Snapshot.ScoutAssistsAverageLast5, 5),
            Shrink(raw.ScoutTacklesAverageLast3, last3Count, prior.Snapshot.ScoutTacklesAverageLast3, 3),
            Shrink(raw.ScoutTacklesAverageLast5, last5Count, prior.Snapshot.ScoutTacklesAverageLast5, 5),
            Shrink(raw.ScoutCleanSheetRateLast3, last3Count, prior.Snapshot.ScoutCleanSheetRateLast3, 3),
            Shrink(raw.ScoutCleanSheetRateLast5, last5Count, prior.Snapshot.ScoutCleanSheetRateLast5, 5),
            Shrink(raw.PlayRateLast5, last5Count, prior.Snapshot.PlayRateLast5, 5),
            raw.RoundsSinceLastInGame,
            Shrink(raw.StatusStabilityLast5, last5Count, prior.Snapshot.StatusStabilityLast5, 5),
            raw.StatusChangeFlag,
            Shrink(raw.EmpiricalPlayRateForCurrentStatus, statusMatchedLast5Count, prior.Snapshot.EmpiricalPlayRateForCurrentStatus, 4),
            Shrink(raw.PriceTrendLast3, last3Count, 0m, 3),
            Shrink(raw.PriceVolatilityLast5, last5Count, prior.Snapshot.PriceVolatilityLast5, 5),
            Shrink(raw.HistoricalPointsPerCartoletaLast3, last3Count, prior.Snapshot.HistoricalPointsPerCartoletaLast3, 3),
            Shrink(raw.HistoricalPointsPerCartoletaLast5, last5Count, prior.Snapshot.HistoricalPointsPerCartoletaLast5, 5),
            raw.PositionRelativePriceSignal,
            currentPrice - Shrink(historicalAveragePrice, playedCount, prior.HistoricalAveragePrice, 5));

    private static ClubContextSnapshot BuildClubSnapshot(
        IReadOnlyDictionary<int, List<ClubHistoryPoint>> clubHistoryByClubId,
        IReadOnlyDictionary<(int ClubId, int Round), decimal> clubFantasyFor,
        IReadOnlyDictionary<(int ClubId, int Round), decimal> clubFantasyAgainst,
        IReadOnlyDictionary<(int ClubId, int Round), decimal> offensiveScoutsConcededByClubAndRound,
        IReadOnlyDictionary<(int ClubId, int Round), decimal> defensiveScoutsConcededByClubAndRound,
        IReadOnlyDictionary<(int ClubId, int Round), decimal> negativeScoutsConcededByClubAndRound,
        IReadOnlyDictionary<(int ClubId, int Round), decimal> balanceScoutsConcededByClubAndRound,
        IReadOnlyDictionary<(int ClubId, int Round), decimal> goalsScoutsConcededByClubAndRound,
        IReadOnlyDictionary<(int ClubId, int Round), decimal> assistsScoutsConcededByClubAndRound,
        IReadOnlyDictionary<(int ClubId, int Round), decimal> tacklesScoutsConcededByClubAndRound,
        IReadOnlyDictionary<(int ClubId, int PositionId, int Round), decimal> positionFantasyFor,
        IReadOnlyDictionary<(int ClubId, int PositionId, int Round), decimal> positionFantasyAgainst,
        IReadOnlyDictionary<(int ClubId, int PositionId, int Round), decimal> offensiveScoutsConcededByClubPositionAndRound,
        IReadOnlyDictionary<(int ClubId, int PositionId, int Round), decimal> defensiveScoutsConcededByClubPositionAndRound,
        IReadOnlyDictionary<(int ClubId, int PositionId, int Round), decimal> negativeScoutsConcededByClubPositionAndRound,
        IReadOnlyDictionary<(int ClubId, int PositionId, int Round), decimal> balanceScoutsConcededByClubPositionAndRound,
        IReadOnlyDictionary<(int ClubId, int PositionId, int Round), decimal> goalsScoutsConcededByClubPositionAndRound,
        IReadOnlyDictionary<(int ClubId, int PositionId, int Round), decimal> assistsScoutsConcededByClubPositionAndRound,
        IReadOnlyDictionary<(int ClubId, int PositionId, int Round), decimal> tacklesScoutsConcededByClubPositionAndRound,
        int clubId,
        int positionId,
        int targetRound,
        bool targetIsHomeMatch)
    {
        if (!clubHistoryByClubId.TryGetValue(clubId, out var history))
        {
            return default;
        }

        var last5 = history.Where(item => item.RoundId < targetRound).OrderByDescending(item => item.RoundId).Take(5).ToList();
        var last3 = last5.Take(3).ToList();
        var venueLast5 = history.Where(item => item.RoundId < targetRound && item.IsHomeMatch == targetIsHomeMatch).OrderByDescending(item => item.RoundId).Take(5).ToList();
        var venueLast3 = venueLast5.Take(3).ToList();

        var pointsPerMatchLast3 = Average(last3.Select(item => item.Points));
        var pointsPerMatchLast5 = Average(last5.Select(item => item.Points));
        var goalsForPerMatchLast3 = Average(last3.Select(item => item.GoalsFor));
        var goalsForPerMatchLast5 = Average(last5.Select(item => item.GoalsFor));
        var goalsAgainstPerMatchLast3 = Average(last3.Select(item => item.GoalsAgainst));
        var goalsAgainstPerMatchLast5 = Average(last5.Select(item => item.GoalsAgainst));
        var goalDifferencePerMatchLast3 = Average(last3.Select(item => item.GoalsFor - item.GoalsAgainst));
        var goalDifferencePerMatchLast5 = Average(last5.Select(item => item.GoalsFor - item.GoalsAgainst));
        var cleanSheetRateLast3 = Rate(last3.Select(item => item.GoalsAgainst == 0m));
        var cleanSheetRateLast5 = Rate(last5.Select(item => item.GoalsAgainst == 0m));
        var failedToScoreRateLast3 = Rate(last3.Select(item => item.GoalsFor == 0m));
        var failedToScoreRateLast5 = Rate(last5.Select(item => item.GoalsFor == 0m));
        var fantasyPointsForAverageLast3 = Average(last3.Select(item => Lookup(clubFantasyFor, (clubId, item.RoundId))));
        var fantasyPointsForAverageLast5 = Average(last5.Select(item => Lookup(clubFantasyFor, (clubId, item.RoundId))));
        var fantasyPointsAgainstAverageLast3 = Average(last3.Select(item => Lookup(clubFantasyAgainst, (clubId, item.RoundId))));
        var fantasyPointsAgainstAverageLast5 = Average(last5.Select(item => Lookup(clubFantasyAgainst, (clubId, item.RoundId))));
        var scoutOffensiveConcededAverageLast3 = Average(last3.Select(item => Lookup(offensiveScoutsConcededByClubAndRound, (clubId, item.RoundId))));
        var scoutOffensiveConcededAverageLast5 = Average(last5.Select(item => Lookup(offensiveScoutsConcededByClubAndRound, (clubId, item.RoundId))));
        var scoutDefensiveConcededAverageLast3 = Average(last3.Select(item => Lookup(defensiveScoutsConcededByClubAndRound, (clubId, item.RoundId))));
        var scoutDefensiveConcededAverageLast5 = Average(last5.Select(item => Lookup(defensiveScoutsConcededByClubAndRound, (clubId, item.RoundId))));
        var scoutNegativeConcededAverageLast3 = Average(last3.Select(item => Lookup(negativeScoutsConcededByClubAndRound, (clubId, item.RoundId))));
        var scoutNegativeConcededAverageLast5 = Average(last5.Select(item => Lookup(negativeScoutsConcededByClubAndRound, (clubId, item.RoundId))));
        var scoutBalanceConcededAverageLast3 = Average(last3.Select(item => Lookup(balanceScoutsConcededByClubAndRound, (clubId, item.RoundId))));
        var scoutBalanceConcededAverageLast5 = Average(last5.Select(item => Lookup(balanceScoutsConcededByClubAndRound, (clubId, item.RoundId))));
        var scoutGoalsConcededAverageLast3 = Average(last3.Select(item => Lookup(goalsScoutsConcededByClubAndRound, (clubId, item.RoundId))));
        var scoutGoalsConcededAverageLast5 = Average(last5.Select(item => Lookup(goalsScoutsConcededByClubAndRound, (clubId, item.RoundId))));
        var scoutAssistsConcededAverageLast3 = Average(last3.Select(item => Lookup(assistsScoutsConcededByClubAndRound, (clubId, item.RoundId))));
        var scoutAssistsConcededAverageLast5 = Average(last5.Select(item => Lookup(assistsScoutsConcededByClubAndRound, (clubId, item.RoundId))));
        var scoutTacklesConcededAverageLast3 = Average(last3.Select(item => Lookup(tacklesScoutsConcededByClubAndRound, (clubId, item.RoundId))));
        var scoutTacklesConcededAverageLast5 = Average(last5.Select(item => Lookup(tacklesScoutsConcededByClubAndRound, (clubId, item.RoundId))));
        var positionFantasyPointsForAverageLast3 = Average(last3.Select(item => Lookup(positionFantasyFor, (clubId, positionId, item.RoundId))));
        var positionFantasyPointsForAverageLast5 = Average(last5.Select(item => Lookup(positionFantasyFor, (clubId, positionId, item.RoundId))));
        var positionFantasyPointsAgainstAverageLast3 = Average(last3.Select(item => Lookup(positionFantasyAgainst, (clubId, positionId, item.RoundId))));
        var positionFantasyPointsAgainstAverageLast5 = Average(last5.Select(item => Lookup(positionFantasyAgainst, (clubId, positionId, item.RoundId))));
        var positionScoutOffensiveConcededAverageLast3 = Average(last3.Select(item => Lookup(offensiveScoutsConcededByClubPositionAndRound, (clubId, positionId, item.RoundId))));
        var positionScoutOffensiveConcededAverageLast5 = Average(last5.Select(item => Lookup(offensiveScoutsConcededByClubPositionAndRound, (clubId, positionId, item.RoundId))));
        var positionScoutDefensiveConcededAverageLast3 = Average(last3.Select(item => Lookup(defensiveScoutsConcededByClubPositionAndRound, (clubId, positionId, item.RoundId))));
        var positionScoutDefensiveConcededAverageLast5 = Average(last5.Select(item => Lookup(defensiveScoutsConcededByClubPositionAndRound, (clubId, positionId, item.RoundId))));
        var positionScoutNegativeConcededAverageLast3 = Average(last3.Select(item => Lookup(negativeScoutsConcededByClubPositionAndRound, (clubId, positionId, item.RoundId))));
        var positionScoutNegativeConcededAverageLast5 = Average(last5.Select(item => Lookup(negativeScoutsConcededByClubPositionAndRound, (clubId, positionId, item.RoundId))));
        var positionScoutBalanceConcededAverageLast3 = Average(last3.Select(item => Lookup(balanceScoutsConcededByClubPositionAndRound, (clubId, positionId, item.RoundId))));
        var positionScoutBalanceConcededAverageLast5 = Average(last5.Select(item => Lookup(balanceScoutsConcededByClubPositionAndRound, (clubId, positionId, item.RoundId))));
        var positionScoutGoalsConcededAverageLast3 = Average(last3.Select(item => Lookup(goalsScoutsConcededByClubPositionAndRound, (clubId, positionId, item.RoundId))));
        var positionScoutGoalsConcededAverageLast5 = Average(last5.Select(item => Lookup(goalsScoutsConcededByClubPositionAndRound, (clubId, positionId, item.RoundId))));
        var positionScoutAssistsConcededAverageLast3 = Average(last3.Select(item => Lookup(assistsScoutsConcededByClubPositionAndRound, (clubId, positionId, item.RoundId))));
        var positionScoutAssistsConcededAverageLast5 = Average(last5.Select(item => Lookup(assistsScoutsConcededByClubPositionAndRound, (clubId, positionId, item.RoundId))));
        var positionScoutTacklesConcededAverageLast3 = Average(last3.Select(item => Lookup(tacklesScoutsConcededByClubPositionAndRound, (clubId, positionId, item.RoundId))));
        var positionScoutTacklesConcededAverageLast5 = Average(last5.Select(item => Lookup(tacklesScoutsConcededByClubPositionAndRound, (clubId, positionId, item.RoundId))));
        var expectedPointsLast3 = Average(last3.Select(item => item.Points));
        var expectedPointsLast5 = Average(last5.Select(item => item.Points));

        return new ClubContextSnapshot(
            last5.Count,
            last3.Count,
            last5.Count,
            pointsPerMatchLast3,
            pointsPerMatchLast5,
            AverageOrFallback(venueLast3.Select(item => item.Points), last3.Select(item => item.Points)),
            AverageOrFallback(venueLast5.Select(item => item.Points), last5.Select(item => item.Points)),
            goalsForPerMatchLast3,
            goalsForPerMatchLast5,
            goalsAgainstPerMatchLast3,
            goalsAgainstPerMatchLast5,
            goalDifferencePerMatchLast3,
            goalDifferencePerMatchLast5,
            AverageOrFallback(venueLast3.Select(item => item.GoalsFor), last3.Select(item => item.GoalsFor)),
            AverageOrFallback(venueLast5.Select(item => item.GoalsFor), last5.Select(item => item.GoalsFor)),
            AverageOrFallback(venueLast3.Select(item => item.GoalsAgainst), last3.Select(item => item.GoalsAgainst)),
            AverageOrFallback(venueLast5.Select(item => item.GoalsAgainst), last5.Select(item => item.GoalsAgainst)),
            cleanSheetRateLast3,
            cleanSheetRateLast5,
            failedToScoreRateLast3,
            failedToScoreRateLast5,
            fantasyPointsForAverageLast3,
            fantasyPointsForAverageLast5,
            fantasyPointsAgainstAverageLast3,
            fantasyPointsAgainstAverageLast5,
            scoutOffensiveConcededAverageLast3,
            scoutOffensiveConcededAverageLast5,
            scoutDefensiveConcededAverageLast3,
            scoutDefensiveConcededAverageLast5,
            scoutNegativeConcededAverageLast3,
            scoutNegativeConcededAverageLast5,
            scoutBalanceConcededAverageLast3,
            scoutBalanceConcededAverageLast5,
            scoutGoalsConcededAverageLast3,
            scoutGoalsConcededAverageLast5,
            scoutAssistsConcededAverageLast3,
            scoutAssistsConcededAverageLast5,
            scoutTacklesConcededAverageLast3,
            scoutTacklesConcededAverageLast5,
            positionFantasyPointsForAverageLast3,
            positionFantasyPointsForAverageLast5,
            positionFantasyPointsAgainstAverageLast3,
            positionFantasyPointsAgainstAverageLast5,
            positionScoutOffensiveConcededAverageLast3,
            positionScoutOffensiveConcededAverageLast5,
            positionScoutDefensiveConcededAverageLast3,
            positionScoutDefensiveConcededAverageLast5,
            positionScoutNegativeConcededAverageLast3,
            positionScoutNegativeConcededAverageLast5,
            positionScoutBalanceConcededAverageLast3,
            positionScoutBalanceConcededAverageLast5,
            positionScoutGoalsConcededAverageLast3,
            positionScoutGoalsConcededAverageLast5,
            positionScoutAssistsConcededAverageLast3,
            positionScoutAssistsConcededAverageLast5,
            positionScoutTacklesConcededAverageLast3,
            positionScoutTacklesConcededAverageLast5,
            expectedPointsLast3,
            Rate(last5.Select(item => item.Points == 3m)),
            Rate(last5.Select(item => item.Points == 1m)),
            Rate(last5.Select(item => item.Points == 0m)),
            expectedPointsLast5,
            ComputeAttackStrength(goalsForPerMatchLast3, fantasyPointsForAverageLast3, positionFantasyPointsForAverageLast3),
            ComputeAttackStrength(goalsForPerMatchLast5, fantasyPointsForAverageLast5, positionFantasyPointsForAverageLast5),
            ComputeDefenseStrength(cleanSheetRateLast3, goalsAgainstPerMatchLast3, fantasyPointsAgainstAverageLast3, positionFantasyPointsAgainstAverageLast3),
            ComputeDefenseStrength(cleanSheetRateLast5, goalsAgainstPerMatchLast5, fantasyPointsAgainstAverageLast5, positionFantasyPointsAgainstAverageLast5));
    }

    private static void AddAthleteObservation(IDictionary<int, List<AthleteObservation>> history, int athleteId, AthleteObservation observation)
    {
        if (!history.TryGetValue(athleteId, out var values))
        {
            values = [];
            history[athleteId] = values;
        }

        values.Add(observation);
    }

    private static void AddClubHistory(IDictionary<int, List<ClubHistoryPoint>> history, int clubId, ClubHistoryPoint point)
    {
        if (!history.TryGetValue(clubId, out var values))
        {
            values = [];
            history[clubId] = values;
        }

        values.Add(point);
    }

    private static void Accumulate<TKey>(IDictionary<TKey, decimal> dictionary, TKey key, decimal value)
        where TKey : notnull
        => dictionary[key] = dictionary.TryGetValue(key, out var current) ? current + value : value;

    private static void Accumulate<TKey>(IDictionary<TKey, int> dictionary, TKey key, int value)
        where TKey : notnull
        => dictionary[key] = dictionary.TryGetValue(key, out var current) ? current + value : value;

    private static decimal Lookup<TKey>(IReadOnlyDictionary<TKey, decimal> dictionary, TKey key)
        where TKey : notnull
        => dictionary.TryGetValue(key, out var value) ? value : 0m;

    private static decimal Scout(IReadOnlyDictionary<string, decimal> scouts, string key)
        => scouts.TryGetValue(key, out var value) ? value : 0m;

    private static decimal ResolveMatchPoints(int? goalsFor, int? goalsAgainst)
        => !goalsFor.HasValue || !goalsAgainst.HasValue ? 0m : goalsFor > goalsAgainst ? 3m : goalsFor == goalsAgainst ? 1m : 0m;

    private static decimal ConvertFormToPoints(string? form)
    {
        if (string.IsNullOrWhiteSpace(form))
        {
            return 0m;
        }

        var score = 0m;
        foreach (var item in form.Trim().ToUpperInvariant())
        {
            score += item switch
            {
                'V' => 3m,
                'E' => 1m,
                _ => 0m,
            };
        }

        return score;
    }

    private static decimal Average(IEnumerable<decimal> values)
    {
        var materialized = values.ToList();
        return materialized.Count == 0 ? 0m : materialized.Average();
    }

    private static decimal Sum(IEnumerable<decimal> values)
    {
        var materialized = values.ToList();
        return materialized.Count == 0 ? 0m : materialized.Sum();
    }

    private static decimal SafeDivide(decimal numerator, decimal denominator)
        => denominator == 0m ? 0m : numerator / denominator;

    private static decimal Shrink(decimal observed, int sampleSize, decimal prior, int priorWeight)
    {
        if (sampleSize <= 0)
        {
            return prior;
        }

        return ((observed * sampleSize) + (prior * priorWeight)) / (sampleSize + priorWeight);
    }

    private static decimal ComputeAttackStrength(decimal goalsForPerMatch, decimal fantasyPointsForAverage, decimal positionFantasyPointsForAverage)
        => goalsForPerMatch + (fantasyPointsForAverage / 10m) + (positionFantasyPointsForAverage / 10m);

    private static decimal ComputeDefenseStrength(decimal cleanSheetRate, decimal goalsAgainstPerMatch, decimal fantasyPointsAgainstAverage, decimal positionFantasyPointsAgainstAverage)
        => (2m * cleanSheetRate) - goalsAgainstPerMatch - (fantasyPointsAgainstAverage / 10m) - (positionFantasyPointsAgainstAverage / 10m);

    private static decimal ComputePriceTrendLast3(IEnumerable<decimal> values)
    {
        var materialized = values.ToList();
        if (materialized.Count < 2)
        {
            return 0m;
        }

        return materialized[0] - Average(materialized.Skip(1));
    }

    private static decimal AverageOrFallback(IEnumerable<decimal> primary, IEnumerable<decimal> fallback)
    {
        var primaryList = primary.ToList();
        if (primaryList.Count > 0)
        {
            return primaryList.Average();
        }

        var fallbackList = fallback.ToList();
        return fallbackList.Count == 0 ? 0m : fallbackList.Average();
    }

    private static decimal Rate(IEnumerable<bool> predicateResults)
    {
        var materialized = predicateResults.ToList();
        return materialized.Count == 0 ? 0m : (decimal)materialized.Count(item => item) / materialized.Count;
    }

    private static decimal Ewma(IEnumerable<decimal> values)
    {
        var ordered = values.Reverse().ToList();
        if (ordered.Count == 0)
        {
            return 0m;
        }

        var alpha = 2m / (ordered.Count + 1m);
        var result = ordered[0];
        for (var index = 1; index < ordered.Count; index++)
        {
            result = (alpha * ordered[index]) + ((1m - alpha) * result);
        }

        return result;
    }

    private static decimal StandardDeviation(IEnumerable<decimal> values)
    {
        var materialized = values.ToList();
        if (materialized.Count == 0)
        {
            return 0m;
        }

        var average = materialized.Average();
        var variance = materialized.Average(item => (item - average) * (item - average));
        return (decimal)Math.Sqrt((double)variance);
    }

    private static decimal Min(IEnumerable<decimal> values)
    {
        var materialized = values.ToList();
        return materialized.Count == 0 ? 0m : materialized.Min();
    }

    private static decimal Max(IEnumerable<decimal> values)
    {
        var materialized = values.ToList();
        return materialized.Count == 0 ? 0m : materialized.Max();
    }

    private readonly record struct AthleteObservation(int RoundId, int PositionId, bool? IsHomeMatch, int StatusId, bool WasInGame, decimal Price, decimal Score, decimal OffensiveScoutScore, decimal DefensiveScoutScore, decimal NegativeScoutScore, decimal GoalsCount, decimal AssistsCount, decimal TacklesCount, decimal CleanSheetCount)
    {
        public decimal PositiveScoutScore => OffensiveScoutScore + DefensiveScoutScore;

        public decimal BalanceScoutScore => PositiveScoutScore - NegativeScoutScore;
    }
    private readonly record struct ClubHistoryPoint(int RoundId, bool IsHomeMatch, decimal Points, decimal GoalsFor, decimal GoalsAgainst);
    private readonly record struct AthleteHistorySnapshot(decimal GamesNumPrior, decimal HomeGamesNumPrior, decimal AwayGamesNumPrior, decimal VenueGamesNumPrior, decimal PlayedMatchesSampleSize, decimal PlayedMatchesSampleSizeLast5, decimal ObservationCountLast3, decimal ObservationCountLast5, decimal PreviousObservationCountLast3, decimal HomeObservationCountLast5, decimal AwayObservationCountLast5, decimal VenueObservationCountLast5, decimal StatusMatchedObservationCountLast5, decimal HistoryCoverageLast3, decimal HistoryCoverageLast5, decimal HomeHistoryCoverageLast5, decimal AwayHistoryCoverageLast5, decimal VenueHistoryCoverageLast5, decimal CurrentStatusCoverageLast5, decimal IsColdStartAthlete, decimal MovingAveragePrior, decimal AverageScorePrior, decimal HomeAverageScorePrior, decimal AwayAverageScorePrior, decimal VenueAverageScorePrior, decimal ScoreLast1, decimal ScoreAverageLast3, decimal ScoreAverageLast5, decimal ScoreEwmaLast3, decimal ScoreEwmaLast5, decimal ScoreStdLast5, decimal ScoreFloorLast5, decimal ScoreCeilingLast5, decimal ScoreHitRateOver3Last5, decimal ScoreHitRateOver5Last5, decimal ScoreHitRateOver8Last5, decimal ScoreNegativeRateLast5, decimal ScoreTrendLast3VsPrev3, decimal HomeScoreAverageLast5, decimal AwayScoreAverageLast5, decimal VenueScoreAverageLast5, decimal HomeScoreEwmaLast3, decimal AwayScoreEwmaLast3, decimal VenueScoreEwmaLast3, decimal ScoutOffensiveSumLast3, decimal ScoutOffensiveSumLast5, decimal ScoutOffensiveAverageLast3, decimal ScoutOffensiveAverageLast5, decimal ScoutOffensiveEwmaLast3, decimal ScoutOffensiveEwmaLast5, decimal ScoutDefensiveSumLast3, decimal ScoutDefensiveSumLast5, decimal ScoutDefensiveAverageLast3, decimal ScoutDefensiveAverageLast5, decimal ScoutDefensiveEwmaLast3, decimal ScoutDefensiveEwmaLast5, decimal ScoutNegativeSumLast3, decimal ScoutNegativeSumLast5, decimal ScoutNegativeAverageLast3, decimal ScoutNegativeAverageLast5, decimal ScoutNegativeEwmaLast3, decimal ScoutNegativeEwmaLast5, decimal ScoutBalanceSumLast3, decimal ScoutBalanceSumLast5, decimal ScoutBalanceAverageLast3, decimal ScoutBalanceAverageLast5, decimal ScoutBalanceEwmaLast3, decimal ScoutBalanceEwmaLast5, decimal ScoutGoalsAverageLast3, decimal ScoutGoalsAverageLast5, decimal ScoutAssistsAverageLast3, decimal ScoutAssistsAverageLast5, decimal ScoutTacklesAverageLast3, decimal ScoutTacklesAverageLast5, decimal ScoutCleanSheetRateLast3, decimal ScoutCleanSheetRateLast5, decimal PlayRateLast5, decimal RoundsSinceLastInGame, decimal StatusStabilityLast5, decimal StatusChangeFlag, decimal EmpiricalPlayRateForCurrentStatus, decimal PriceTrendLast3, decimal PriceVolatilityLast5, decimal HistoricalPointsPerCartoletaLast3, decimal HistoricalPointsPerCartoletaLast5, decimal PositionRelativePriceSignal, decimal PriceDeltaVsHistoricalAverage);
    private readonly record struct AthleteHistoryPrior(AthleteHistorySnapshot Snapshot, decimal HistoricalAveragePrice);
    private readonly record struct ClubContextSnapshot(int MatchesSampleSize, int MatchesSampleSizeLast3, int MatchesSampleSizeLast5, decimal PointsPerMatchLast3, decimal PointsPerMatchLast5, decimal VenuePointsPerMatchLast3, decimal VenuePointsPerMatchLast5, decimal GoalsForPerMatchLast3, decimal GoalsForPerMatchLast5, decimal GoalsAgainstPerMatchLast3, decimal GoalsAgainstPerMatchLast5, decimal GoalDifferencePerMatchLast3, decimal GoalDifferencePerMatchLast5, decimal VenueGoalsForPerMatchLast3, decimal VenueGoalsForPerMatchLast5, decimal VenueGoalsAgainstPerMatchLast3, decimal VenueGoalsAgainstPerMatchLast5, decimal CleanSheetRateLast3, decimal CleanSheetRateLast5, decimal FailedToScoreRateLast3, decimal FailedToScoreRateLast5, decimal FantasyPointsForAverageLast3, decimal FantasyPointsForAverageLast5, decimal FantasyPointsAgainstAverageLast3, decimal FantasyPointsAgainstAverageLast5, decimal ScoutOffensiveConcededAverageLast3, decimal ScoutOffensiveConcededAverageLast5, decimal ScoutDefensiveConcededAverageLast3, decimal ScoutDefensiveConcededAverageLast5, decimal ScoutNegativeConcededAverageLast3, decimal ScoutNegativeConcededAverageLast5, decimal ScoutBalanceConcededAverageLast3, decimal ScoutBalanceConcededAverageLast5, decimal ScoutGoalsConcededAverageLast3, decimal ScoutGoalsConcededAverageLast5, decimal ScoutAssistsConcededAverageLast3, decimal ScoutAssistsConcededAverageLast5, decimal ScoutTacklesConcededAverageLast3, decimal ScoutTacklesConcededAverageLast5, decimal PositionFantasyPointsForAverageLast3, decimal PositionFantasyPointsForAverageLast5, decimal PositionFantasyPointsAgainstAverageLast3, decimal PositionFantasyPointsAgainstAverageLast5, decimal PositionScoutOffensiveConcededAverageLast3, decimal PositionScoutOffensiveConcededAverageLast5, decimal PositionScoutDefensiveConcededAverageLast3, decimal PositionScoutDefensiveConcededAverageLast5, decimal PositionScoutNegativeConcededAverageLast3, decimal PositionScoutNegativeConcededAverageLast5, decimal PositionScoutBalanceConcededAverageLast3, decimal PositionScoutBalanceConcededAverageLast5, decimal PositionScoutGoalsConcededAverageLast3, decimal PositionScoutGoalsConcededAverageLast5, decimal PositionScoutAssistsConcededAverageLast3, decimal PositionScoutAssistsConcededAverageLast5, decimal PositionScoutTacklesConcededAverageLast3, decimal PositionScoutTacklesConcededAverageLast5, decimal ExpectedPointsLast3, decimal WinRateLast5, decimal DrawRateLast5, decimal LossRateLast5, decimal ExpectedPointsLast5, decimal AttackStrengthLast3, decimal AttackStrengthLast5, decimal DefenseStrengthLast3, decimal DefenseStrengthLast5);
}
