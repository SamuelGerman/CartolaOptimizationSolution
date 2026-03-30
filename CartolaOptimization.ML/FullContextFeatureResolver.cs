using CartolaOptimization.Application.Models;
using CartolaOptimization.Domain.Entities;
using CartolaOptimization.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CartolaOptimization.ML;

internal static class FullContextFeatureResolver
{
    internal static async Task<FullContextFeatureLookup> LoadFeatureLookupAsync(
        AppDbContext dbContext,
        IReadOnlyCollection<int> rounds,
        CancellationToken cancellationToken)
    {
        if (rounds.Count == 0)
        {
            return new FullContextFeatureLookup
            {
                MatchByRoundAndClubId = new Dictionary<(int Round, int ClubId), Match>(),
                AthleteHistoryByAthleteId = new Dictionary<int, IReadOnlyList<AthleteHistoryObservation>>(),
                ClubMatchHistoryByClubId = new Dictionary<int, IReadOnlyList<ClubMatchHistoryPoint>>(),
                FantasyPointsForByClubAndRound = new Dictionary<(int ClubId, int Round), decimal>(),
                FantasyPointsAgainstByClubAndRound = new Dictionary<(int ClubId, int Round), decimal>(),
                PositionFantasyPointsForByClubPositionAndRound = new Dictionary<(int ClubId, int PositionId, int Round), decimal>(),
                PositionFantasyPointsAgainstByClubPositionAndRound = new Dictionary<(int ClubId, int PositionId, int Round), decimal>(),
                AveragePriceByPositionAndRound = new Dictionary<(int PositionId, int Round), decimal>(),
                OffensiveScoutsConcededByClubAndRound = new Dictionary<(int ClubId, int Round), decimal>(),
                DefensiveScoutsConcededByClubAndRound = new Dictionary<(int ClubId, int Round), decimal>(),
                NegativeScoutsConcededByClubAndRound = new Dictionary<(int ClubId, int Round), decimal>(),
                BalanceScoutsConcededByClubAndRound = new Dictionary<(int ClubId, int Round), decimal>(),
                GoalsScoutsConcededByClubAndRound = new Dictionary<(int ClubId, int Round), decimal>(),
                AssistsScoutsConcededByClubAndRound = new Dictionary<(int ClubId, int Round), decimal>(),
                TacklesScoutsConcededByClubAndRound = new Dictionary<(int ClubId, int Round), decimal>(),
                OffensiveScoutsConcededByClubPositionAndRound = new Dictionary<(int ClubId, int PositionId, int Round), decimal>(),
                DefensiveScoutsConcededByClubPositionAndRound = new Dictionary<(int ClubId, int PositionId, int Round), decimal>(),
                NegativeScoutsConcededByClubPositionAndRound = new Dictionary<(int ClubId, int PositionId, int Round), decimal>(),
                BalanceScoutsConcededByClubPositionAndRound = new Dictionary<(int ClubId, int PositionId, int Round), decimal>(),
                GoalsScoutsConcededByClubPositionAndRound = new Dictionary<(int ClubId, int PositionId, int Round), decimal>(),
                AssistsScoutsConcededByClubPositionAndRound = new Dictionary<(int ClubId, int PositionId, int Round), decimal>(),
                TacklesScoutsConcededByClubPositionAndRound = new Dictionary<(int ClubId, int PositionId, int Round), decimal>(),
            };
        }

        var maxRound = rounds.Max();
        var matches = await dbContext.Matches
            .AsNoTracking()
            .Include(item => item.HomeClub)
            .Include(item => item.AwayClub)
            .Where(item => item.ValidForCartola && item.Round <= maxRound)
            .ToListAsync(cancellationToken);

        var matchByRoundAndClubId = new Dictionary<(int Round, int ClubId), Match>();
        var clubHistoryByClubId = new Dictionary<int, List<ClubMatchHistoryPoint>>();
        foreach (var match in matches)
        {
            matchByRoundAndClubId[(match.Round, match.HomeClubId)] = match;
            matchByRoundAndClubId[(match.Round, match.AwayClubId)] = match;

            AddClubHistoryPoint(
                clubHistoryByClubId,
                match.HomeClubId,
                new ClubMatchHistoryPoint(
                    match.Round,
                    true,
                    match.AwayClubId,
                    ResolveMatchPoints(match.HomeScore, match.AwayScore),
                    match.HomeScore ?? 0,
                    match.AwayScore ?? 0));

            AddClubHistoryPoint(
                clubHistoryByClubId,
                match.AwayClubId,
                new ClubMatchHistoryPoint(
                    match.Round,
                    false,
                    match.HomeClubId,
                    ResolveMatchPoints(match.AwayScore, match.HomeScore),
                    match.AwayScore ?? 0,
                    match.HomeScore ?? 0));
        }

        var historicalAthleteRounds = await dbContext.AthleteRounds
            .AsNoTracking()
            .Include(item => item.Scouts)
            .Where(item =>
                item.DataPhase == Domain.Constants.RoundDataPhaseValues.HistoricalClosed &&
                item.ClubId != 0 &&
                item.PositionId != 0 &&
                item.StatusId != 0 &&
                item.RoundId <= maxRound)
            .OrderByDescending(item => item.RoundId)
            .ThenByDescending(item => item.Id)
            .ToListAsync(cancellationToken);

        var athleteHistoryByAthleteId = new Dictionary<int, List<AthleteHistoryObservation>>();
        var fantasyPointsForByClubAndRound = new Dictionary<(int ClubId, int Round), decimal>();
        var fantasyPointsAgainstByClubAndRound = new Dictionary<(int ClubId, int Round), decimal>();
        var positionFantasyPointsForByClubPositionAndRound = new Dictionary<(int ClubId, int PositionId, int Round), decimal>();
        var positionFantasyPointsAgainstByClubPositionAndRound = new Dictionary<(int ClubId, int PositionId, int Round), decimal>();
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
            var scoutValues = athleteRound.Scouts.ToDictionary(item => item.ScoutKey, item => item.Valor, StringComparer.OrdinalIgnoreCase);
            var isHomeMatch = matchByRoundAndClubId.TryGetValue((athleteRound.RoundId, athleteRound.ClubId), out var match)
                ? (bool?)(match.HomeClubId == athleteRound.ClubId)
                : null;

            AddAthleteHistoryObservation(
                athleteHistoryByAthleteId,
                athleteRound.AthleteId,
                new AthleteHistoryObservation(
                    athleteRound.RoundId,
                    isHomeMatch,
                    athleteRound.StatusId,
                    athleteRound.WasInGame,
                    athleteRound.PriceNum,
                    athleteRound.ScoreNum,
                    GetScoutValue(scoutValues, "G") + GetScoutValue(scoutValues, "A") + GetScoutValue(scoutValues, "FS") + GetScoutValue(scoutValues, "FD") + GetScoutValue(scoutValues, "FF"),
                    GetScoutValue(scoutValues, "DS") + GetScoutValue(scoutValues, "SG"),
                    GetScoutValue(scoutValues, "GC") + GetScoutValue(scoutValues, "CA") + GetScoutValue(scoutValues, "CV") + GetScoutValue(scoutValues, "PP"),
                    GetScoutValue(scoutValues, "G"),
                    GetScoutValue(scoutValues, "A"),
                    GetScoutValue(scoutValues, "DS"),
                    GetScoutValue(scoutValues, "SG") > 0m ? 1m : 0m));

            if (match is null)
            {
                continue;
            }

            var opponentClubId = match.HomeClubId == athleteRound.ClubId ? match.AwayClubId : match.HomeClubId;

            Accumulate(fantasyPointsForByClubAndRound, (athleteRound.ClubId, athleteRound.RoundId), athleteRound.ScoreNum);
            Accumulate(fantasyPointsAgainstByClubAndRound, (opponentClubId, athleteRound.RoundId), athleteRound.ScoreNum);
            Accumulate(positionFantasyPointsForByClubPositionAndRound, (athleteRound.ClubId, athleteRound.PositionId, athleteRound.RoundId), athleteRound.ScoreNum);
            Accumulate(positionFantasyPointsAgainstByClubPositionAndRound, (opponentClubId, athleteRound.PositionId, athleteRound.RoundId), athleteRound.ScoreNum);
            Accumulate(priceSumByPositionAndRound, (athleteRound.PositionId, athleteRound.RoundId), athleteRound.PriceNum);
            Accumulate(priceCountByPositionAndRound, (athleteRound.PositionId, athleteRound.RoundId), 1);
            Accumulate(offensiveScoutsConcededByClubAndRound, (opponentClubId, athleteRound.RoundId), GetScoutValue(scoutValues, "G") + GetScoutValue(scoutValues, "A") + GetScoutValue(scoutValues, "FS") + GetScoutValue(scoutValues, "FD") + GetScoutValue(scoutValues, "FF"));
            Accumulate(defensiveScoutsConcededByClubAndRound, (opponentClubId, athleteRound.RoundId), GetScoutValue(scoutValues, "DS") + GetScoutValue(scoutValues, "SG"));
            Accumulate(negativeScoutsConcededByClubAndRound, (opponentClubId, athleteRound.RoundId), GetScoutValue(scoutValues, "GC") + GetScoutValue(scoutValues, "CA") + GetScoutValue(scoutValues, "CV") + GetScoutValue(scoutValues, "PP"));
            Accumulate(balanceScoutsConcededByClubAndRound, (opponentClubId, athleteRound.RoundId), (GetScoutValue(scoutValues, "G") + GetScoutValue(scoutValues, "A") + GetScoutValue(scoutValues, "FS") + GetScoutValue(scoutValues, "FD") + GetScoutValue(scoutValues, "FF") + GetScoutValue(scoutValues, "DS") + GetScoutValue(scoutValues, "SG")) - (GetScoutValue(scoutValues, "GC") + GetScoutValue(scoutValues, "CA") + GetScoutValue(scoutValues, "CV") + GetScoutValue(scoutValues, "PP")));
            Accumulate(goalsScoutsConcededByClubAndRound, (opponentClubId, athleteRound.RoundId), GetScoutValue(scoutValues, "G"));
            Accumulate(assistsScoutsConcededByClubAndRound, (opponentClubId, athleteRound.RoundId), GetScoutValue(scoutValues, "A"));
            Accumulate(tacklesScoutsConcededByClubAndRound, (opponentClubId, athleteRound.RoundId), GetScoutValue(scoutValues, "DS"));
            Accumulate(offensiveScoutsConcededByClubPositionAndRound, (opponentClubId, athleteRound.PositionId, athleteRound.RoundId), GetScoutValue(scoutValues, "G") + GetScoutValue(scoutValues, "A") + GetScoutValue(scoutValues, "FS") + GetScoutValue(scoutValues, "FD") + GetScoutValue(scoutValues, "FF"));
            Accumulate(defensiveScoutsConcededByClubPositionAndRound, (opponentClubId, athleteRound.PositionId, athleteRound.RoundId), GetScoutValue(scoutValues, "DS") + GetScoutValue(scoutValues, "SG"));
            Accumulate(negativeScoutsConcededByClubPositionAndRound, (opponentClubId, athleteRound.PositionId, athleteRound.RoundId), GetScoutValue(scoutValues, "GC") + GetScoutValue(scoutValues, "CA") + GetScoutValue(scoutValues, "CV") + GetScoutValue(scoutValues, "PP"));
            Accumulate(balanceScoutsConcededByClubPositionAndRound, (opponentClubId, athleteRound.PositionId, athleteRound.RoundId), (GetScoutValue(scoutValues, "G") + GetScoutValue(scoutValues, "A") + GetScoutValue(scoutValues, "FS") + GetScoutValue(scoutValues, "FD") + GetScoutValue(scoutValues, "FF") + GetScoutValue(scoutValues, "DS") + GetScoutValue(scoutValues, "SG")) - (GetScoutValue(scoutValues, "GC") + GetScoutValue(scoutValues, "CA") + GetScoutValue(scoutValues, "CV") + GetScoutValue(scoutValues, "PP")));
            Accumulate(goalsScoutsConcededByClubPositionAndRound, (opponentClubId, athleteRound.PositionId, athleteRound.RoundId), GetScoutValue(scoutValues, "G"));
            Accumulate(assistsScoutsConcededByClubPositionAndRound, (opponentClubId, athleteRound.PositionId, athleteRound.RoundId), GetScoutValue(scoutValues, "A"));
            Accumulate(tacklesScoutsConcededByClubPositionAndRound, (opponentClubId, athleteRound.PositionId, athleteRound.RoundId), GetScoutValue(scoutValues, "DS"));
        }

        var averagePriceByPositionAndRound = priceSumByPositionAndRound.ToDictionary(
            item => item.Key,
            item => item.Value / priceCountByPositionAndRound[item.Key]);

        return new FullContextFeatureLookup
        {
            MatchByRoundAndClubId = matchByRoundAndClubId,
            AthleteHistoryByAthleteId = athleteHistoryByAthleteId.ToDictionary(
                item => item.Key,
                item => (IReadOnlyList<AthleteHistoryObservation>)item.Value.OrderByDescending(value => value.RoundId).ToList()),
            ClubMatchHistoryByClubId = clubHistoryByClubId.ToDictionary(
                item => item.Key,
                item => (IReadOnlyList<ClubMatchHistoryPoint>)item.Value.OrderByDescending(value => value.RoundId).ToList()),
            FantasyPointsForByClubAndRound = fantasyPointsForByClubAndRound,
            FantasyPointsAgainstByClubAndRound = fantasyPointsAgainstByClubAndRound,
            PositionFantasyPointsForByClubPositionAndRound = positionFantasyPointsForByClubPositionAndRound,
            PositionFantasyPointsAgainstByClubPositionAndRound = positionFantasyPointsAgainstByClubPositionAndRound,
            AveragePriceByPositionAndRound = averagePriceByPositionAndRound,
            OffensiveScoutsConcededByClubAndRound = offensiveScoutsConcededByClubAndRound,
            DefensiveScoutsConcededByClubAndRound = defensiveScoutsConcededByClubAndRound,
            NegativeScoutsConcededByClubAndRound = negativeScoutsConcededByClubAndRound,
            BalanceScoutsConcededByClubAndRound = balanceScoutsConcededByClubAndRound,
            GoalsScoutsConcededByClubAndRound = goalsScoutsConcededByClubAndRound,
            AssistsScoutsConcededByClubAndRound = assistsScoutsConcededByClubAndRound,
            TacklesScoutsConcededByClubAndRound = tacklesScoutsConcededByClubAndRound,
            OffensiveScoutsConcededByClubPositionAndRound = offensiveScoutsConcededByClubPositionAndRound,
            DefensiveScoutsConcededByClubPositionAndRound = defensiveScoutsConcededByClubPositionAndRound,
            NegativeScoutsConcededByClubPositionAndRound = negativeScoutsConcededByClubPositionAndRound,
            BalanceScoutsConcededByClubPositionAndRound = balanceScoutsConcededByClubPositionAndRound,
            GoalsScoutsConcededByClubPositionAndRound = goalsScoutsConcededByClubPositionAndRound,
            AssistsScoutsConcededByClubPositionAndRound = assistsScoutsConcededByClubPositionAndRound,
            TacklesScoutsConcededByClubPositionAndRound = tacklesScoutsConcededByClubPositionAndRound,
        };
    }

    internal static bool TryBuildTrainingRow(
        AthleteRound athleteRound,
        FullContextFeatureLookup lookup,
        out FullContextAthleteScoreTrainingRow row)
    {
        row = null!;

        if (!TryResolveContext(athleteRound.RoundId, athleteRound, lookup, out var context))
        {
            return false;
        }

        row = new FullContextAthleteScoreTrainingRow
        {
            RoundId = athleteRound.RoundId,
            ClubCartolaId = athleteRound.Club.CartolaId,
            OpponentClubCartolaId = context.OpponentClubCartolaId,
            PositionCartolaId = athleteRound.Position.CartolaId,
            StatusCartolaId = athleteRound.Status.CartolaId,
            GamesNum = athleteRound.GamesNum,
            HomeGamesNum = athleteRound.HomeGamesNum,
            AwayGamesNum = athleteRound.AwayGamesNum,
            GamesAtVenueNum = context.IsHomeMatch ? athleteRound.HomeGamesNum : athleteRound.AwayGamesNum,
            MovingAverage = (float)athleteRound.MovingAverage,
            AverageScore = (float)athleteRound.AverageScoreNum,
            HomeAverageScore = (float)athleteRound.HomeAverageScore,
            AwayAverageScore = (float)athleteRound.AwayAverageScore,
            ContextualAverageScore = context.IsHomeMatch ? (float)athleteRound.HomeAverageScore : (float)athleteRound.AwayAverageScore,
            PriceNum = (float)athleteRound.PriceNum,
            VariationNum = (float)athleteRound.VariationNum,
            HasAthleteContext = athleteRound.Context is null ? 0f : 1f,
            TeamAverageScore = (float)(athleteRound.Context?.TeamAverageScore ?? 0m),
            TeamHomeAverageCurrent = (float)(athleteRound.Context?.TeamHomeAverageCurrent ?? 0m),
            TeamHomeAveragePrevious = (float)(athleteRound.Context?.TeamHomeAveragePrevious ?? 0m),
            TeamAwayAverageCurrent = (float)(athleteRound.Context?.TeamAwayAverageCurrent ?? 0m),
            TeamAwayAveragePrevious = (float)(athleteRound.Context?.TeamAwayAveragePrevious ?? 0m),
            TeamVenueAverageCurrent = context.IsHomeMatch ? (float)(athleteRound.Context?.TeamHomeAverageCurrent ?? 0m) : (float)(athleteRound.Context?.TeamAwayAverageCurrent ?? 0m),
            TeamVenueAveragePrevious = context.IsHomeMatch ? (float)(athleteRound.Context?.TeamHomeAveragePrevious ?? 0m) : (float)(athleteRound.Context?.TeamAwayAveragePrevious ?? 0m),
            PointsPerCartoletaCurrent = (float)(athleteRound.Context?.PointsPerCartoletaCurrent ?? 0m),
            PointsPerCartoletaPrevious = (float)(athleteRound.Context?.PointsPerCartoletaPrevious ?? 0m),
            GrossOverallAverageCurrent = (float)(athleteRound.Context?.GrossOverallAverageCurrent ?? 0m),
            GrossOverallMovingAverageCurrent = (float)(athleteRound.Context?.GrossOverallMovingAverageCurrent ?? 0m),
            GrossOverallAveragePrevious = (float)(athleteRound.Context?.GrossOverallAveragePrevious ?? 0m),
            GrossOverallMovingAveragePrevious = (float)(athleteRound.Context?.GrossOverallMovingAveragePrevious ?? 0m),
            NetOverallAverageCurrent = (float)(athleteRound.Context?.NetOverallAverageCurrent ?? 0m),
            NetOverallMovingAverageCurrent = (float)(athleteRound.Context?.NetOverallMovingAverageCurrent ?? 0m),
            NetOverallAveragePrevious = (float)(athleteRound.Context?.NetOverallAveragePrevious ?? 0m),
            NetOverallMovingAveragePrevious = (float)(athleteRound.Context?.NetOverallMovingAveragePrevious ?? 0m),
            ValuationCurrent = (float)(athleteRound.Context?.ValuationCurrent ?? 0m),
            MinimumToAppreciateCurrent = (float)(athleteRound.Context?.MinimumToAppreciateCurrent ?? 0m),
            ValuationPrevious = (float)(athleteRound.Context?.ValuationPrevious ?? 0m),
            MinimumToAppreciatePrevious = (float)(athleteRound.Context?.MinimumToAppreciatePrevious ?? 0m),
            IndexPointsPerCartoleta = (float)(athleteRound.Context?.IndexPointsPerCartoleta ?? 0m),
            IndexOffensiveUsage = (float)(athleteRound.Context?.IndexOffensiveUsage ?? 0m),
            IndexOffensiveRepresentation = (float)(athleteRound.Context?.IndexOffensiveRepresentation ?? 0m),
            IndexPointsRepresentation = (float)(athleteRound.Context?.IndexPointsRepresentation ?? 0m),
            IndexOffensiveEfficiency = (float)(athleteRound.Context?.IndexOffensiveEfficiency ?? 0m),
            IndexDefensiveEfficiency = (float)(athleteRound.Context?.IndexDefensiveEfficiency ?? 0m),
            IndexTackleDuelPercent = (float)(athleteRound.Context?.IndexTackleDuelPercent ?? 0m),
            IndexShotsPerGame = (float)(athleteRound.Context?.IndexShotsPerGame ?? 0m),
            IndexShotsPerGoal = (float)(athleteRound.Context?.IndexShotsPerGoal ?? 0m),
            IsHomeMatch = context.IsHomeMatch ? 1f : 0f,
            ClubTablePosition = context.ClubTablePosition,
            OpponentClubTablePosition = context.OpponentClubTablePosition,
            TablePositionDelta = context.OpponentClubTablePosition - context.ClubTablePosition,
            ClubRecentFormPoints = context.ClubRecentFormPoints,
            OpponentRecentFormPoints = context.OpponentRecentFormPoints,
            RecentFormPointsDelta = context.ClubRecentFormPoints - context.OpponentRecentFormPoints,
            ScoreLast1 = (float)context.AthleteHistory.ScoreLast1,
            ScoreAverageLast3 = (float)context.AthleteHistory.ScoreAverageLast3,
            ScoreAverageLast5 = (float)context.AthleteHistory.ScoreAverageLast5,
            ScoreEwmaLast3 = (float)context.AthleteHistory.ScoreEwmaLast3,
            ScoreEwmaLast5 = (float)context.AthleteHistory.ScoreEwmaLast5,
            ScoreStdLast5 = (float)context.AthleteHistory.ScoreStdLast5,
            ScoreFloorLast5 = (float)context.AthleteHistory.ScoreFloorLast5,
            ScoreCeilingLast5 = (float)context.AthleteHistory.ScoreCeilingLast5,
            ScoreHitRateOver3Last5 = (float)context.AthleteHistory.ScoreHitRateOver3Last5,
            ScoreHitRateOver5Last5 = (float)context.AthleteHistory.ScoreHitRateOver5Last5,
            ScoreHitRateOver8Last5 = (float)context.AthleteHistory.ScoreHitRateOver8Last5,
            ScoreNegativeRateLast5 = (float)context.AthleteHistory.ScoreNegativeRateLast5,
            ScoreTrendLast3VsPrev3 = (float)context.AthleteHistory.ScoreTrendLast3VsPrev3,
            HomeScoreAverageLast5 = (float)context.AthleteHistory.HomeScoreAverageLast5,
            AwayScoreAverageLast5 = (float)context.AthleteHistory.AwayScoreAverageLast5,
            VenueScoreAverageLast5 = (float)context.AthleteHistory.VenueScoreAverageLast5,
            HomeScoreEwmaLast3 = (float)context.AthleteHistory.HomeScoreEwmaLast3,
            AwayScoreEwmaLast3 = (float)context.AthleteHistory.AwayScoreEwmaLast3,
            VenueScoreEwmaLast3 = (float)context.AthleteHistory.VenueScoreEwmaLast3,
            ScoutOffensiveSumLast3 = (float)context.AthleteHistory.ScoutOffensiveSumLast3,
            ScoutOffensiveSumLast5 = (float)context.AthleteHistory.ScoutOffensiveSumLast5,
            ScoutOffensiveAverageLast3 = (float)context.AthleteHistory.ScoutOffensiveAverageLast3,
            ScoutOffensiveAverageLast5 = (float)context.AthleteHistory.ScoutOffensiveAverageLast5,
            ScoutOffensiveEwmaLast3 = (float)context.AthleteHistory.ScoutOffensiveEwmaLast3,
            ScoutOffensiveEwmaLast5 = (float)context.AthleteHistory.ScoutOffensiveEwmaLast5,
            ScoutDefensiveSumLast3 = (float)context.AthleteHistory.ScoutDefensiveSumLast3,
            ScoutDefensiveSumLast5 = (float)context.AthleteHistory.ScoutDefensiveSumLast5,
            ScoutDefensiveAverageLast3 = (float)context.AthleteHistory.ScoutDefensiveAverageLast3,
            ScoutDefensiveAverageLast5 = (float)context.AthleteHistory.ScoutDefensiveAverageLast5,
            ScoutDefensiveEwmaLast3 = (float)context.AthleteHistory.ScoutDefensiveEwmaLast3,
            ScoutDefensiveEwmaLast5 = (float)context.AthleteHistory.ScoutDefensiveEwmaLast5,
            ScoutNegativeSumLast3 = (float)context.AthleteHistory.ScoutNegativeSumLast3,
            ScoutNegativeSumLast5 = (float)context.AthleteHistory.ScoutNegativeSumLast5,
            ScoutNegativeAverageLast3 = (float)context.AthleteHistory.ScoutNegativeAverageLast3,
            ScoutNegativeAverageLast5 = (float)context.AthleteHistory.ScoutNegativeAverageLast5,
            ScoutNegativeEwmaLast3 = (float)context.AthleteHistory.ScoutNegativeEwmaLast3,
            ScoutNegativeEwmaLast5 = (float)context.AthleteHistory.ScoutNegativeEwmaLast5,
            ScoutBalanceSumLast3 = (float)context.AthleteHistory.ScoutBalanceSumLast3,
            ScoutBalanceSumLast5 = (float)context.AthleteHistory.ScoutBalanceSumLast5,
            ScoutBalanceAverageLast3 = (float)context.AthleteHistory.ScoutBalanceAverageLast3,
            ScoutBalanceAverageLast5 = (float)context.AthleteHistory.ScoutBalanceAverageLast5,
            ScoutBalanceEwmaLast3 = (float)context.AthleteHistory.ScoutBalanceEwmaLast3,
            ScoutBalanceEwmaLast5 = (float)context.AthleteHistory.ScoutBalanceEwmaLast5,
            ScoutGoalsAverageLast3 = (float)context.AthleteHistory.ScoutGoalsAverageLast3,
            ScoutGoalsAverageLast5 = (float)context.AthleteHistory.ScoutGoalsAverageLast5,
            ScoutAssistsAverageLast3 = (float)context.AthleteHistory.ScoutAssistsAverageLast3,
            ScoutAssistsAverageLast5 = (float)context.AthleteHistory.ScoutAssistsAverageLast5,
            ScoutTacklesAverageLast3 = (float)context.AthleteHistory.ScoutTacklesAverageLast3,
            ScoutTacklesAverageLast5 = (float)context.AthleteHistory.ScoutTacklesAverageLast5,
            ScoutCleanSheetRateLast3 = (float)context.AthleteHistory.ScoutCleanSheetRateLast3,
            ScoutCleanSheetRateLast5 = (float)context.AthleteHistory.ScoutCleanSheetRateLast5,
            PlayRateLast5 = (float)context.AthleteHistory.PlayRateLast5,
            RoundsSinceLastInGame = (float)context.AthleteHistory.RoundsSinceLastInGame,
            StatusStabilityLast5 = (float)context.AthleteHistory.StatusStabilityLast5,
            StatusChangeFlag = (float)context.AthleteHistory.StatusChangeFlag,
            EmpiricalPlayRateForCurrentStatus = (float)context.AthleteHistory.EmpiricalPlayRateForCurrentStatus,
            PriceTrendLast3 = (float)context.AthleteHistory.PriceTrendLast3,
            PriceVolatilityLast5 = (float)context.AthleteHistory.PriceVolatilityLast5,
            HistoricalPointsPerCartoletaLast3 = (float)context.AthleteHistory.HistoricalPointsPerCartoletaLast3,
            HistoricalPointsPerCartoletaLast5 = (float)context.AthleteHistory.HistoricalPointsPerCartoletaLast5,
            PositionRelativePriceSignal = (float)context.AthleteHistory.PositionRelativePriceSignal,
            PriceDeltaVsHistoricalAverage = (float)context.AthleteHistory.PriceDeltaVsHistoricalAverage,
            ClubMatchesSampleSize = context.ClubLocal.MatchesSampleSize,
            ClubPointsPerMatchLast3 = (float)context.ClubLocal.PointsPerMatchLast3,
            ClubPointsPerMatchLast5 = (float)context.ClubLocal.PointsPerMatchLast5,
            ClubVenuePointsPerMatchLast3 = (float)context.ClubLocal.VenuePointsPerMatchLast3,
            ClubVenuePointsPerMatchLast5 = (float)context.ClubLocal.VenuePointsPerMatchLast5,
            ClubGoalsForPerMatchLast3 = (float)context.ClubLocal.GoalsForPerMatchLast3,
            ClubGoalsForPerMatchLast5 = (float)context.ClubLocal.GoalsForPerMatchLast5,
            ClubGoalsAgainstPerMatchLast3 = (float)context.ClubLocal.GoalsAgainstPerMatchLast3,
            ClubGoalsAgainstPerMatchLast5 = (float)context.ClubLocal.GoalsAgainstPerMatchLast5,
            ClubGoalDifferencePerMatchLast3 = (float)context.ClubLocal.GoalDifferencePerMatchLast3,
            ClubGoalDifferencePerMatchLast5 = (float)context.ClubLocal.GoalDifferencePerMatchLast5,
            ClubVenueGoalsForPerMatchLast3 = (float)context.ClubLocal.VenueGoalsForPerMatchLast3,
            ClubVenueGoalsForPerMatchLast5 = (float)context.ClubLocal.VenueGoalsForPerMatchLast5,
            ClubVenueGoalsAgainstPerMatchLast3 = (float)context.ClubLocal.VenueGoalsAgainstPerMatchLast3,
            ClubVenueGoalsAgainstPerMatchLast5 = (float)context.ClubLocal.VenueGoalsAgainstPerMatchLast5,
            ClubCleanSheetRateLast3 = (float)context.ClubLocal.CleanSheetRateLast3,
            ClubCleanSheetRateLast5 = (float)context.ClubLocal.CleanSheetRateLast5,
            ClubFailedToScoreRateLast3 = (float)context.ClubLocal.FailedToScoreRateLast3,
            ClubFailedToScoreRateLast5 = (float)context.ClubLocal.FailedToScoreRateLast5,
            ClubFantasyPointsForAverageLast3 = (float)context.ClubLocal.FantasyPointsForAverageLast3,
            ClubFantasyPointsForAverageLast5 = (float)context.ClubLocal.FantasyPointsForAverageLast5,
            ClubFantasyPointsAgainstAverageLast3 = (float)context.ClubLocal.FantasyPointsAgainstAverageLast3,
            ClubFantasyPointsAgainstAverageLast5 = (float)context.ClubLocal.FantasyPointsAgainstAverageLast5,
            ClubScoutOffensiveConcededAverageLast3 = (float)context.ClubLocal.ScoutOffensiveConcededAverageLast3,
            ClubScoutOffensiveConcededAverageLast5 = (float)context.ClubLocal.ScoutOffensiveConcededAverageLast5,
            ClubScoutDefensiveConcededAverageLast3 = (float)context.ClubLocal.ScoutDefensiveConcededAverageLast3,
            ClubScoutDefensiveConcededAverageLast5 = (float)context.ClubLocal.ScoutDefensiveConcededAverageLast5,
            ClubScoutNegativeConcededAverageLast3 = (float)context.ClubLocal.ScoutNegativeConcededAverageLast3,
            ClubScoutNegativeConcededAverageLast5 = (float)context.ClubLocal.ScoutNegativeConcededAverageLast5,
            ClubScoutBalanceConcededAverageLast3 = (float)context.ClubLocal.ScoutBalanceConcededAverageLast3,
            ClubScoutBalanceConcededAverageLast5 = (float)context.ClubLocal.ScoutBalanceConcededAverageLast5,
            ClubScoutGoalsConcededAverageLast3 = (float)context.ClubLocal.ScoutGoalsConcededAverageLast3,
            ClubScoutGoalsConcededAverageLast5 = (float)context.ClubLocal.ScoutGoalsConcededAverageLast5,
            ClubScoutAssistsConcededAverageLast3 = (float)context.ClubLocal.ScoutAssistsConcededAverageLast3,
            ClubScoutAssistsConcededAverageLast5 = (float)context.ClubLocal.ScoutAssistsConcededAverageLast5,
            ClubScoutTacklesConcededAverageLast3 = (float)context.ClubLocal.ScoutTacklesConcededAverageLast3,
            ClubScoutTacklesConcededAverageLast5 = (float)context.ClubLocal.ScoutTacklesConcededAverageLast5,
            ClubPositionFantasyPointsForAverageLast3 = (float)context.ClubLocal.PositionFantasyPointsForAverageLast3,
            ClubPositionFantasyPointsForAverageLast5 = (float)context.ClubLocal.PositionFantasyPointsForAverageLast5,
            ClubPositionFantasyPointsAgainstAverageLast3 = (float)context.ClubLocal.PositionFantasyPointsAgainstAverageLast3,
            ClubPositionFantasyPointsAgainstAverageLast5 = (float)context.ClubLocal.PositionFantasyPointsAgainstAverageLast5,
            ClubPositionScoutOffensiveConcededAverageLast3 = (float)context.ClubLocal.PositionScoutOffensiveConcededAverageLast3,
            ClubPositionScoutOffensiveConcededAverageLast5 = (float)context.ClubLocal.PositionScoutOffensiveConcededAverageLast5,
            ClubPositionScoutDefensiveConcededAverageLast3 = (float)context.ClubLocal.PositionScoutDefensiveConcededAverageLast3,
            ClubPositionScoutDefensiveConcededAverageLast5 = (float)context.ClubLocal.PositionScoutDefensiveConcededAverageLast5,
            ClubPositionScoutNegativeConcededAverageLast3 = (float)context.ClubLocal.PositionScoutNegativeConcededAverageLast3,
            ClubPositionScoutNegativeConcededAverageLast5 = (float)context.ClubLocal.PositionScoutNegativeConcededAverageLast5,
            ClubPositionScoutBalanceConcededAverageLast3 = (float)context.ClubLocal.PositionScoutBalanceConcededAverageLast3,
            ClubPositionScoutBalanceConcededAverageLast5 = (float)context.ClubLocal.PositionScoutBalanceConcededAverageLast5,
            ClubPositionScoutGoalsConcededAverageLast3 = (float)context.ClubLocal.PositionScoutGoalsConcededAverageLast3,
            ClubPositionScoutGoalsConcededAverageLast5 = (float)context.ClubLocal.PositionScoutGoalsConcededAverageLast5,
            ClubPositionScoutAssistsConcededAverageLast3 = (float)context.ClubLocal.PositionScoutAssistsConcededAverageLast3,
            ClubPositionScoutAssistsConcededAverageLast5 = (float)context.ClubLocal.PositionScoutAssistsConcededAverageLast5,
            ClubPositionScoutTacklesConcededAverageLast3 = (float)context.ClubLocal.PositionScoutTacklesConcededAverageLast3,
            ClubPositionScoutTacklesConcededAverageLast5 = (float)context.ClubLocal.PositionScoutTacklesConcededAverageLast5,
            ClubExpectedPointsLast3 = (float)context.ClubLocal.ExpectedPointsLast3,
            ClubWinRateLast5 = (float)context.ClubLocal.WinRateLast5,
            ClubDrawRateLast5 = (float)context.ClubLocal.DrawRateLast5,
            ClubLossRateLast5 = (float)context.ClubLocal.LossRateLast5,
            ClubExpectedPointsLast5 = (float)context.ClubLocal.ExpectedPointsLast5,
            ClubAttackStrengthLast3 = (float)context.ClubLocal.AttackStrengthLast3,
            ClubAttackStrengthLast5 = (float)context.ClubLocal.AttackStrengthLast5,
            ClubDefenseStrengthLast3 = (float)context.ClubLocal.DefenseStrengthLast3,
            ClubDefenseStrengthLast5 = (float)context.ClubLocal.DefenseStrengthLast5,
            OpponentMatchesSampleSize = context.OpponentLocal.MatchesSampleSize,
            OpponentPointsPerMatchLast3 = (float)context.OpponentLocal.PointsPerMatchLast3,
            OpponentPointsPerMatchLast5 = (float)context.OpponentLocal.PointsPerMatchLast5,
            OpponentVenuePointsPerMatchLast3 = (float)context.OpponentLocal.VenuePointsPerMatchLast3,
            OpponentVenuePointsPerMatchLast5 = (float)context.OpponentLocal.VenuePointsPerMatchLast5,
            OpponentGoalsForPerMatchLast3 = (float)context.OpponentLocal.GoalsForPerMatchLast3,
            OpponentGoalsForPerMatchLast5 = (float)context.OpponentLocal.GoalsForPerMatchLast5,
            OpponentGoalsAgainstPerMatchLast3 = (float)context.OpponentLocal.GoalsAgainstPerMatchLast3,
            OpponentGoalsAgainstPerMatchLast5 = (float)context.OpponentLocal.GoalsAgainstPerMatchLast5,
            OpponentGoalDifferencePerMatchLast3 = (float)context.OpponentLocal.GoalDifferencePerMatchLast3,
            OpponentGoalDifferencePerMatchLast5 = (float)context.OpponentLocal.GoalDifferencePerMatchLast5,
            OpponentVenueGoalsForPerMatchLast3 = (float)context.OpponentLocal.VenueGoalsForPerMatchLast3,
            OpponentVenueGoalsForPerMatchLast5 = (float)context.OpponentLocal.VenueGoalsForPerMatchLast5,
            OpponentVenueGoalsAgainstPerMatchLast3 = (float)context.OpponentLocal.VenueGoalsAgainstPerMatchLast3,
            OpponentVenueGoalsAgainstPerMatchLast5 = (float)context.OpponentLocal.VenueGoalsAgainstPerMatchLast5,
            OpponentCleanSheetRateLast3 = (float)context.OpponentLocal.CleanSheetRateLast3,
            OpponentCleanSheetRateLast5 = (float)context.OpponentLocal.CleanSheetRateLast5,
            OpponentFailedToScoreRateLast3 = (float)context.OpponentLocal.FailedToScoreRateLast3,
            OpponentFailedToScoreRateLast5 = (float)context.OpponentLocal.FailedToScoreRateLast5,
            OpponentFantasyPointsForAverageLast3 = (float)context.OpponentLocal.FantasyPointsForAverageLast3,
            OpponentFantasyPointsForAverageLast5 = (float)context.OpponentLocal.FantasyPointsForAverageLast5,
            OpponentFantasyPointsAgainstAverageLast3 = (float)context.OpponentLocal.FantasyPointsAgainstAverageLast3,
            OpponentFantasyPointsAgainstAverageLast5 = (float)context.OpponentLocal.FantasyPointsAgainstAverageLast5,
            OpponentScoutOffensiveConcededAverageLast3 = (float)context.OpponentLocal.ScoutOffensiveConcededAverageLast3,
            OpponentScoutOffensiveConcededAverageLast5 = (float)context.OpponentLocal.ScoutOffensiveConcededAverageLast5,
            OpponentScoutDefensiveConcededAverageLast3 = (float)context.OpponentLocal.ScoutDefensiveConcededAverageLast3,
            OpponentScoutDefensiveConcededAverageLast5 = (float)context.OpponentLocal.ScoutDefensiveConcededAverageLast5,
            OpponentScoutNegativeConcededAverageLast3 = (float)context.OpponentLocal.ScoutNegativeConcededAverageLast3,
            OpponentScoutNegativeConcededAverageLast5 = (float)context.OpponentLocal.ScoutNegativeConcededAverageLast5,
            OpponentScoutBalanceConcededAverageLast3 = (float)context.OpponentLocal.ScoutBalanceConcededAverageLast3,
            OpponentScoutBalanceConcededAverageLast5 = (float)context.OpponentLocal.ScoutBalanceConcededAverageLast5,
            OpponentScoutGoalsConcededAverageLast3 = (float)context.OpponentLocal.ScoutGoalsConcededAverageLast3,
            OpponentScoutGoalsConcededAverageLast5 = (float)context.OpponentLocal.ScoutGoalsConcededAverageLast5,
            OpponentScoutAssistsConcededAverageLast3 = (float)context.OpponentLocal.ScoutAssistsConcededAverageLast3,
            OpponentScoutAssistsConcededAverageLast5 = (float)context.OpponentLocal.ScoutAssistsConcededAverageLast5,
            OpponentScoutTacklesConcededAverageLast3 = (float)context.OpponentLocal.ScoutTacklesConcededAverageLast3,
            OpponentScoutTacklesConcededAverageLast5 = (float)context.OpponentLocal.ScoutTacklesConcededAverageLast5,
            OpponentPositionFantasyPointsForAverageLast3 = (float)context.OpponentLocal.PositionFantasyPointsForAverageLast3,
            OpponentPositionFantasyPointsForAverageLast5 = (float)context.OpponentLocal.PositionFantasyPointsForAverageLast5,
            OpponentPositionFantasyPointsAgainstAverageLast3 = (float)context.OpponentLocal.PositionFantasyPointsAgainstAverageLast3,
            OpponentPositionFantasyPointsAgainstAverageLast5 = (float)context.OpponentLocal.PositionFantasyPointsAgainstAverageLast5,
            OpponentPositionScoutOffensiveConcededAverageLast3 = (float)context.OpponentLocal.PositionScoutOffensiveConcededAverageLast3,
            OpponentPositionScoutOffensiveConcededAverageLast5 = (float)context.OpponentLocal.PositionScoutOffensiveConcededAverageLast5,
            OpponentPositionScoutDefensiveConcededAverageLast3 = (float)context.OpponentLocal.PositionScoutDefensiveConcededAverageLast3,
            OpponentPositionScoutDefensiveConcededAverageLast5 = (float)context.OpponentLocal.PositionScoutDefensiveConcededAverageLast5,
            OpponentPositionScoutNegativeConcededAverageLast3 = (float)context.OpponentLocal.PositionScoutNegativeConcededAverageLast3,
            OpponentPositionScoutNegativeConcededAverageLast5 = (float)context.OpponentLocal.PositionScoutNegativeConcededAverageLast5,
            OpponentPositionScoutBalanceConcededAverageLast3 = (float)context.OpponentLocal.PositionScoutBalanceConcededAverageLast3,
            OpponentPositionScoutBalanceConcededAverageLast5 = (float)context.OpponentLocal.PositionScoutBalanceConcededAverageLast5,
            OpponentPositionScoutGoalsConcededAverageLast3 = (float)context.OpponentLocal.PositionScoutGoalsConcededAverageLast3,
            OpponentPositionScoutGoalsConcededAverageLast5 = (float)context.OpponentLocal.PositionScoutGoalsConcededAverageLast5,
            OpponentPositionScoutAssistsConcededAverageLast3 = (float)context.OpponentLocal.PositionScoutAssistsConcededAverageLast3,
            OpponentPositionScoutAssistsConcededAverageLast5 = (float)context.OpponentLocal.PositionScoutAssistsConcededAverageLast5,
            OpponentPositionScoutTacklesConcededAverageLast3 = (float)context.OpponentLocal.PositionScoutTacklesConcededAverageLast3,
            OpponentPositionScoutTacklesConcededAverageLast5 = (float)context.OpponentLocal.PositionScoutTacklesConcededAverageLast5,
            OpponentExpectedPointsLast3 = (float)context.OpponentLocal.ExpectedPointsLast3,
            OpponentWinRateLast5 = (float)context.OpponentLocal.WinRateLast5,
            OpponentDrawRateLast5 = (float)context.OpponentLocal.DrawRateLast5,
            OpponentLossRateLast5 = (float)context.OpponentLocal.LossRateLast5,
            OpponentExpectedPointsLast5 = (float)context.OpponentLocal.ExpectedPointsLast5,
            OpponentAttackStrengthLast3 = (float)context.OpponentLocal.AttackStrengthLast3,
            OpponentAttackStrengthLast5 = (float)context.OpponentLocal.AttackStrengthLast5,
            OpponentDefenseStrengthLast3 = (float)context.OpponentLocal.DefenseStrengthLast3,
            OpponentDefenseStrengthLast5 = (float)context.OpponentLocal.DefenseStrengthLast5,
            AttackVsOpponentDefenseDeltaLast3 = (float)(context.ClubLocal.AttackStrengthLast3 - context.OpponentLocal.DefenseStrengthLast3),
            AttackVsOpponentDefenseDeltaLast5 = (float)(context.ClubLocal.AttackStrengthLast5 - context.OpponentLocal.DefenseStrengthLast5),
            PositionForVsOpponentConcededDeltaLast3 = (float)(context.ClubLocal.PositionFantasyPointsForAverageLast3 - context.OpponentLocal.PositionFantasyPointsAgainstAverageLast3),
            PositionForVsOpponentConcededDeltaLast5 = (float)(context.ClubLocal.PositionFantasyPointsForAverageLast5 - context.OpponentLocal.PositionFantasyPointsAgainstAverageLast5),
            GoalDifferenceDeltaLast3 = (float)(context.ClubLocal.GoalDifferencePerMatchLast3 - context.OpponentLocal.GoalDifferencePerMatchLast3),
            GoalDifferenceDeltaLast5 = (float)(context.ClubLocal.GoalDifferencePerMatchLast5 - context.OpponentLocal.GoalDifferencePerMatchLast5),
            Label = (float)athleteRound.ScoreNum,
        };

        return true;
    }

    internal static bool TryBuildPredictionCandidate(
        int round,
        AthleteRound baseline,
        FullContextFeatureLookup lookup,
        out FullContextPredictionCandidate candidate)
    {
        candidate = null!;

        if (!TryResolveContext(round, baseline, lookup, out var context))
        {
            return false;
        }

        candidate = new FullContextPredictionCandidate
        {
            Baseline = baseline,
            Input = CreatePredictionInput(round, baseline, context),
            IsHomeMatch = context.IsHomeMatch,
            ClubTablePosition = context.ClubTablePosition,
            OpponentClubCartolaId = context.OpponentClubCartolaId,
            OpponentClubName = context.OpponentClubName,
            OpponentClubTablePosition = context.OpponentClubTablePosition,
            ClubWinProbability = context.ClubLocal.WinRateLast5,
            ClubExpectedPoints = context.ClubLocal.ExpectedPointsLast5,
            OpponentWinProbability = context.OpponentLocal.WinRateLast5,
            OpponentExpectedPoints = context.OpponentLocal.ExpectedPointsLast5,
        };

        return true;
    }

    internal static bool TryBuildDatasetRow(
        AthleteRound athleteRound,
        FullContextFeatureLookup lookup,
        out TrainingFullContextDatasetRow row)
    {
        row = null!;

        if (!TryResolveContext(athleteRound.RoundId, athleteRound, lookup, out var context))
        {
            return false;
        }

        row = new TrainingFullContextDatasetRow
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
            GamesAtVenueNum = context.IsHomeMatch ? athleteRound.HomeGamesNum : athleteRound.AwayGamesNum,
            MovingAverage = athleteRound.MovingAverage,
            AverageScore = athleteRound.AverageScoreNum,
            HomeAverageScore = athleteRound.HomeAverageScore,
            AwayAverageScore = athleteRound.AwayAverageScore,
            ContextualAverageScore = context.IsHomeMatch ? athleteRound.HomeAverageScore : athleteRound.AwayAverageScore,
            PriceNum = athleteRound.PriceNum,
            VariationNum = athleteRound.VariationNum,
            HasAthleteContext = athleteRound.Context is not null,
            TeamAverageScore = athleteRound.Context?.TeamAverageScore ?? 0m,
            TeamHomeAverageCurrent = athleteRound.Context?.TeamHomeAverageCurrent ?? 0m,
            TeamHomeAveragePrevious = athleteRound.Context?.TeamHomeAveragePrevious ?? 0m,
            TeamAwayAverageCurrent = athleteRound.Context?.TeamAwayAverageCurrent ?? 0m,
            TeamAwayAveragePrevious = athleteRound.Context?.TeamAwayAveragePrevious ?? 0m,
            TeamVenueAverageCurrent = context.IsHomeMatch ? athleteRound.Context?.TeamHomeAverageCurrent ?? 0m : athleteRound.Context?.TeamAwayAverageCurrent ?? 0m,
            TeamVenueAveragePrevious = context.IsHomeMatch ? athleteRound.Context?.TeamHomeAveragePrevious ?? 0m : athleteRound.Context?.TeamAwayAveragePrevious ?? 0m,
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
            IsHomeMatch = context.IsHomeMatch,
            ClubTablePosition = context.ClubTablePosition,
            OpponentClubCartolaId = context.OpponentClubCartolaId,
            OpponentClubName = context.OpponentClubName,
            OpponentClubTablePosition = context.OpponentClubTablePosition,
            TablePositionDelta = context.OpponentClubTablePosition - context.ClubTablePosition,
            ClubRecentForm = context.ClubRecentForm,
            OpponentRecentForm = context.OpponentRecentForm,
            ClubRecentFormPoints = (decimal)context.ClubRecentFormPoints,
            OpponentRecentFormPoints = (decimal)context.OpponentRecentFormPoints,
            RecentFormPointsDelta = (decimal)(context.ClubRecentFormPoints - context.OpponentRecentFormPoints),
            ScoreLast1 = context.AthleteHistory.ScoreLast1,
            ScoreAverageLast3 = context.AthleteHistory.ScoreAverageLast3,
            ScoreAverageLast5 = context.AthleteHistory.ScoreAverageLast5,
            ScoreEwmaLast3 = context.AthleteHistory.ScoreEwmaLast3,
            ScoreEwmaLast5 = context.AthleteHistory.ScoreEwmaLast5,
            ScoreStdLast5 = context.AthleteHistory.ScoreStdLast5,
            ScoreFloorLast5 = context.AthleteHistory.ScoreFloorLast5,
            ScoreCeilingLast5 = context.AthleteHistory.ScoreCeilingLast5,
            ScoreHitRateOver3Last5 = context.AthleteHistory.ScoreHitRateOver3Last5,
            ScoreHitRateOver5Last5 = context.AthleteHistory.ScoreHitRateOver5Last5,
            ScoreHitRateOver8Last5 = context.AthleteHistory.ScoreHitRateOver8Last5,
            ScoreNegativeRateLast5 = context.AthleteHistory.ScoreNegativeRateLast5,
            ScoreTrendLast3VsPrev3 = context.AthleteHistory.ScoreTrendLast3VsPrev3,
            HomeScoreAverageLast5 = context.AthleteHistory.HomeScoreAverageLast5,
            AwayScoreAverageLast5 = context.AthleteHistory.AwayScoreAverageLast5,
            VenueScoreAverageLast5 = context.AthleteHistory.VenueScoreAverageLast5,
            HomeScoreEwmaLast3 = context.AthleteHistory.HomeScoreEwmaLast3,
            AwayScoreEwmaLast3 = context.AthleteHistory.AwayScoreEwmaLast3,
            VenueScoreEwmaLast3 = context.AthleteHistory.VenueScoreEwmaLast3,
            ScoutOffensiveSumLast3 = context.AthleteHistory.ScoutOffensiveSumLast3,
            ScoutOffensiveSumLast5 = context.AthleteHistory.ScoutOffensiveSumLast5,
            ScoutOffensiveAverageLast3 = context.AthleteHistory.ScoutOffensiveAverageLast3,
            ScoutOffensiveAverageLast5 = context.AthleteHistory.ScoutOffensiveAverageLast5,
            ScoutOffensiveEwmaLast3 = context.AthleteHistory.ScoutOffensiveEwmaLast3,
            ScoutOffensiveEwmaLast5 = context.AthleteHistory.ScoutOffensiveEwmaLast5,
            ScoutDefensiveSumLast3 = context.AthleteHistory.ScoutDefensiveSumLast3,
            ScoutDefensiveSumLast5 = context.AthleteHistory.ScoutDefensiveSumLast5,
            ScoutDefensiveAverageLast3 = context.AthleteHistory.ScoutDefensiveAverageLast3,
            ScoutDefensiveAverageLast5 = context.AthleteHistory.ScoutDefensiveAverageLast5,
            ScoutDefensiveEwmaLast3 = context.AthleteHistory.ScoutDefensiveEwmaLast3,
            ScoutDefensiveEwmaLast5 = context.AthleteHistory.ScoutDefensiveEwmaLast5,
            ScoutNegativeSumLast3 = context.AthleteHistory.ScoutNegativeSumLast3,
            ScoutNegativeSumLast5 = context.AthleteHistory.ScoutNegativeSumLast5,
            ScoutNegativeAverageLast3 = context.AthleteHistory.ScoutNegativeAverageLast3,
            ScoutNegativeAverageLast5 = context.AthleteHistory.ScoutNegativeAverageLast5,
            ScoutNegativeEwmaLast3 = context.AthleteHistory.ScoutNegativeEwmaLast3,
            ScoutNegativeEwmaLast5 = context.AthleteHistory.ScoutNegativeEwmaLast5,
            ScoutBalanceSumLast3 = context.AthleteHistory.ScoutBalanceSumLast3,
            ScoutBalanceSumLast5 = context.AthleteHistory.ScoutBalanceSumLast5,
            ScoutBalanceAverageLast3 = context.AthleteHistory.ScoutBalanceAverageLast3,
            ScoutBalanceAverageLast5 = context.AthleteHistory.ScoutBalanceAverageLast5,
            ScoutBalanceEwmaLast3 = context.AthleteHistory.ScoutBalanceEwmaLast3,
            ScoutBalanceEwmaLast5 = context.AthleteHistory.ScoutBalanceEwmaLast5,
            ScoutGoalsAverageLast3 = context.AthleteHistory.ScoutGoalsAverageLast3,
            ScoutGoalsAverageLast5 = context.AthleteHistory.ScoutGoalsAverageLast5,
            ScoutAssistsAverageLast3 = context.AthleteHistory.ScoutAssistsAverageLast3,
            ScoutAssistsAverageLast5 = context.AthleteHistory.ScoutAssistsAverageLast5,
            ScoutTacklesAverageLast3 = context.AthleteHistory.ScoutTacklesAverageLast3,
            ScoutTacklesAverageLast5 = context.AthleteHistory.ScoutTacklesAverageLast5,
            ScoutCleanSheetRateLast3 = context.AthleteHistory.ScoutCleanSheetRateLast3,
            ScoutCleanSheetRateLast5 = context.AthleteHistory.ScoutCleanSheetRateLast5,
            PlayRateLast5 = context.AthleteHistory.PlayRateLast5,
            RoundsSinceLastInGame = context.AthleteHistory.RoundsSinceLastInGame,
            StatusStabilityLast5 = context.AthleteHistory.StatusStabilityLast5,
            StatusChangeFlag = context.AthleteHistory.StatusChangeFlag,
            EmpiricalPlayRateForCurrentStatus = context.AthleteHistory.EmpiricalPlayRateForCurrentStatus,
            PriceTrendLast3 = context.AthleteHistory.PriceTrendLast3,
            PriceVolatilityLast5 = context.AthleteHistory.PriceVolatilityLast5,
            HistoricalPointsPerCartoletaLast3 = context.AthleteHistory.HistoricalPointsPerCartoletaLast3,
            HistoricalPointsPerCartoletaLast5 = context.AthleteHistory.HistoricalPointsPerCartoletaLast5,
            PositionRelativePriceSignal = context.AthleteHistory.PositionRelativePriceSignal,
            PriceDeltaVsHistoricalAverage = context.AthleteHistory.PriceDeltaVsHistoricalAverage,
            ClubMatchesSampleSize = context.ClubLocal.MatchesSampleSize,
            ClubPointsPerMatchLast3 = context.ClubLocal.PointsPerMatchLast3,
            ClubPointsPerMatchLast5 = context.ClubLocal.PointsPerMatchLast5,
            ClubVenuePointsPerMatchLast3 = context.ClubLocal.VenuePointsPerMatchLast3,
            ClubVenuePointsPerMatchLast5 = context.ClubLocal.VenuePointsPerMatchLast5,
            ClubGoalsForPerMatchLast3 = context.ClubLocal.GoalsForPerMatchLast3,
            ClubGoalsForPerMatchLast5 = context.ClubLocal.GoalsForPerMatchLast5,
            ClubGoalsAgainstPerMatchLast3 = context.ClubLocal.GoalsAgainstPerMatchLast3,
            ClubGoalsAgainstPerMatchLast5 = context.ClubLocal.GoalsAgainstPerMatchLast5,
            ClubGoalDifferencePerMatchLast3 = context.ClubLocal.GoalDifferencePerMatchLast3,
            ClubGoalDifferencePerMatchLast5 = context.ClubLocal.GoalDifferencePerMatchLast5,
            ClubVenueGoalsForPerMatchLast3 = context.ClubLocal.VenueGoalsForPerMatchLast3,
            ClubVenueGoalsForPerMatchLast5 = context.ClubLocal.VenueGoalsForPerMatchLast5,
            ClubVenueGoalsAgainstPerMatchLast3 = context.ClubLocal.VenueGoalsAgainstPerMatchLast3,
            ClubVenueGoalsAgainstPerMatchLast5 = context.ClubLocal.VenueGoalsAgainstPerMatchLast5,
            ClubCleanSheetRateLast3 = context.ClubLocal.CleanSheetRateLast3,
            ClubCleanSheetRateLast5 = context.ClubLocal.CleanSheetRateLast5,
            ClubFailedToScoreRateLast3 = context.ClubLocal.FailedToScoreRateLast3,
            ClubFailedToScoreRateLast5 = context.ClubLocal.FailedToScoreRateLast5,
            ClubFantasyPointsForAverageLast3 = context.ClubLocal.FantasyPointsForAverageLast3,
            ClubFantasyPointsForAverageLast5 = context.ClubLocal.FantasyPointsForAverageLast5,
            ClubFantasyPointsAgainstAverageLast3 = context.ClubLocal.FantasyPointsAgainstAverageLast3,
            ClubFantasyPointsAgainstAverageLast5 = context.ClubLocal.FantasyPointsAgainstAverageLast5,
            ClubScoutOffensiveConcededAverageLast3 = context.ClubLocal.ScoutOffensiveConcededAverageLast3,
            ClubScoutOffensiveConcededAverageLast5 = context.ClubLocal.ScoutOffensiveConcededAverageLast5,
            ClubScoutDefensiveConcededAverageLast3 = context.ClubLocal.ScoutDefensiveConcededAverageLast3,
            ClubScoutDefensiveConcededAverageLast5 = context.ClubLocal.ScoutDefensiveConcededAverageLast5,
            ClubScoutNegativeConcededAverageLast3 = context.ClubLocal.ScoutNegativeConcededAverageLast3,
            ClubScoutNegativeConcededAverageLast5 = context.ClubLocal.ScoutNegativeConcededAverageLast5,
            ClubScoutBalanceConcededAverageLast3 = context.ClubLocal.ScoutBalanceConcededAverageLast3,
            ClubScoutBalanceConcededAverageLast5 = context.ClubLocal.ScoutBalanceConcededAverageLast5,
            ClubScoutGoalsConcededAverageLast3 = context.ClubLocal.ScoutGoalsConcededAverageLast3,
            ClubScoutGoalsConcededAverageLast5 = context.ClubLocal.ScoutGoalsConcededAverageLast5,
            ClubScoutAssistsConcededAverageLast3 = context.ClubLocal.ScoutAssistsConcededAverageLast3,
            ClubScoutAssistsConcededAverageLast5 = context.ClubLocal.ScoutAssistsConcededAverageLast5,
            ClubScoutTacklesConcededAverageLast3 = context.ClubLocal.ScoutTacklesConcededAverageLast3,
            ClubScoutTacklesConcededAverageLast5 = context.ClubLocal.ScoutTacklesConcededAverageLast5,
            ClubPositionFantasyPointsForAverageLast3 = context.ClubLocal.PositionFantasyPointsForAverageLast3,
            ClubPositionFantasyPointsForAverageLast5 = context.ClubLocal.PositionFantasyPointsForAverageLast5,
            ClubPositionFantasyPointsAgainstAverageLast3 = context.ClubLocal.PositionFantasyPointsAgainstAverageLast3,
            ClubPositionFantasyPointsAgainstAverageLast5 = context.ClubLocal.PositionFantasyPointsAgainstAverageLast5,
            ClubPositionScoutOffensiveConcededAverageLast3 = context.ClubLocal.PositionScoutOffensiveConcededAverageLast3,
            ClubPositionScoutOffensiveConcededAverageLast5 = context.ClubLocal.PositionScoutOffensiveConcededAverageLast5,
            ClubPositionScoutDefensiveConcededAverageLast3 = context.ClubLocal.PositionScoutDefensiveConcededAverageLast3,
            ClubPositionScoutDefensiveConcededAverageLast5 = context.ClubLocal.PositionScoutDefensiveConcededAverageLast5,
            ClubPositionScoutNegativeConcededAverageLast3 = context.ClubLocal.PositionScoutNegativeConcededAverageLast3,
            ClubPositionScoutNegativeConcededAverageLast5 = context.ClubLocal.PositionScoutNegativeConcededAverageLast5,
            ClubPositionScoutBalanceConcededAverageLast3 = context.ClubLocal.PositionScoutBalanceConcededAverageLast3,
            ClubPositionScoutBalanceConcededAverageLast5 = context.ClubLocal.PositionScoutBalanceConcededAverageLast5,
            ClubPositionScoutGoalsConcededAverageLast3 = context.ClubLocal.PositionScoutGoalsConcededAverageLast3,
            ClubPositionScoutGoalsConcededAverageLast5 = context.ClubLocal.PositionScoutGoalsConcededAverageLast5,
            ClubPositionScoutAssistsConcededAverageLast3 = context.ClubLocal.PositionScoutAssistsConcededAverageLast3,
            ClubPositionScoutAssistsConcededAverageLast5 = context.ClubLocal.PositionScoutAssistsConcededAverageLast5,
            ClubPositionScoutTacklesConcededAverageLast3 = context.ClubLocal.PositionScoutTacklesConcededAverageLast3,
            ClubPositionScoutTacklesConcededAverageLast5 = context.ClubLocal.PositionScoutTacklesConcededAverageLast5,
            ClubExpectedPointsLast3 = context.ClubLocal.ExpectedPointsLast3,
            ClubWinRateLast5 = context.ClubLocal.WinRateLast5,
            ClubDrawRateLast5 = context.ClubLocal.DrawRateLast5,
            ClubLossRateLast5 = context.ClubLocal.LossRateLast5,
            ClubExpectedPointsLast5 = context.ClubLocal.ExpectedPointsLast5,
            ClubAttackStrengthLast3 = context.ClubLocal.AttackStrengthLast3,
            ClubAttackStrengthLast5 = context.ClubLocal.AttackStrengthLast5,
            ClubDefenseStrengthLast3 = context.ClubLocal.DefenseStrengthLast3,
            ClubDefenseStrengthLast5 = context.ClubLocal.DefenseStrengthLast5,
            OpponentMatchesSampleSize = context.OpponentLocal.MatchesSampleSize,
            OpponentPointsPerMatchLast3 = context.OpponentLocal.PointsPerMatchLast3,
            OpponentPointsPerMatchLast5 = context.OpponentLocal.PointsPerMatchLast5,
            OpponentVenuePointsPerMatchLast3 = context.OpponentLocal.VenuePointsPerMatchLast3,
            OpponentVenuePointsPerMatchLast5 = context.OpponentLocal.VenuePointsPerMatchLast5,
            OpponentGoalsForPerMatchLast3 = context.OpponentLocal.GoalsForPerMatchLast3,
            OpponentGoalsForPerMatchLast5 = context.OpponentLocal.GoalsForPerMatchLast5,
            OpponentGoalsAgainstPerMatchLast3 = context.OpponentLocal.GoalsAgainstPerMatchLast3,
            OpponentGoalsAgainstPerMatchLast5 = context.OpponentLocal.GoalsAgainstPerMatchLast5,
            OpponentGoalDifferencePerMatchLast3 = context.OpponentLocal.GoalDifferencePerMatchLast3,
            OpponentGoalDifferencePerMatchLast5 = context.OpponentLocal.GoalDifferencePerMatchLast5,
            OpponentVenueGoalsForPerMatchLast3 = context.OpponentLocal.VenueGoalsForPerMatchLast3,
            OpponentVenueGoalsForPerMatchLast5 = context.OpponentLocal.VenueGoalsForPerMatchLast5,
            OpponentVenueGoalsAgainstPerMatchLast3 = context.OpponentLocal.VenueGoalsAgainstPerMatchLast3,
            OpponentVenueGoalsAgainstPerMatchLast5 = context.OpponentLocal.VenueGoalsAgainstPerMatchLast5,
            OpponentCleanSheetRateLast3 = context.OpponentLocal.CleanSheetRateLast3,
            OpponentCleanSheetRateLast5 = context.OpponentLocal.CleanSheetRateLast5,
            OpponentFailedToScoreRateLast3 = context.OpponentLocal.FailedToScoreRateLast3,
            OpponentFailedToScoreRateLast5 = context.OpponentLocal.FailedToScoreRateLast5,
            OpponentFantasyPointsForAverageLast3 = context.OpponentLocal.FantasyPointsForAverageLast3,
            OpponentFantasyPointsForAverageLast5 = context.OpponentLocal.FantasyPointsForAverageLast5,
            OpponentFantasyPointsAgainstAverageLast3 = context.OpponentLocal.FantasyPointsAgainstAverageLast3,
            OpponentFantasyPointsAgainstAverageLast5 = context.OpponentLocal.FantasyPointsAgainstAverageLast5,
            OpponentScoutOffensiveConcededAverageLast3 = context.OpponentLocal.ScoutOffensiveConcededAverageLast3,
            OpponentScoutOffensiveConcededAverageLast5 = context.OpponentLocal.ScoutOffensiveConcededAverageLast5,
            OpponentScoutDefensiveConcededAverageLast3 = context.OpponentLocal.ScoutDefensiveConcededAverageLast3,
            OpponentScoutDefensiveConcededAverageLast5 = context.OpponentLocal.ScoutDefensiveConcededAverageLast5,
            OpponentScoutNegativeConcededAverageLast3 = context.OpponentLocal.ScoutNegativeConcededAverageLast3,
            OpponentScoutNegativeConcededAverageLast5 = context.OpponentLocal.ScoutNegativeConcededAverageLast5,
            OpponentScoutBalanceConcededAverageLast3 = context.OpponentLocal.ScoutBalanceConcededAverageLast3,
            OpponentScoutBalanceConcededAverageLast5 = context.OpponentLocal.ScoutBalanceConcededAverageLast5,
            OpponentScoutGoalsConcededAverageLast3 = context.OpponentLocal.ScoutGoalsConcededAverageLast3,
            OpponentScoutGoalsConcededAverageLast5 = context.OpponentLocal.ScoutGoalsConcededAverageLast5,
            OpponentScoutAssistsConcededAverageLast3 = context.OpponentLocal.ScoutAssistsConcededAverageLast3,
            OpponentScoutAssistsConcededAverageLast5 = context.OpponentLocal.ScoutAssistsConcededAverageLast5,
            OpponentScoutTacklesConcededAverageLast3 = context.OpponentLocal.ScoutTacklesConcededAverageLast3,
            OpponentScoutTacklesConcededAverageLast5 = context.OpponentLocal.ScoutTacklesConcededAverageLast5,
            OpponentPositionFantasyPointsForAverageLast3 = context.OpponentLocal.PositionFantasyPointsForAverageLast3,
            OpponentPositionFantasyPointsForAverageLast5 = context.OpponentLocal.PositionFantasyPointsForAverageLast5,
            OpponentPositionFantasyPointsAgainstAverageLast3 = context.OpponentLocal.PositionFantasyPointsAgainstAverageLast3,
            OpponentPositionFantasyPointsAgainstAverageLast5 = context.OpponentLocal.PositionFantasyPointsAgainstAverageLast5,
            OpponentPositionScoutOffensiveConcededAverageLast3 = context.OpponentLocal.PositionScoutOffensiveConcededAverageLast3,
            OpponentPositionScoutOffensiveConcededAverageLast5 = context.OpponentLocal.PositionScoutOffensiveConcededAverageLast5,
            OpponentPositionScoutDefensiveConcededAverageLast3 = context.OpponentLocal.PositionScoutDefensiveConcededAverageLast3,
            OpponentPositionScoutDefensiveConcededAverageLast5 = context.OpponentLocal.PositionScoutDefensiveConcededAverageLast5,
            OpponentPositionScoutNegativeConcededAverageLast3 = context.OpponentLocal.PositionScoutNegativeConcededAverageLast3,
            OpponentPositionScoutNegativeConcededAverageLast5 = context.OpponentLocal.PositionScoutNegativeConcededAverageLast5,
            OpponentPositionScoutBalanceConcededAverageLast3 = context.OpponentLocal.PositionScoutBalanceConcededAverageLast3,
            OpponentPositionScoutBalanceConcededAverageLast5 = context.OpponentLocal.PositionScoutBalanceConcededAverageLast5,
            OpponentPositionScoutGoalsConcededAverageLast3 = context.OpponentLocal.PositionScoutGoalsConcededAverageLast3,
            OpponentPositionScoutGoalsConcededAverageLast5 = context.OpponentLocal.PositionScoutGoalsConcededAverageLast5,
            OpponentPositionScoutAssistsConcededAverageLast3 = context.OpponentLocal.PositionScoutAssistsConcededAverageLast3,
            OpponentPositionScoutAssistsConcededAverageLast5 = context.OpponentLocal.PositionScoutAssistsConcededAverageLast5,
            OpponentPositionScoutTacklesConcededAverageLast3 = context.OpponentLocal.PositionScoutTacklesConcededAverageLast3,
            OpponentPositionScoutTacklesConcededAverageLast5 = context.OpponentLocal.PositionScoutTacklesConcededAverageLast5,
            OpponentExpectedPointsLast3 = context.OpponentLocal.ExpectedPointsLast3,
            OpponentWinRateLast5 = context.OpponentLocal.WinRateLast5,
            OpponentDrawRateLast5 = context.OpponentLocal.DrawRateLast5,
            OpponentLossRateLast5 = context.OpponentLocal.LossRateLast5,
            OpponentExpectedPointsLast5 = context.OpponentLocal.ExpectedPointsLast5,
            OpponentAttackStrengthLast3 = context.OpponentLocal.AttackStrengthLast3,
            OpponentAttackStrengthLast5 = context.OpponentLocal.AttackStrengthLast5,
            OpponentDefenseStrengthLast3 = context.OpponentLocal.DefenseStrengthLast3,
            OpponentDefenseStrengthLast5 = context.OpponentLocal.DefenseStrengthLast5,
            AttackVsOpponentDefenseDeltaLast3 = context.ClubLocal.AttackStrengthLast3 - context.OpponentLocal.DefenseStrengthLast3,
            AttackVsOpponentDefenseDeltaLast5 = context.ClubLocal.AttackStrengthLast5 - context.OpponentLocal.DefenseStrengthLast5,
            PositionForVsOpponentConcededDeltaLast3 = context.ClubLocal.PositionFantasyPointsForAverageLast3 - context.OpponentLocal.PositionFantasyPointsAgainstAverageLast3,
            PositionForVsOpponentConcededDeltaLast5 = context.ClubLocal.PositionFantasyPointsForAverageLast5 - context.OpponentLocal.PositionFantasyPointsAgainstAverageLast5,
            GoalDifferenceDeltaLast3 = context.ClubLocal.GoalDifferencePerMatchLast3 - context.OpponentLocal.GoalDifferencePerMatchLast3,
            GoalDifferenceDeltaLast5 = context.ClubLocal.GoalDifferencePerMatchLast5 - context.OpponentLocal.GoalDifferencePerMatchLast5,
            TargetScore = athleteRound.ScoreNum,
            TargetWasInGame = athleteRound.WasInGame,
        };

        return true;
    }

    private static FullContextAthleteScorePredictionInput CreatePredictionInput(int round, AthleteRound baseline, ResolvedFullContext context)
        => new()
        {
            RoundId = round,
            ClubCartolaId = baseline.Club.CartolaId,
            OpponentClubCartolaId = context.OpponentClubCartolaId,
            PositionCartolaId = baseline.Position.CartolaId,
            StatusCartolaId = baseline.Status.CartolaId,
            GamesNum = baseline.GamesNum,
            HomeGamesNum = baseline.HomeGamesNum,
            AwayGamesNum = baseline.AwayGamesNum,
            GamesAtVenueNum = context.IsHomeMatch ? baseline.HomeGamesNum : baseline.AwayGamesNum,
            MovingAverage = (float)baseline.MovingAverage,
            AverageScore = (float)baseline.AverageScoreNum,
            HomeAverageScore = (float)baseline.HomeAverageScore,
            AwayAverageScore = (float)baseline.AwayAverageScore,
            ContextualAverageScore = context.IsHomeMatch ? (float)baseline.HomeAverageScore : (float)baseline.AwayAverageScore,
            PriceNum = (float)baseline.PriceNum,
            VariationNum = (float)baseline.VariationNum,
            HasAthleteContext = baseline.Context is null ? 0f : 1f,
            TeamAverageScore = (float)(baseline.Context?.TeamAverageScore ?? 0m),
            TeamHomeAverageCurrent = (float)(baseline.Context?.TeamHomeAverageCurrent ?? 0m),
            TeamHomeAveragePrevious = (float)(baseline.Context?.TeamHomeAveragePrevious ?? 0m),
            TeamAwayAverageCurrent = (float)(baseline.Context?.TeamAwayAverageCurrent ?? 0m),
            TeamAwayAveragePrevious = (float)(baseline.Context?.TeamAwayAveragePrevious ?? 0m),
            TeamVenueAverageCurrent = context.IsHomeMatch ? (float)(baseline.Context?.TeamHomeAverageCurrent ?? 0m) : (float)(baseline.Context?.TeamAwayAverageCurrent ?? 0m),
            TeamVenueAveragePrevious = context.IsHomeMatch ? (float)(baseline.Context?.TeamHomeAveragePrevious ?? 0m) : (float)(baseline.Context?.TeamAwayAveragePrevious ?? 0m),
            PointsPerCartoletaCurrent = (float)(baseline.Context?.PointsPerCartoletaCurrent ?? 0m),
            PointsPerCartoletaPrevious = (float)(baseline.Context?.PointsPerCartoletaPrevious ?? 0m),
            GrossOverallAverageCurrent = (float)(baseline.Context?.GrossOverallAverageCurrent ?? 0m),
            GrossOverallMovingAverageCurrent = (float)(baseline.Context?.GrossOverallMovingAverageCurrent ?? 0m),
            GrossOverallAveragePrevious = (float)(baseline.Context?.GrossOverallAveragePrevious ?? 0m),
            GrossOverallMovingAveragePrevious = (float)(baseline.Context?.GrossOverallMovingAveragePrevious ?? 0m),
            NetOverallAverageCurrent = (float)(baseline.Context?.NetOverallAverageCurrent ?? 0m),
            NetOverallMovingAverageCurrent = (float)(baseline.Context?.NetOverallMovingAverageCurrent ?? 0m),
            NetOverallAveragePrevious = (float)(baseline.Context?.NetOverallAveragePrevious ?? 0m),
            NetOverallMovingAveragePrevious = (float)(baseline.Context?.NetOverallMovingAveragePrevious ?? 0m),
            ValuationCurrent = (float)(baseline.Context?.ValuationCurrent ?? 0m),
            MinimumToAppreciateCurrent = (float)(baseline.Context?.MinimumToAppreciateCurrent ?? 0m),
            ValuationPrevious = (float)(baseline.Context?.ValuationPrevious ?? 0m),
            MinimumToAppreciatePrevious = (float)(baseline.Context?.MinimumToAppreciatePrevious ?? 0m),
            IndexPointsPerCartoleta = (float)(baseline.Context?.IndexPointsPerCartoleta ?? 0m),
            IndexOffensiveUsage = (float)(baseline.Context?.IndexOffensiveUsage ?? 0m),
            IndexOffensiveRepresentation = (float)(baseline.Context?.IndexOffensiveRepresentation ?? 0m),
            IndexPointsRepresentation = (float)(baseline.Context?.IndexPointsRepresentation ?? 0m),
            IndexOffensiveEfficiency = (float)(baseline.Context?.IndexOffensiveEfficiency ?? 0m),
            IndexDefensiveEfficiency = (float)(baseline.Context?.IndexDefensiveEfficiency ?? 0m),
            IndexTackleDuelPercent = (float)(baseline.Context?.IndexTackleDuelPercent ?? 0m),
            IndexShotsPerGame = (float)(baseline.Context?.IndexShotsPerGame ?? 0m),
            IndexShotsPerGoal = (float)(baseline.Context?.IndexShotsPerGoal ?? 0m),
            IsHomeMatch = context.IsHomeMatch ? 1f : 0f,
            ClubTablePosition = context.ClubTablePosition,
            OpponentClubTablePosition = context.OpponentClubTablePosition,
            TablePositionDelta = context.OpponentClubTablePosition - context.ClubTablePosition,
            ClubRecentFormPoints = context.ClubRecentFormPoints,
            OpponentRecentFormPoints = context.OpponentRecentFormPoints,
            RecentFormPointsDelta = context.ClubRecentFormPoints - context.OpponentRecentFormPoints,
            ScoreLast1 = (float)context.AthleteHistory.ScoreLast1,
            ScoreAverageLast3 = (float)context.AthleteHistory.ScoreAverageLast3,
            ScoreAverageLast5 = (float)context.AthleteHistory.ScoreAverageLast5,
            ScoreEwmaLast3 = (float)context.AthleteHistory.ScoreEwmaLast3,
            ScoreEwmaLast5 = (float)context.AthleteHistory.ScoreEwmaLast5,
            ScoreStdLast5 = (float)context.AthleteHistory.ScoreStdLast5,
            ScoreFloorLast5 = (float)context.AthleteHistory.ScoreFloorLast5,
            ScoreCeilingLast5 = (float)context.AthleteHistory.ScoreCeilingLast5,
            ScoreHitRateOver3Last5 = (float)context.AthleteHistory.ScoreHitRateOver3Last5,
            ScoreHitRateOver5Last5 = (float)context.AthleteHistory.ScoreHitRateOver5Last5,
            ScoreHitRateOver8Last5 = (float)context.AthleteHistory.ScoreHitRateOver8Last5,
            ScoreNegativeRateLast5 = (float)context.AthleteHistory.ScoreNegativeRateLast5,
            ScoreTrendLast3VsPrev3 = (float)context.AthleteHistory.ScoreTrendLast3VsPrev3,
            HomeScoreAverageLast5 = (float)context.AthleteHistory.HomeScoreAverageLast5,
            AwayScoreAverageLast5 = (float)context.AthleteHistory.AwayScoreAverageLast5,
            VenueScoreAverageLast5 = (float)context.AthleteHistory.VenueScoreAverageLast5,
            HomeScoreEwmaLast3 = (float)context.AthleteHistory.HomeScoreEwmaLast3,
            AwayScoreEwmaLast3 = (float)context.AthleteHistory.AwayScoreEwmaLast3,
            VenueScoreEwmaLast3 = (float)context.AthleteHistory.VenueScoreEwmaLast3,
            ScoutOffensiveSumLast3 = (float)context.AthleteHistory.ScoutOffensiveSumLast3,
            ScoutOffensiveSumLast5 = (float)context.AthleteHistory.ScoutOffensiveSumLast5,
            ScoutOffensiveAverageLast3 = (float)context.AthleteHistory.ScoutOffensiveAverageLast3,
            ScoutOffensiveAverageLast5 = (float)context.AthleteHistory.ScoutOffensiveAverageLast5,
            ScoutOffensiveEwmaLast3 = (float)context.AthleteHistory.ScoutOffensiveEwmaLast3,
            ScoutOffensiveEwmaLast5 = (float)context.AthleteHistory.ScoutOffensiveEwmaLast5,
            ScoutDefensiveSumLast3 = (float)context.AthleteHistory.ScoutDefensiveSumLast3,
            ScoutDefensiveSumLast5 = (float)context.AthleteHistory.ScoutDefensiveSumLast5,
            ScoutDefensiveAverageLast3 = (float)context.AthleteHistory.ScoutDefensiveAverageLast3,
            ScoutDefensiveAverageLast5 = (float)context.AthleteHistory.ScoutDefensiveAverageLast5,
            ScoutDefensiveEwmaLast3 = (float)context.AthleteHistory.ScoutDefensiveEwmaLast3,
            ScoutDefensiveEwmaLast5 = (float)context.AthleteHistory.ScoutDefensiveEwmaLast5,
            ScoutNegativeSumLast3 = (float)context.AthleteHistory.ScoutNegativeSumLast3,
            ScoutNegativeSumLast5 = (float)context.AthleteHistory.ScoutNegativeSumLast5,
            ScoutNegativeAverageLast3 = (float)context.AthleteHistory.ScoutNegativeAverageLast3,
            ScoutNegativeAverageLast5 = (float)context.AthleteHistory.ScoutNegativeAverageLast5,
            ScoutNegativeEwmaLast3 = (float)context.AthleteHistory.ScoutNegativeEwmaLast3,
            ScoutNegativeEwmaLast5 = (float)context.AthleteHistory.ScoutNegativeEwmaLast5,
            ScoutBalanceSumLast3 = (float)context.AthleteHistory.ScoutBalanceSumLast3,
            ScoutBalanceSumLast5 = (float)context.AthleteHistory.ScoutBalanceSumLast5,
            ScoutBalanceAverageLast3 = (float)context.AthleteHistory.ScoutBalanceAverageLast3,
            ScoutBalanceAverageLast5 = (float)context.AthleteHistory.ScoutBalanceAverageLast5,
            ScoutBalanceEwmaLast3 = (float)context.AthleteHistory.ScoutBalanceEwmaLast3,
            ScoutBalanceEwmaLast5 = (float)context.AthleteHistory.ScoutBalanceEwmaLast5,
            ScoutGoalsAverageLast3 = (float)context.AthleteHistory.ScoutGoalsAverageLast3,
            ScoutGoalsAverageLast5 = (float)context.AthleteHistory.ScoutGoalsAverageLast5,
            ScoutAssistsAverageLast3 = (float)context.AthleteHistory.ScoutAssistsAverageLast3,
            ScoutAssistsAverageLast5 = (float)context.AthleteHistory.ScoutAssistsAverageLast5,
            ScoutTacklesAverageLast3 = (float)context.AthleteHistory.ScoutTacklesAverageLast3,
            ScoutTacklesAverageLast5 = (float)context.AthleteHistory.ScoutTacklesAverageLast5,
            ScoutCleanSheetRateLast3 = (float)context.AthleteHistory.ScoutCleanSheetRateLast3,
            ScoutCleanSheetRateLast5 = (float)context.AthleteHistory.ScoutCleanSheetRateLast5,
            PlayRateLast5 = (float)context.AthleteHistory.PlayRateLast5,
            RoundsSinceLastInGame = (float)context.AthleteHistory.RoundsSinceLastInGame,
            StatusStabilityLast5 = (float)context.AthleteHistory.StatusStabilityLast5,
            StatusChangeFlag = (float)context.AthleteHistory.StatusChangeFlag,
            EmpiricalPlayRateForCurrentStatus = (float)context.AthleteHistory.EmpiricalPlayRateForCurrentStatus,
            PriceTrendLast3 = (float)context.AthleteHistory.PriceTrendLast3,
            PriceVolatilityLast5 = (float)context.AthleteHistory.PriceVolatilityLast5,
            HistoricalPointsPerCartoletaLast3 = (float)context.AthleteHistory.HistoricalPointsPerCartoletaLast3,
            HistoricalPointsPerCartoletaLast5 = (float)context.AthleteHistory.HistoricalPointsPerCartoletaLast5,
            PositionRelativePriceSignal = (float)context.AthleteHistory.PositionRelativePriceSignal,
            PriceDeltaVsHistoricalAverage = (float)context.AthleteHistory.PriceDeltaVsHistoricalAverage,
            ClubMatchesSampleSize = context.ClubLocal.MatchesSampleSize,
            ClubPointsPerMatchLast3 = (float)context.ClubLocal.PointsPerMatchLast3,
            ClubPointsPerMatchLast5 = (float)context.ClubLocal.PointsPerMatchLast5,
            ClubVenuePointsPerMatchLast3 = (float)context.ClubLocal.VenuePointsPerMatchLast3,
            ClubVenuePointsPerMatchLast5 = (float)context.ClubLocal.VenuePointsPerMatchLast5,
            ClubGoalsForPerMatchLast3 = (float)context.ClubLocal.GoalsForPerMatchLast3,
            ClubGoalsForPerMatchLast5 = (float)context.ClubLocal.GoalsForPerMatchLast5,
            ClubGoalsAgainstPerMatchLast3 = (float)context.ClubLocal.GoalsAgainstPerMatchLast3,
            ClubGoalsAgainstPerMatchLast5 = (float)context.ClubLocal.GoalsAgainstPerMatchLast5,
            ClubGoalDifferencePerMatchLast3 = (float)context.ClubLocal.GoalDifferencePerMatchLast3,
            ClubGoalDifferencePerMatchLast5 = (float)context.ClubLocal.GoalDifferencePerMatchLast5,
            ClubVenueGoalsForPerMatchLast3 = (float)context.ClubLocal.VenueGoalsForPerMatchLast3,
            ClubVenueGoalsForPerMatchLast5 = (float)context.ClubLocal.VenueGoalsForPerMatchLast5,
            ClubVenueGoalsAgainstPerMatchLast3 = (float)context.ClubLocal.VenueGoalsAgainstPerMatchLast3,
            ClubVenueGoalsAgainstPerMatchLast5 = (float)context.ClubLocal.VenueGoalsAgainstPerMatchLast5,
            ClubCleanSheetRateLast3 = (float)context.ClubLocal.CleanSheetRateLast3,
            ClubCleanSheetRateLast5 = (float)context.ClubLocal.CleanSheetRateLast5,
            ClubFailedToScoreRateLast3 = (float)context.ClubLocal.FailedToScoreRateLast3,
            ClubFailedToScoreRateLast5 = (float)context.ClubLocal.FailedToScoreRateLast5,
            ClubFantasyPointsForAverageLast3 = (float)context.ClubLocal.FantasyPointsForAverageLast3,
            ClubFantasyPointsForAverageLast5 = (float)context.ClubLocal.FantasyPointsForAverageLast5,
            ClubFantasyPointsAgainstAverageLast3 = (float)context.ClubLocal.FantasyPointsAgainstAverageLast3,
            ClubFantasyPointsAgainstAverageLast5 = (float)context.ClubLocal.FantasyPointsAgainstAverageLast5,
            ClubScoutOffensiveConcededAverageLast3 = (float)context.ClubLocal.ScoutOffensiveConcededAverageLast3,
            ClubScoutOffensiveConcededAverageLast5 = (float)context.ClubLocal.ScoutOffensiveConcededAverageLast5,
            ClubScoutDefensiveConcededAverageLast3 = (float)context.ClubLocal.ScoutDefensiveConcededAverageLast3,
            ClubScoutDefensiveConcededAverageLast5 = (float)context.ClubLocal.ScoutDefensiveConcededAverageLast5,
            ClubScoutNegativeConcededAverageLast3 = (float)context.ClubLocal.ScoutNegativeConcededAverageLast3,
            ClubScoutNegativeConcededAverageLast5 = (float)context.ClubLocal.ScoutNegativeConcededAverageLast5,
            ClubScoutBalanceConcededAverageLast3 = (float)context.ClubLocal.ScoutBalanceConcededAverageLast3,
            ClubScoutBalanceConcededAverageLast5 = (float)context.ClubLocal.ScoutBalanceConcededAverageLast5,
            ClubScoutGoalsConcededAverageLast3 = (float)context.ClubLocal.ScoutGoalsConcededAverageLast3,
            ClubScoutGoalsConcededAverageLast5 = (float)context.ClubLocal.ScoutGoalsConcededAverageLast5,
            ClubScoutAssistsConcededAverageLast3 = (float)context.ClubLocal.ScoutAssistsConcededAverageLast3,
            ClubScoutAssistsConcededAverageLast5 = (float)context.ClubLocal.ScoutAssistsConcededAverageLast5,
            ClubScoutTacklesConcededAverageLast3 = (float)context.ClubLocal.ScoutTacklesConcededAverageLast3,
            ClubScoutTacklesConcededAverageLast5 = (float)context.ClubLocal.ScoutTacklesConcededAverageLast5,
            ClubPositionFantasyPointsForAverageLast3 = (float)context.ClubLocal.PositionFantasyPointsForAverageLast3,
            ClubPositionFantasyPointsForAverageLast5 = (float)context.ClubLocal.PositionFantasyPointsForAverageLast5,
            ClubPositionFantasyPointsAgainstAverageLast3 = (float)context.ClubLocal.PositionFantasyPointsAgainstAverageLast3,
            ClubPositionFantasyPointsAgainstAverageLast5 = (float)context.ClubLocal.PositionFantasyPointsAgainstAverageLast5,
            ClubPositionScoutOffensiveConcededAverageLast3 = (float)context.ClubLocal.PositionScoutOffensiveConcededAverageLast3,
            ClubPositionScoutOffensiveConcededAverageLast5 = (float)context.ClubLocal.PositionScoutOffensiveConcededAverageLast5,
            ClubPositionScoutDefensiveConcededAverageLast3 = (float)context.ClubLocal.PositionScoutDefensiveConcededAverageLast3,
            ClubPositionScoutDefensiveConcededAverageLast5 = (float)context.ClubLocal.PositionScoutDefensiveConcededAverageLast5,
            ClubPositionScoutNegativeConcededAverageLast3 = (float)context.ClubLocal.PositionScoutNegativeConcededAverageLast3,
            ClubPositionScoutNegativeConcededAverageLast5 = (float)context.ClubLocal.PositionScoutNegativeConcededAverageLast5,
            ClubPositionScoutBalanceConcededAverageLast3 = (float)context.ClubLocal.PositionScoutBalanceConcededAverageLast3,
            ClubPositionScoutBalanceConcededAverageLast5 = (float)context.ClubLocal.PositionScoutBalanceConcededAverageLast5,
            ClubPositionScoutGoalsConcededAverageLast3 = (float)context.ClubLocal.PositionScoutGoalsConcededAverageLast3,
            ClubPositionScoutGoalsConcededAverageLast5 = (float)context.ClubLocal.PositionScoutGoalsConcededAverageLast5,
            ClubPositionScoutAssistsConcededAverageLast3 = (float)context.ClubLocal.PositionScoutAssistsConcededAverageLast3,
            ClubPositionScoutAssistsConcededAverageLast5 = (float)context.ClubLocal.PositionScoutAssistsConcededAverageLast5,
            ClubPositionScoutTacklesConcededAverageLast3 = (float)context.ClubLocal.PositionScoutTacklesConcededAverageLast3,
            ClubPositionScoutTacklesConcededAverageLast5 = (float)context.ClubLocal.PositionScoutTacklesConcededAverageLast5,
            ClubExpectedPointsLast3 = (float)context.ClubLocal.ExpectedPointsLast3,
            ClubWinRateLast5 = (float)context.ClubLocal.WinRateLast5,
            ClubDrawRateLast5 = (float)context.ClubLocal.DrawRateLast5,
            ClubLossRateLast5 = (float)context.ClubLocal.LossRateLast5,
            ClubExpectedPointsLast5 = (float)context.ClubLocal.ExpectedPointsLast5,
            ClubAttackStrengthLast3 = (float)context.ClubLocal.AttackStrengthLast3,
            ClubAttackStrengthLast5 = (float)context.ClubLocal.AttackStrengthLast5,
            ClubDefenseStrengthLast3 = (float)context.ClubLocal.DefenseStrengthLast3,
            ClubDefenseStrengthLast5 = (float)context.ClubLocal.DefenseStrengthLast5,
            OpponentMatchesSampleSize = context.OpponentLocal.MatchesSampleSize,
            OpponentPointsPerMatchLast3 = (float)context.OpponentLocal.PointsPerMatchLast3,
            OpponentPointsPerMatchLast5 = (float)context.OpponentLocal.PointsPerMatchLast5,
            OpponentVenuePointsPerMatchLast3 = (float)context.OpponentLocal.VenuePointsPerMatchLast3,
            OpponentVenuePointsPerMatchLast5 = (float)context.OpponentLocal.VenuePointsPerMatchLast5,
            OpponentGoalsForPerMatchLast3 = (float)context.OpponentLocal.GoalsForPerMatchLast3,
            OpponentGoalsForPerMatchLast5 = (float)context.OpponentLocal.GoalsForPerMatchLast5,
            OpponentGoalsAgainstPerMatchLast3 = (float)context.OpponentLocal.GoalsAgainstPerMatchLast3,
            OpponentGoalsAgainstPerMatchLast5 = (float)context.OpponentLocal.GoalsAgainstPerMatchLast5,
            OpponentGoalDifferencePerMatchLast3 = (float)context.OpponentLocal.GoalDifferencePerMatchLast3,
            OpponentGoalDifferencePerMatchLast5 = (float)context.OpponentLocal.GoalDifferencePerMatchLast5,
            OpponentVenueGoalsForPerMatchLast3 = (float)context.OpponentLocal.VenueGoalsForPerMatchLast3,
            OpponentVenueGoalsForPerMatchLast5 = (float)context.OpponentLocal.VenueGoalsForPerMatchLast5,
            OpponentVenueGoalsAgainstPerMatchLast3 = (float)context.OpponentLocal.VenueGoalsAgainstPerMatchLast3,
            OpponentVenueGoalsAgainstPerMatchLast5 = (float)context.OpponentLocal.VenueGoalsAgainstPerMatchLast5,
            OpponentCleanSheetRateLast3 = (float)context.OpponentLocal.CleanSheetRateLast3,
            OpponentCleanSheetRateLast5 = (float)context.OpponentLocal.CleanSheetRateLast5,
            OpponentFailedToScoreRateLast3 = (float)context.OpponentLocal.FailedToScoreRateLast3,
            OpponentFailedToScoreRateLast5 = (float)context.OpponentLocal.FailedToScoreRateLast5,
            OpponentFantasyPointsForAverageLast3 = (float)context.OpponentLocal.FantasyPointsForAverageLast3,
            OpponentFantasyPointsForAverageLast5 = (float)context.OpponentLocal.FantasyPointsForAverageLast5,
            OpponentFantasyPointsAgainstAverageLast3 = (float)context.OpponentLocal.FantasyPointsAgainstAverageLast3,
            OpponentFantasyPointsAgainstAverageLast5 = (float)context.OpponentLocal.FantasyPointsAgainstAverageLast5,
            OpponentScoutOffensiveConcededAverageLast3 = (float)context.OpponentLocal.ScoutOffensiveConcededAverageLast3,
            OpponentScoutOffensiveConcededAverageLast5 = (float)context.OpponentLocal.ScoutOffensiveConcededAverageLast5,
            OpponentScoutDefensiveConcededAverageLast3 = (float)context.OpponentLocal.ScoutDefensiveConcededAverageLast3,
            OpponentScoutDefensiveConcededAverageLast5 = (float)context.OpponentLocal.ScoutDefensiveConcededAverageLast5,
            OpponentScoutNegativeConcededAverageLast3 = (float)context.OpponentLocal.ScoutNegativeConcededAverageLast3,
            OpponentScoutNegativeConcededAverageLast5 = (float)context.OpponentLocal.ScoutNegativeConcededAverageLast5,
            OpponentScoutBalanceConcededAverageLast3 = (float)context.OpponentLocal.ScoutBalanceConcededAverageLast3,
            OpponentScoutBalanceConcededAverageLast5 = (float)context.OpponentLocal.ScoutBalanceConcededAverageLast5,
            OpponentScoutGoalsConcededAverageLast3 = (float)context.OpponentLocal.ScoutGoalsConcededAverageLast3,
            OpponentScoutGoalsConcededAverageLast5 = (float)context.OpponentLocal.ScoutGoalsConcededAverageLast5,
            OpponentScoutAssistsConcededAverageLast3 = (float)context.OpponentLocal.ScoutAssistsConcededAverageLast3,
            OpponentScoutAssistsConcededAverageLast5 = (float)context.OpponentLocal.ScoutAssistsConcededAverageLast5,
            OpponentScoutTacklesConcededAverageLast3 = (float)context.OpponentLocal.ScoutTacklesConcededAverageLast3,
            OpponentScoutTacklesConcededAverageLast5 = (float)context.OpponentLocal.ScoutTacklesConcededAverageLast5,
            OpponentPositionFantasyPointsForAverageLast3 = (float)context.OpponentLocal.PositionFantasyPointsForAverageLast3,
            OpponentPositionFantasyPointsForAverageLast5 = (float)context.OpponentLocal.PositionFantasyPointsForAverageLast5,
            OpponentPositionFantasyPointsAgainstAverageLast3 = (float)context.OpponentLocal.PositionFantasyPointsAgainstAverageLast3,
            OpponentPositionFantasyPointsAgainstAverageLast5 = (float)context.OpponentLocal.PositionFantasyPointsAgainstAverageLast5,
            OpponentPositionScoutOffensiveConcededAverageLast3 = (float)context.OpponentLocal.PositionScoutOffensiveConcededAverageLast3,
            OpponentPositionScoutOffensiveConcededAverageLast5 = (float)context.OpponentLocal.PositionScoutOffensiveConcededAverageLast5,
            OpponentPositionScoutDefensiveConcededAverageLast3 = (float)context.OpponentLocal.PositionScoutDefensiveConcededAverageLast3,
            OpponentPositionScoutDefensiveConcededAverageLast5 = (float)context.OpponentLocal.PositionScoutDefensiveConcededAverageLast5,
            OpponentPositionScoutNegativeConcededAverageLast3 = (float)context.OpponentLocal.PositionScoutNegativeConcededAverageLast3,
            OpponentPositionScoutNegativeConcededAverageLast5 = (float)context.OpponentLocal.PositionScoutNegativeConcededAverageLast5,
            OpponentPositionScoutBalanceConcededAverageLast3 = (float)context.OpponentLocal.PositionScoutBalanceConcededAverageLast3,
            OpponentPositionScoutBalanceConcededAverageLast5 = (float)context.OpponentLocal.PositionScoutBalanceConcededAverageLast5,
            OpponentPositionScoutGoalsConcededAverageLast3 = (float)context.OpponentLocal.PositionScoutGoalsConcededAverageLast3,
            OpponentPositionScoutGoalsConcededAverageLast5 = (float)context.OpponentLocal.PositionScoutGoalsConcededAverageLast5,
            OpponentPositionScoutAssistsConcededAverageLast3 = (float)context.OpponentLocal.PositionScoutAssistsConcededAverageLast3,
            OpponentPositionScoutAssistsConcededAverageLast5 = (float)context.OpponentLocal.PositionScoutAssistsConcededAverageLast5,
            OpponentPositionScoutTacklesConcededAverageLast3 = (float)context.OpponentLocal.PositionScoutTacklesConcededAverageLast3,
            OpponentPositionScoutTacklesConcededAverageLast5 = (float)context.OpponentLocal.PositionScoutTacklesConcededAverageLast5,
            OpponentExpectedPointsLast3 = (float)context.OpponentLocal.ExpectedPointsLast3,
            OpponentWinRateLast5 = (float)context.OpponentLocal.WinRateLast5,
            OpponentDrawRateLast5 = (float)context.OpponentLocal.DrawRateLast5,
            OpponentLossRateLast5 = (float)context.OpponentLocal.LossRateLast5,
            OpponentExpectedPointsLast5 = (float)context.OpponentLocal.ExpectedPointsLast5,
            OpponentAttackStrengthLast3 = (float)context.OpponentLocal.AttackStrengthLast3,
            OpponentAttackStrengthLast5 = (float)context.OpponentLocal.AttackStrengthLast5,
            OpponentDefenseStrengthLast3 = (float)context.OpponentLocal.DefenseStrengthLast3,
            OpponentDefenseStrengthLast5 = (float)context.OpponentLocal.DefenseStrengthLast5,
            AttackVsOpponentDefenseDeltaLast3 = (float)(context.ClubLocal.AttackStrengthLast3 - context.OpponentLocal.DefenseStrengthLast3),
            AttackVsOpponentDefenseDeltaLast5 = (float)(context.ClubLocal.AttackStrengthLast5 - context.OpponentLocal.DefenseStrengthLast5),
            PositionForVsOpponentConcededDeltaLast3 = (float)(context.ClubLocal.PositionFantasyPointsForAverageLast3 - context.OpponentLocal.PositionFantasyPointsAgainstAverageLast3),
            PositionForVsOpponentConcededDeltaLast5 = (float)(context.ClubLocal.PositionFantasyPointsForAverageLast5 - context.OpponentLocal.PositionFantasyPointsAgainstAverageLast5),
            GoalDifferenceDeltaLast3 = (float)(context.ClubLocal.GoalDifferencePerMatchLast3 - context.OpponentLocal.GoalDifferencePerMatchLast3),
            GoalDifferenceDeltaLast5 = (float)(context.ClubLocal.GoalDifferencePerMatchLast5 - context.OpponentLocal.GoalDifferencePerMatchLast5),
        };

    private static bool TryResolveContext(
        int targetRound,
        AthleteRound athleteRound,
        FullContextFeatureLookup lookup,
        out ResolvedFullContext context)
    {
        context = default;

        if (!lookup.MatchByRoundAndClubId.TryGetValue((targetRound, athleteRound.ClubId), out var match))
        {
            return false;
        }

        var isHomeMatch = match.HomeClubId == athleteRound.ClubId;
        var opponentClub = isHomeMatch ? match.AwayClub : match.HomeClub;

        context = new ResolvedFullContext(
            isHomeMatch,
            isHomeMatch ? match.HomeClubPosition : match.AwayClubPosition,
            isHomeMatch ? match.AwayClubPosition : match.HomeClubPosition,
            isHomeMatch ? match.HomeForm : match.AwayForm,
            isHomeMatch ? match.AwayForm : match.HomeForm,
            opponentClub.CartolaId,
            opponentClub.Nickname,
            ConvertFormToPoints(isHomeMatch ? match.HomeForm : match.AwayForm),
            ConvertFormToPoints(isHomeMatch ? match.AwayForm : match.HomeForm),
            FullContextHistoricalFeatureCalculator.BuildAthleteHistorySnapshot(lookup.AthleteHistoryByAthleteId, lookup.AveragePriceByPositionAndRound, athleteRound.AthleteId, athleteRound.StatusId, athleteRound.PriceNum, athleteRound.PositionId, targetRound, isHomeMatch),
            FullContextHistoricalFeatureCalculator.BuildLocalClubContextSnapshot(
                lookup.ClubMatchHistoryByClubId,
                lookup.FantasyPointsForByClubAndRound,
                lookup.FantasyPointsAgainstByClubAndRound,
                lookup.OffensiveScoutsConcededByClubAndRound,
                lookup.DefensiveScoutsConcededByClubAndRound,
                lookup.NegativeScoutsConcededByClubAndRound,
                lookup.BalanceScoutsConcededByClubAndRound,
                lookup.GoalsScoutsConcededByClubAndRound,
                lookup.AssistsScoutsConcededByClubAndRound,
                lookup.TacklesScoutsConcededByClubAndRound,
                lookup.PositionFantasyPointsForByClubPositionAndRound,
                lookup.PositionFantasyPointsAgainstByClubPositionAndRound,
                lookup.OffensiveScoutsConcededByClubPositionAndRound,
                lookup.DefensiveScoutsConcededByClubPositionAndRound,
                lookup.NegativeScoutsConcededByClubPositionAndRound,
                lookup.BalanceScoutsConcededByClubPositionAndRound,
                lookup.GoalsScoutsConcededByClubPositionAndRound,
                lookup.AssistsScoutsConcededByClubPositionAndRound,
                lookup.TacklesScoutsConcededByClubPositionAndRound,
                athleteRound.ClubId,
                athleteRound.PositionId,
                targetRound,
                isHomeMatch),
            FullContextHistoricalFeatureCalculator.BuildLocalClubContextSnapshot(
                lookup.ClubMatchHistoryByClubId,
                lookup.FantasyPointsForByClubAndRound,
                lookup.FantasyPointsAgainstByClubAndRound,
                lookup.OffensiveScoutsConcededByClubAndRound,
                lookup.DefensiveScoutsConcededByClubAndRound,
                lookup.NegativeScoutsConcededByClubAndRound,
                lookup.BalanceScoutsConcededByClubAndRound,
                lookup.GoalsScoutsConcededByClubAndRound,
                lookup.AssistsScoutsConcededByClubAndRound,
                lookup.TacklesScoutsConcededByClubAndRound,
                lookup.PositionFantasyPointsForByClubPositionAndRound,
                lookup.PositionFantasyPointsAgainstByClubPositionAndRound,
                lookup.OffensiveScoutsConcededByClubPositionAndRound,
                lookup.DefensiveScoutsConcededByClubPositionAndRound,
                lookup.NegativeScoutsConcededByClubPositionAndRound,
                lookup.BalanceScoutsConcededByClubPositionAndRound,
                lookup.GoalsScoutsConcededByClubPositionAndRound,
                lookup.AssistsScoutsConcededByClubPositionAndRound,
                lookup.TacklesScoutsConcededByClubPositionAndRound,
                opponentClub.Id,
                athleteRound.PositionId,
                targetRound,
                !isHomeMatch));

        return true;
    }

    private static float ConvertFormToPoints(string? form)
    {
        if (string.IsNullOrWhiteSpace(form))
        {
            return 0f;
        }

        var score = 0;
        foreach (var item in form.Trim().ToUpperInvariant())
        {
            score += item switch
            {
                'V' => 3,
                'E' => 1,
                _ => 0,
            };
        }

        return score;
    }

    private static void AddAthleteHistoryObservation(IDictionary<int, List<AthleteHistoryObservation>> athleteHistoryByAthleteId, int athleteId, AthleteHistoryObservation observation)
    {
        if (!athleteHistoryByAthleteId.TryGetValue(athleteId, out var history))
        {
            history = [];
            athleteHistoryByAthleteId[athleteId] = history;
        }

        history.Add(observation);
    }

    private static void AddClubHistoryPoint(IDictionary<int, List<ClubMatchHistoryPoint>> clubHistoryByClubId, int clubId, ClubMatchHistoryPoint historyPoint)
    {
        if (!clubHistoryByClubId.TryGetValue(clubId, out var history))
        {
            history = [];
            clubHistoryByClubId[clubId] = history;
        }

        history.Add(historyPoint);
    }

    private static void Accumulate<TKey>(IDictionary<TKey, decimal> dictionary, TKey key, decimal value)
        where TKey : notnull
        => dictionary[key] = dictionary.TryGetValue(key, out var current) ? current + value : value;

    private static void Accumulate<TKey>(IDictionary<TKey, int> dictionary, TKey key, int value)
        where TKey : notnull
        => dictionary[key] = dictionary.TryGetValue(key, out var current) ? current + value : value;

    private static decimal ResolveMatchPoints(int? goalsFor, int? goalsAgainst)
    {
        if (!goalsFor.HasValue || !goalsAgainst.HasValue)
        {
            return 0m;
        }

        return goalsFor > goalsAgainst ? 3m : goalsFor == goalsAgainst ? 1m : 0m;
    }

    private static decimal GetScoutValue(IReadOnlyDictionary<string, decimal> scouts, string key)
        => scouts.TryGetValue(key, out var value) ? value : 0m;

    private readonly record struct ResolvedFullContext(
        bool IsHomeMatch,
        int ClubTablePosition,
        int OpponentClubTablePosition,
        string ClubRecentForm,
        string OpponentRecentForm,
        int OpponentClubCartolaId,
        string OpponentClubName,
        float ClubRecentFormPoints,
        float OpponentRecentFormPoints,
        AthleteHistorySnapshot AthleteHistory,
        LocalClubContextSnapshot ClubLocal,
        LocalClubContextSnapshot OpponentLocal);
}
