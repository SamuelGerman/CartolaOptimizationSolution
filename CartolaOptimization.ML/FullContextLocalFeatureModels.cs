using CartolaOptimization.Domain.Entities;

namespace CartolaOptimization.ML;

internal sealed class FullContextFeatureLookup
{
    public required IReadOnlyDictionary<(int Round, int ClubId), Match> MatchByRoundAndClubId { get; init; }

    public required IReadOnlyDictionary<int, IReadOnlyList<AthleteHistoryObservation>> AthleteHistoryByAthleteId { get; init; }

    public required IReadOnlyDictionary<int, IReadOnlyList<AthleteHistoryObservation>> PositionHistoryByPositionId { get; init; }

    public required IReadOnlyList<AthleteHistoryObservation> GlobalAthleteHistory { get; init; }

    public required IReadOnlyDictionary<int, IReadOnlyList<ClubMatchHistoryPoint>> ClubMatchHistoryByClubId { get; init; }

    public required IReadOnlyDictionary<(int ClubId, int Round), decimal> FantasyPointsForByClubAndRound { get; init; }

    public required IReadOnlyDictionary<(int ClubId, int Round), decimal> FantasyPointsAgainstByClubAndRound { get; init; }

    public required IReadOnlyDictionary<(int ClubId, int PositionId, int Round), decimal> PositionFantasyPointsForByClubPositionAndRound { get; init; }

    public required IReadOnlyDictionary<(int ClubId, int PositionId, int Round), decimal> PositionFantasyPointsAgainstByClubPositionAndRound { get; init; }

    public required IReadOnlyDictionary<(int PositionId, int Round), decimal> AveragePriceByPositionAndRound { get; init; }

    public required IReadOnlyDictionary<(int ClubId, int Round), decimal> OffensiveScoutsConcededByClubAndRound { get; init; }

    public required IReadOnlyDictionary<(int ClubId, int Round), decimal> DefensiveScoutsConcededByClubAndRound { get; init; }

    public required IReadOnlyDictionary<(int ClubId, int Round), decimal> NegativeScoutsConcededByClubAndRound { get; init; }

    public required IReadOnlyDictionary<(int ClubId, int Round), decimal> BalanceScoutsConcededByClubAndRound { get; init; }

    public required IReadOnlyDictionary<(int ClubId, int Round), decimal> GoalsScoutsConcededByClubAndRound { get; init; }

    public required IReadOnlyDictionary<(int ClubId, int Round), decimal> AssistsScoutsConcededByClubAndRound { get; init; }

    public required IReadOnlyDictionary<(int ClubId, int Round), decimal> TacklesScoutsConcededByClubAndRound { get; init; }

    public required IReadOnlyDictionary<(int ClubId, int PositionId, int Round), decimal> OffensiveScoutsConcededByClubPositionAndRound { get; init; }

    public required IReadOnlyDictionary<(int ClubId, int PositionId, int Round), decimal> DefensiveScoutsConcededByClubPositionAndRound { get; init; }

    public required IReadOnlyDictionary<(int ClubId, int PositionId, int Round), decimal> NegativeScoutsConcededByClubPositionAndRound { get; init; }

    public required IReadOnlyDictionary<(int ClubId, int PositionId, int Round), decimal> BalanceScoutsConcededByClubPositionAndRound { get; init; }

    public required IReadOnlyDictionary<(int ClubId, int PositionId, int Round), decimal> GoalsScoutsConcededByClubPositionAndRound { get; init; }

    public required IReadOnlyDictionary<(int ClubId, int PositionId, int Round), decimal> AssistsScoutsConcededByClubPositionAndRound { get; init; }

    public required IReadOnlyDictionary<(int ClubId, int PositionId, int Round), decimal> TacklesScoutsConcededByClubPositionAndRound { get; init; }
}

internal readonly record struct AthleteHistoryObservation(
    int RoundId,
    int PositionId,
    bool? IsHomeMatch,
    int StatusId,
    bool WasInGame,
    decimal Price,
    decimal Score,
    decimal OffensiveScoutScore,
    decimal DefensiveScoutScore,
    decimal NegativeScoutScore,
    decimal GoalsCount,
    decimal AssistsCount,
    decimal TacklesCount,
    decimal CleanSheetCount)
{
    public decimal PositiveScoutScore => OffensiveScoutScore + DefensiveScoutScore;

    public decimal BalanceScoutScore => PositiveScoutScore - NegativeScoutScore;
}

internal readonly record struct ClubMatchHistoryPoint(
    int RoundId,
    bool IsHomeMatch,
    int OpponentClubId,
    decimal Points,
    decimal GoalsFor,
    decimal GoalsAgainst);

internal readonly record struct AthleteHistorySnapshot(
    decimal GamesNumPrior,
    decimal HomeGamesNumPrior,
    decimal AwayGamesNumPrior,
    decimal VenueGamesNumPrior,
    decimal PlayedMatchesSampleSize,
    decimal PlayedMatchesSampleSizeLast5,
    decimal ObservationCountLast3,
    decimal ObservationCountLast5,
    decimal PreviousObservationCountLast3,
    decimal HomeObservationCountLast5,
    decimal AwayObservationCountLast5,
    decimal VenueObservationCountLast5,
    decimal StatusMatchedObservationCountLast5,
    decimal HistoryCoverageLast3,
    decimal HistoryCoverageLast5,
    decimal HomeHistoryCoverageLast5,
    decimal AwayHistoryCoverageLast5,
    decimal VenueHistoryCoverageLast5,
    decimal CurrentStatusCoverageLast5,
    decimal IsColdStartAthlete,
    decimal MovingAveragePrior,
    decimal AverageScorePrior,
    decimal HomeAverageScorePrior,
    decimal AwayAverageScorePrior,
    decimal VenueAverageScorePrior,
    decimal ScoreLast1,
    decimal ScoreAverageLast3,
    decimal ScoreAverageLast5,
    decimal ScoreEwmaLast3,
    decimal ScoreEwmaLast5,
    decimal ScoreStdLast5,
    decimal ScoreFloorLast5,
    decimal ScoreCeilingLast5,
    decimal ScoreHitRateOver3Last5,
    decimal ScoreHitRateOver5Last5,
    decimal ScoreHitRateOver8Last5,
    decimal ScoreNegativeRateLast5,
    decimal ScoreTrendLast3VsPrev3,
    decimal HomeScoreAverageLast5,
    decimal AwayScoreAverageLast5,
    decimal VenueScoreAverageLast5,
    decimal HomeScoreEwmaLast3,
    decimal AwayScoreEwmaLast3,
    decimal VenueScoreEwmaLast3,
    decimal ScoutOffensiveSumLast3,
    decimal ScoutOffensiveSumLast5,
    decimal ScoutOffensiveAverageLast3,
    decimal ScoutOffensiveAverageLast5,
    decimal ScoutOffensiveEwmaLast3,
    decimal ScoutOffensiveEwmaLast5,
    decimal ScoutDefensiveSumLast3,
    decimal ScoutDefensiveSumLast5,
    decimal ScoutDefensiveAverageLast3,
    decimal ScoutDefensiveAverageLast5,
    decimal ScoutDefensiveEwmaLast3,
    decimal ScoutDefensiveEwmaLast5,
    decimal ScoutNegativeSumLast3,
    decimal ScoutNegativeSumLast5,
    decimal ScoutNegativeAverageLast3,
    decimal ScoutNegativeAverageLast5,
    decimal ScoutNegativeEwmaLast3,
    decimal ScoutNegativeEwmaLast5,
    decimal ScoutBalanceSumLast3,
    decimal ScoutBalanceSumLast5,
    decimal ScoutBalanceAverageLast3,
    decimal ScoutBalanceAverageLast5,
    decimal ScoutBalanceEwmaLast3,
    decimal ScoutBalanceEwmaLast5,
    decimal ScoutGoalsAverageLast3,
    decimal ScoutGoalsAverageLast5,
    decimal ScoutAssistsAverageLast3,
    decimal ScoutAssistsAverageLast5,
    decimal ScoutTacklesAverageLast3,
    decimal ScoutTacklesAverageLast5,
    decimal ScoutCleanSheetRateLast3,
    decimal ScoutCleanSheetRateLast5,
    decimal PlayRateLast5,
    decimal RoundsSinceLastInGame,
    decimal StatusStabilityLast5,
    decimal StatusChangeFlag,
    decimal EmpiricalPlayRateForCurrentStatus,
    decimal PriceTrendLast3,
    decimal PriceVolatilityLast5,
    decimal HistoricalPointsPerCartoletaLast3,
    decimal HistoricalPointsPerCartoletaLast5,
    decimal PositionRelativePriceSignal,
    decimal PriceDeltaVsHistoricalAverage);

internal readonly record struct AthleteHistoryPrior(
    AthleteHistorySnapshot Snapshot,
    decimal HistoricalAveragePrice,
    int PlayedCount,
    int PlayedLast5Count,
    int PlayedHomeCount,
    int PlayedAwayCount,
    int PlayedVenueCount,
    int Last3Count,
    int Last5Count,
    int Previous3Count,
    int HomeLast5Count,
    int AwayLast5Count,
    int VenueLast5Count,
    int HomeLast3Count,
    int AwayLast3Count,
    int VenueLast3Count,
    int StatusMatchedLast5Count);

internal readonly record struct LocalClubContextSnapshot(
    int MatchesSampleSize,
    int MatchesSampleSizeLast3,
    int MatchesSampleSizeLast5,
    decimal PointsPerMatchLast3,
    decimal PointsPerMatchLast5,
    decimal VenuePointsPerMatchLast3,
    decimal VenuePointsPerMatchLast5,
    decimal GoalsForPerMatchLast3,
    decimal GoalsForPerMatchLast5,
    decimal GoalsAgainstPerMatchLast3,
    decimal GoalsAgainstPerMatchLast5,
    decimal GoalDifferencePerMatchLast3,
    decimal GoalDifferencePerMatchLast5,
    decimal VenueGoalsForPerMatchLast3,
    decimal VenueGoalsForPerMatchLast5,
    decimal VenueGoalsAgainstPerMatchLast3,
    decimal VenueGoalsAgainstPerMatchLast5,
    decimal CleanSheetRateLast3,
    decimal CleanSheetRateLast5,
    decimal FailedToScoreRateLast3,
    decimal FailedToScoreRateLast5,
    decimal FantasyPointsForAverageLast3,
    decimal FantasyPointsForAverageLast5,
    decimal FantasyPointsAgainstAverageLast3,
    decimal FantasyPointsAgainstAverageLast5,
    decimal ScoutOffensiveConcededAverageLast3,
    decimal ScoutOffensiveConcededAverageLast5,
    decimal ScoutDefensiveConcededAverageLast3,
    decimal ScoutDefensiveConcededAverageLast5,
    decimal ScoutNegativeConcededAverageLast3,
    decimal ScoutNegativeConcededAverageLast5,
    decimal ScoutBalanceConcededAverageLast3,
    decimal ScoutBalanceConcededAverageLast5,
    decimal ScoutGoalsConcededAverageLast3,
    decimal ScoutGoalsConcededAverageLast5,
    decimal ScoutAssistsConcededAverageLast3,
    decimal ScoutAssistsConcededAverageLast5,
    decimal ScoutTacklesConcededAverageLast3,
    decimal ScoutTacklesConcededAverageLast5,
    decimal PositionFantasyPointsForAverageLast3,
    decimal PositionFantasyPointsForAverageLast5,
    decimal PositionFantasyPointsAgainstAverageLast3,
    decimal PositionFantasyPointsAgainstAverageLast5,
    decimal PositionScoutOffensiveConcededAverageLast3,
    decimal PositionScoutOffensiveConcededAverageLast5,
    decimal PositionScoutDefensiveConcededAverageLast3,
    decimal PositionScoutDefensiveConcededAverageLast5,
    decimal PositionScoutNegativeConcededAverageLast3,
    decimal PositionScoutNegativeConcededAverageLast5,
    decimal PositionScoutBalanceConcededAverageLast3,
    decimal PositionScoutBalanceConcededAverageLast5,
    decimal PositionScoutGoalsConcededAverageLast3,
    decimal PositionScoutGoalsConcededAverageLast5,
    decimal PositionScoutAssistsConcededAverageLast3,
    decimal PositionScoutAssistsConcededAverageLast5,
    decimal PositionScoutTacklesConcededAverageLast3,
    decimal PositionScoutTacklesConcededAverageLast5,
    decimal ExpectedPointsLast3,
    decimal WinRateLast5,
    decimal DrawRateLast5,
    decimal LossRateLast5,
    decimal ExpectedPointsLast5,
    decimal AttackStrengthLast3,
    decimal AttackStrengthLast5,
    decimal DefenseStrengthLast3,
    decimal DefenseStrengthLast5);

internal static class FullContextHistoricalFeatureCalculator
{
    internal static AthleteHistorySnapshot BuildAthleteHistorySnapshot(
        IReadOnlyDictionary<int, IReadOnlyList<AthleteHistoryObservation>> historyByAthleteId,
        IReadOnlyDictionary<int, IReadOnlyList<AthleteHistoryObservation>> positionHistoryByPositionId,
        IReadOnlyList<AthleteHistoryObservation> globalAthleteHistory,
        IReadOnlyDictionary<(int PositionId, int Round), decimal> averagePriceByPositionAndRound,
        int athleteId,
        int currentStatusId,
        decimal currentPrice,
        int positionId,
        int targetRound,
        bool targetIsHomeMatch)
    {
        if (!historyByAthleteId.TryGetValue(athleteId, out var observations))
        {
            return default;
        }

        var prior = observations
            .Where(item => item.RoundId < targetRound)
            .OrderByDescending(item => item.RoundId)
            .ToList();
        var playedPrior = prior
            .Where(item => item.WasInGame)
            .ToList();
        var playedPriorHome = playedPrior
            .Where(item => item.IsHomeMatch is true)
            .ToList();
        var playedPriorAway = playedPrior
            .Where(item => item.IsHomeMatch is false)
            .ToList();
        var playedPriorVenue = playedPrior
            .Where(item => item.IsHomeMatch == targetIsHomeMatch)
            .ToList();
        var playedPriorLast5 = playedPrior
            .Take(5)
            .ToList();

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
            GamesNumPrior: playedPrior.Count,
            HomeGamesNumPrior: playedPriorHome.Count,
            AwayGamesNumPrior: playedPriorAway.Count,
            VenueGamesNumPrior: playedPriorVenue.Count,
            PlayedMatchesSampleSize: playedPrior.Count,
            PlayedMatchesSampleSizeLast5: playedPriorLast5.Count,
            ObservationCountLast3: last3.Count,
            ObservationCountLast5: last5.Count,
            PreviousObservationCountLast3: previous3.Count,
            HomeObservationCountLast5: homeLast5.Count,
            AwayObservationCountLast5: awayLast5.Count,
            VenueObservationCountLast5: venueLast5.Count,
            StatusMatchedObservationCountLast5: statusMatchedLast5.Count,
            HistoryCoverageLast3: Math.Min(1m, last3.Count / 3m),
            HistoryCoverageLast5: Math.Min(1m, last5.Count / 5m),
            HomeHistoryCoverageLast5: Math.Min(1m, homeLast5.Count / 5m),
            AwayHistoryCoverageLast5: Math.Min(1m, awayLast5.Count / 5m),
            VenueHistoryCoverageLast5: Math.Min(1m, venueLast5.Count / 5m),
            CurrentStatusCoverageLast5: Math.Min(1m, statusMatchedLast5.Count / 5m),
            IsColdStartAthlete: playedPrior.Count < 3 ? 1m : 0m,
            MovingAveragePrior: Ewma(playedPriorLast5.Select(item => item.Score)),
            AverageScorePrior: Average(playedPrior.Select(item => item.Score)),
            HomeAverageScorePrior: Average(playedPriorHome.Select(item => item.Score)),
            AwayAverageScorePrior: Average(playedPriorAway.Select(item => item.Score)),
            VenueAverageScorePrior: Average(playedPriorVenue.Select(item => item.Score)),
            ScoreLast1: last5.FirstOrDefault().Score,
            ScoreAverageLast3: Average(last3.Select(item => item.Score)),
            ScoreAverageLast5: Average(last5.Select(item => item.Score)),
            ScoreEwmaLast3: Ewma(last3.Select(item => item.Score)),
            ScoreEwmaLast5: Ewma(last5.Select(item => item.Score)),
            ScoreStdLast5: StandardDeviation(last5.Select(item => item.Score)),
            ScoreFloorLast5: Min(last5.Select(item => item.Score)),
            ScoreCeilingLast5: Max(last5.Select(item => item.Score)),
            ScoreHitRateOver3Last5: Rate(last5.Select(item => item.Score > 3m)),
            ScoreHitRateOver5Last5: Rate(last5.Select(item => item.Score > 5m)),
            ScoreHitRateOver8Last5: Rate(last5.Select(item => item.Score > 8m)),
            ScoreNegativeRateLast5: Rate(last5.Select(item => item.Score < 0m)),
            ScoreTrendLast3VsPrev3: Average(last3.Select(item => item.Score)) - Average(previous3.Select(item => item.Score)),
            HomeScoreAverageLast5: Average(homeLast5.Select(item => item.Score)),
            AwayScoreAverageLast5: Average(awayLast5.Select(item => item.Score)),
            VenueScoreAverageLast5: Average(venueLast5.Select(item => item.Score)),
            HomeScoreEwmaLast3: Ewma(homeLast3.Select(item => item.Score)),
            AwayScoreEwmaLast3: Ewma(awayLast3.Select(item => item.Score)),
            VenueScoreEwmaLast3: Ewma(venueLast3.Select(item => item.Score)),
            ScoutOffensiveSumLast3: Sum(scoutLast3.Select(item => item.OffensiveScoutScore)),
            ScoutOffensiveSumLast5: Sum(scoutLast5.Select(item => item.OffensiveScoutScore)),
            ScoutOffensiveAverageLast3: Average(scoutLast3.Select(item => item.OffensiveScoutScore)),
            ScoutOffensiveAverageLast5: Average(scoutLast5.Select(item => item.OffensiveScoutScore)),
            ScoutOffensiveEwmaLast3: Ewma(scoutLast3.Select(item => item.OffensiveScoutScore)),
            ScoutOffensiveEwmaLast5: Ewma(scoutLast5.Select(item => item.OffensiveScoutScore)),
            ScoutDefensiveSumLast3: Sum(scoutLast3.Select(item => item.DefensiveScoutScore)),
            ScoutDefensiveSumLast5: Sum(scoutLast5.Select(item => item.DefensiveScoutScore)),
            ScoutDefensiveAverageLast3: Average(scoutLast3.Select(item => item.DefensiveScoutScore)),
            ScoutDefensiveAverageLast5: Average(scoutLast5.Select(item => item.DefensiveScoutScore)),
            ScoutDefensiveEwmaLast3: Ewma(scoutLast3.Select(item => item.DefensiveScoutScore)),
            ScoutDefensiveEwmaLast5: Ewma(scoutLast5.Select(item => item.DefensiveScoutScore)),
            ScoutNegativeSumLast3: Sum(scoutLast3.Select(item => item.NegativeScoutScore)),
            ScoutNegativeSumLast5: Sum(scoutLast5.Select(item => item.NegativeScoutScore)),
            ScoutNegativeAverageLast3: Average(scoutLast3.Select(item => item.NegativeScoutScore)),
            ScoutNegativeAverageLast5: Average(scoutLast5.Select(item => item.NegativeScoutScore)),
            ScoutNegativeEwmaLast3: Ewma(scoutLast3.Select(item => item.NegativeScoutScore)),
            ScoutNegativeEwmaLast5: Ewma(scoutLast5.Select(item => item.NegativeScoutScore)),
            ScoutBalanceSumLast3: Sum(scoutLast3.Select(item => item.BalanceScoutScore)),
            ScoutBalanceSumLast5: Sum(scoutLast5.Select(item => item.BalanceScoutScore)),
            ScoutBalanceAverageLast3: Average(scoutLast3.Select(item => item.BalanceScoutScore)),
            ScoutBalanceAverageLast5: Average(scoutLast5.Select(item => item.BalanceScoutScore)),
            ScoutBalanceEwmaLast3: Ewma(scoutLast3.Select(item => item.BalanceScoutScore)),
            ScoutBalanceEwmaLast5: Ewma(scoutLast5.Select(item => item.BalanceScoutScore)),
            ScoutGoalsAverageLast3: Average(scoutLast3.Select(item => item.GoalsCount)),
            ScoutGoalsAverageLast5: Average(scoutLast5.Select(item => item.GoalsCount)),
            ScoutAssistsAverageLast3: Average(scoutLast3.Select(item => item.AssistsCount)),
            ScoutAssistsAverageLast5: Average(scoutLast5.Select(item => item.AssistsCount)),
            ScoutTacklesAverageLast3: Average(scoutLast3.Select(item => item.TacklesCount)),
            ScoutTacklesAverageLast5: Average(scoutLast5.Select(item => item.TacklesCount)),
            ScoutCleanSheetRateLast3: Rate(scoutLast3.Select(item => item.CleanSheetCount > 0m)),
            ScoutCleanSheetRateLast5: Rate(scoutLast5.Select(item => item.CleanSheetCount > 0m)),
            PlayRateLast5: Rate(last5.Select(item => item.WasInGame)),
            RoundsSinceLastInGame: roundsSinceLastInGame,
            StatusStabilityLast5: Rate(last5.Select(item => item.StatusId == currentStatusId)),
            StatusChangeFlag: prior.Count > 0 && prior[0].StatusId != currentStatusId ? 1m : 0m,
            EmpiricalPlayRateForCurrentStatus: empiricalPlayRateForCurrentStatus,
            PriceTrendLast3: priceTrendLast3,
            PriceVolatilityLast5: StandardDeviation(last5.Select(item => item.Price)),
            HistoricalPointsPerCartoletaLast3: Average(last3.Select(item => SafeDivide(item.Score, item.Price))),
            HistoricalPointsPerCartoletaLast5: Average(last5.Select(item => SafeDivide(item.Score, item.Price))),
            PositionRelativePriceSignal: currentPrice - positionAveragePriceLast5,
            PriceDeltaVsHistoricalAverage: currentPrice - historicalAveragePrice);

        var priorSnapshot = BuildAthleteHistoryPrior(
            positionHistoryByPositionId,
            globalAthleteHistory,
            averagePriceByPositionAndRound,
            currentStatusId,
            currentPrice,
            positionId,
            targetRound,
            targetIsHomeMatch);

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
        IReadOnlyDictionary<int, IReadOnlyList<AthleteHistoryObservation>> positionHistoryByPositionId,
        IReadOnlyList<AthleteHistoryObservation> globalAthleteHistory,
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
            ? CreateAthletePriorFromObservations(positionObservations, averagePriceByPositionAndRound, currentStatusId, currentPrice, positionId, targetRound, targetIsHomeMatch)
            : CreateAthletePriorFromObservations(globalObservations, averagePriceByPositionAndRound, currentStatusId, currentPrice, positionId, targetRound, targetIsHomeMatch);
    }

    private static AthleteHistoryPrior CreateAthletePriorFromObservations(
        IReadOnlyList<AthleteHistoryObservation> observations,
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
                GamesNumPrior: played.Count,
                HomeGamesNumPrior: playedHome.Count,
                AwayGamesNumPrior: playedAway.Count,
                VenueGamesNumPrior: playedVenue.Count,
                PlayedMatchesSampleSize: played.Count,
                PlayedMatchesSampleSizeLast5: playedLast5.Count,
                ObservationCountLast3: last3.Count,
                ObservationCountLast5: last5.Count,
                PreviousObservationCountLast3: previous3.Count,
                HomeObservationCountLast5: homeLast5.Count,
                AwayObservationCountLast5: awayLast5.Count,
                VenueObservationCountLast5: venueLast5.Count,
                StatusMatchedObservationCountLast5: statusMatchedLast5.Count,
                HistoryCoverageLast3: Math.Min(1m, last3.Count / 3m),
                HistoryCoverageLast5: Math.Min(1m, last5.Count / 5m),
                HomeHistoryCoverageLast5: Math.Min(1m, homeLast5.Count / 5m),
                AwayHistoryCoverageLast5: Math.Min(1m, awayLast5.Count / 5m),
                VenueHistoryCoverageLast5: Math.Min(1m, venueLast5.Count / 5m),
                CurrentStatusCoverageLast5: Math.Min(1m, statusMatchedLast5.Count / 5m),
                IsColdStartAthlete: played.Count < 3 ? 1m : 0m,
                MovingAveragePrior: Ewma(playedLast5.Select(item => item.Score)),
                AverageScorePrior: Average(played.Select(item => item.Score)),
                HomeAverageScorePrior: Average(playedHome.Select(item => item.Score)),
                AwayAverageScorePrior: Average(playedAway.Select(item => item.Score)),
                VenueAverageScorePrior: Average(playedVenue.Select(item => item.Score)),
                ScoreLast1: last5.FirstOrDefault().Score,
                ScoreAverageLast3: Average(last3.Select(item => item.Score)),
                ScoreAverageLast5: Average(last5.Select(item => item.Score)),
                ScoreEwmaLast3: Ewma(last3.Select(item => item.Score)),
                ScoreEwmaLast5: Ewma(last5.Select(item => item.Score)),
                ScoreStdLast5: StandardDeviation(last5.Select(item => item.Score)),
                ScoreFloorLast5: Min(last5.Select(item => item.Score)),
                ScoreCeilingLast5: Max(last5.Select(item => item.Score)),
                ScoreHitRateOver3Last5: Rate(last5.Select(item => item.Score > 3m)),
                ScoreHitRateOver5Last5: Rate(last5.Select(item => item.Score > 5m)),
                ScoreHitRateOver8Last5: Rate(last5.Select(item => item.Score > 8m)),
                ScoreNegativeRateLast5: Rate(last5.Select(item => item.Score < 0m)),
                ScoreTrendLast3VsPrev3: Average(last3.Select(item => item.Score)) - Average(previous3.Select(item => item.Score)),
                HomeScoreAverageLast5: Average(homeLast5.Select(item => item.Score)),
                AwayScoreAverageLast5: Average(awayLast5.Select(item => item.Score)),
                VenueScoreAverageLast5: Average(venueLast5.Select(item => item.Score)),
                HomeScoreEwmaLast3: Ewma(homeLast3.Select(item => item.Score)),
                AwayScoreEwmaLast3: Ewma(awayLast3.Select(item => item.Score)),
                VenueScoreEwmaLast3: Ewma(venueLast3.Select(item => item.Score)),
                ScoutOffensiveSumLast3: Sum(last3.Select(item => item.OffensiveScoutScore)),
                ScoutOffensiveSumLast5: Sum(last5.Select(item => item.OffensiveScoutScore)),
                ScoutOffensiveAverageLast3: Average(last3.Select(item => item.OffensiveScoutScore)),
                ScoutOffensiveAverageLast5: Average(last5.Select(item => item.OffensiveScoutScore)),
                ScoutOffensiveEwmaLast3: Ewma(last3.Select(item => item.OffensiveScoutScore)),
                ScoutOffensiveEwmaLast5: Ewma(last5.Select(item => item.OffensiveScoutScore)),
                ScoutDefensiveSumLast3: Sum(last3.Select(item => item.DefensiveScoutScore)),
                ScoutDefensiveSumLast5: Sum(last5.Select(item => item.DefensiveScoutScore)),
                ScoutDefensiveAverageLast3: Average(last3.Select(item => item.DefensiveScoutScore)),
                ScoutDefensiveAverageLast5: Average(last5.Select(item => item.DefensiveScoutScore)),
                ScoutDefensiveEwmaLast3: Ewma(last3.Select(item => item.DefensiveScoutScore)),
                ScoutDefensiveEwmaLast5: Ewma(last5.Select(item => item.DefensiveScoutScore)),
                ScoutNegativeSumLast3: Sum(last3.Select(item => item.NegativeScoutScore)),
                ScoutNegativeSumLast5: Sum(last5.Select(item => item.NegativeScoutScore)),
                ScoutNegativeAverageLast3: Average(last3.Select(item => item.NegativeScoutScore)),
                ScoutNegativeAverageLast5: Average(last5.Select(item => item.NegativeScoutScore)),
                ScoutNegativeEwmaLast3: Ewma(last3.Select(item => item.NegativeScoutScore)),
                ScoutNegativeEwmaLast5: Ewma(last5.Select(item => item.NegativeScoutScore)),
                ScoutBalanceSumLast3: Sum(last3.Select(item => item.BalanceScoutScore)),
                ScoutBalanceSumLast5: Sum(last5.Select(item => item.BalanceScoutScore)),
                ScoutBalanceAverageLast3: Average(last3.Select(item => item.BalanceScoutScore)),
                ScoutBalanceAverageLast5: Average(last5.Select(item => item.BalanceScoutScore)),
                ScoutBalanceEwmaLast3: Ewma(last3.Select(item => item.BalanceScoutScore)),
                ScoutBalanceEwmaLast5: Ewma(last5.Select(item => item.BalanceScoutScore)),
                ScoutGoalsAverageLast3: Average(last3.Select(item => item.GoalsCount)),
                ScoutGoalsAverageLast5: Average(last5.Select(item => item.GoalsCount)),
                ScoutAssistsAverageLast3: Average(last3.Select(item => item.AssistsCount)),
                ScoutAssistsAverageLast5: Average(last5.Select(item => item.AssistsCount)),
                ScoutTacklesAverageLast3: Average(last3.Select(item => item.TacklesCount)),
                ScoutTacklesAverageLast5: Average(last5.Select(item => item.TacklesCount)),
                ScoutCleanSheetRateLast3: Rate(last3.Select(item => item.CleanSheetCount > 0m)),
                ScoutCleanSheetRateLast5: Rate(last5.Select(item => item.CleanSheetCount > 0m)),
                PlayRateLast5: Rate(last5.Select(item => item.WasInGame)),
                RoundsSinceLastInGame: roundsSinceLastInGame,
                StatusStabilityLast5: Rate(last5.Select(item => item.StatusId == currentStatusId)),
                StatusChangeFlag: observations.Count > 0 && observations[0].StatusId != currentStatusId ? 1m : 0m,
                EmpiricalPlayRateForCurrentStatus: empiricalPlayRateForCurrentStatus,
                PriceTrendLast3: ComputePriceTrendLast3(last3.Select(item => item.Price)),
                PriceVolatilityLast5: StandardDeviation(last5.Select(item => item.Price)),
                HistoricalPointsPerCartoletaLast3: Average(last3.Select(item => SafeDivide(item.Score, item.Price))),
                HistoricalPointsPerCartoletaLast5: Average(last5.Select(item => SafeDivide(item.Score, item.Price))),
                PositionRelativePriceSignal: currentPrice - positionAveragePriceLast5,
                PriceDeltaVsHistoricalAverage: currentPrice - historicalAveragePrice),
            historicalAveragePrice,
            played.Count,
            playedLast5.Count,
            playedHome.Count,
            playedAway.Count,
            playedVenue.Count,
            last3.Count,
            last5.Count,
            previous3.Count,
            homeLast5.Count,
            awayLast5.Count,
            venueLast5.Count,
            homeLast3.Count,
            awayLast3.Count,
            venueLast3.Count,
            statusMatchedLast5.Count);
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

    internal static LocalClubContextSnapshot BuildLocalClubContextSnapshot(
        IReadOnlyDictionary<int, IReadOnlyList<ClubMatchHistoryPoint>> clubHistoryByClubId,
        IReadOnlyDictionary<(int ClubId, int Round), decimal> fantasyPointsForByClubAndRound,
        IReadOnlyDictionary<(int ClubId, int Round), decimal> fantasyPointsAgainstByClubAndRound,
        IReadOnlyDictionary<(int ClubId, int Round), decimal> offensiveScoutsConcededByClubAndRound,
        IReadOnlyDictionary<(int ClubId, int Round), decimal> defensiveScoutsConcededByClubAndRound,
        IReadOnlyDictionary<(int ClubId, int Round), decimal> negativeScoutsConcededByClubAndRound,
        IReadOnlyDictionary<(int ClubId, int Round), decimal> balanceScoutsConcededByClubAndRound,
        IReadOnlyDictionary<(int ClubId, int Round), decimal> goalsScoutsConcededByClubAndRound,
        IReadOnlyDictionary<(int ClubId, int Round), decimal> assistsScoutsConcededByClubAndRound,
        IReadOnlyDictionary<(int ClubId, int Round), decimal> tacklesScoutsConcededByClubAndRound,
        IReadOnlyDictionary<(int ClubId, int PositionId, int Round), decimal> positionFantasyPointsForByClubPositionAndRound,
        IReadOnlyDictionary<(int ClubId, int PositionId, int Round), decimal> positionFantasyPointsAgainstByClubPositionAndRound,
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

        var last5 = history
            .Where(item => item.RoundId < targetRound)
            .OrderByDescending(item => item.RoundId)
            .Take(5)
            .ToList();
        var last3 = last5.Take(3).ToList();

        var venueLast5 = history
            .Where(item => item.RoundId < targetRound && item.IsHomeMatch == targetIsHomeMatch)
            .OrderByDescending(item => item.RoundId)
            .Take(5)
            .ToList();
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
        var fantasyPointsForAverageLast3 = Average(last3.Select(item => Lookup(fantasyPointsForByClubAndRound, (clubId, item.RoundId))));
        var fantasyPointsForAverageLast5 = Average(last5.Select(item => Lookup(fantasyPointsForByClubAndRound, (clubId, item.RoundId))));
        var fantasyPointsAgainstAverageLast3 = Average(last3.Select(item => Lookup(fantasyPointsAgainstByClubAndRound, (clubId, item.RoundId))));
        var fantasyPointsAgainstAverageLast5 = Average(last5.Select(item => Lookup(fantasyPointsAgainstByClubAndRound, (clubId, item.RoundId))));
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
        var positionFantasyPointsForAverageLast3 = Average(last3.Select(item => Lookup(positionFantasyPointsForByClubPositionAndRound, (clubId, positionId, item.RoundId))));
        var positionFantasyPointsForAverageLast5 = Average(last5.Select(item => Lookup(positionFantasyPointsForByClubPositionAndRound, (clubId, positionId, item.RoundId))));
        var positionFantasyPointsAgainstAverageLast3 = Average(last3.Select(item => Lookup(positionFantasyPointsAgainstByClubPositionAndRound, (clubId, positionId, item.RoundId))));
        var positionFantasyPointsAgainstAverageLast5 = Average(last5.Select(item => Lookup(positionFantasyPointsAgainstByClubPositionAndRound, (clubId, positionId, item.RoundId))));
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

        return new LocalClubContextSnapshot(
            MatchesSampleSize: last5.Count,
            MatchesSampleSizeLast3: last3.Count,
            MatchesSampleSizeLast5: last5.Count,
            PointsPerMatchLast3: pointsPerMatchLast3,
            PointsPerMatchLast5: pointsPerMatchLast5,
            VenuePointsPerMatchLast3: AverageOrFallback(venueLast3.Select(item => item.Points), last3.Select(item => item.Points)),
            VenuePointsPerMatchLast5: AverageOrFallback(venueLast5.Select(item => item.Points), last5.Select(item => item.Points)),
            GoalsForPerMatchLast3: goalsForPerMatchLast3,
            GoalsForPerMatchLast5: goalsForPerMatchLast5,
            GoalsAgainstPerMatchLast3: goalsAgainstPerMatchLast3,
            GoalsAgainstPerMatchLast5: goalsAgainstPerMatchLast5,
            GoalDifferencePerMatchLast3: goalDifferencePerMatchLast3,
            GoalDifferencePerMatchLast5: goalDifferencePerMatchLast5,
            VenueGoalsForPerMatchLast3: AverageOrFallback(venueLast3.Select(item => item.GoalsFor), last3.Select(item => item.GoalsFor)),
            VenueGoalsForPerMatchLast5: AverageOrFallback(venueLast5.Select(item => item.GoalsFor), last5.Select(item => item.GoalsFor)),
            VenueGoalsAgainstPerMatchLast3: AverageOrFallback(venueLast3.Select(item => item.GoalsAgainst), last3.Select(item => item.GoalsAgainst)),
            VenueGoalsAgainstPerMatchLast5: AverageOrFallback(venueLast5.Select(item => item.GoalsAgainst), last5.Select(item => item.GoalsAgainst)),
            CleanSheetRateLast3: cleanSheetRateLast3,
            CleanSheetRateLast5: cleanSheetRateLast5,
            FailedToScoreRateLast3: failedToScoreRateLast3,
            FailedToScoreRateLast5: failedToScoreRateLast5,
            FantasyPointsForAverageLast3: fantasyPointsForAverageLast3,
            FantasyPointsForAverageLast5: fantasyPointsForAverageLast5,
            FantasyPointsAgainstAverageLast3: fantasyPointsAgainstAverageLast3,
            FantasyPointsAgainstAverageLast5: fantasyPointsAgainstAverageLast5,
            ScoutOffensiveConcededAverageLast3: scoutOffensiveConcededAverageLast3,
            ScoutOffensiveConcededAverageLast5: scoutOffensiveConcededAverageLast5,
            ScoutDefensiveConcededAverageLast3: scoutDefensiveConcededAverageLast3,
            ScoutDefensiveConcededAverageLast5: scoutDefensiveConcededAverageLast5,
            ScoutNegativeConcededAverageLast3: scoutNegativeConcededAverageLast3,
            ScoutNegativeConcededAverageLast5: scoutNegativeConcededAverageLast5,
            ScoutBalanceConcededAverageLast3: scoutBalanceConcededAverageLast3,
            ScoutBalanceConcededAverageLast5: scoutBalanceConcededAverageLast5,
            ScoutGoalsConcededAverageLast3: scoutGoalsConcededAverageLast3,
            ScoutGoalsConcededAverageLast5: scoutGoalsConcededAverageLast5,
            ScoutAssistsConcededAverageLast3: scoutAssistsConcededAverageLast3,
            ScoutAssistsConcededAverageLast5: scoutAssistsConcededAverageLast5,
            ScoutTacklesConcededAverageLast3: scoutTacklesConcededAverageLast3,
            ScoutTacklesConcededAverageLast5: scoutTacklesConcededAverageLast5,
            PositionFantasyPointsForAverageLast3: positionFantasyPointsForAverageLast3,
            PositionFantasyPointsForAverageLast5: positionFantasyPointsForAverageLast5,
            PositionFantasyPointsAgainstAverageLast3: positionFantasyPointsAgainstAverageLast3,
            PositionFantasyPointsAgainstAverageLast5: positionFantasyPointsAgainstAverageLast5,
            PositionScoutOffensiveConcededAverageLast3: positionScoutOffensiveConcededAverageLast3,
            PositionScoutOffensiveConcededAverageLast5: positionScoutOffensiveConcededAverageLast5,
            PositionScoutDefensiveConcededAverageLast3: positionScoutDefensiveConcededAverageLast3,
            PositionScoutDefensiveConcededAverageLast5: positionScoutDefensiveConcededAverageLast5,
            PositionScoutNegativeConcededAverageLast3: positionScoutNegativeConcededAverageLast3,
            PositionScoutNegativeConcededAverageLast5: positionScoutNegativeConcededAverageLast5,
            PositionScoutBalanceConcededAverageLast3: positionScoutBalanceConcededAverageLast3,
            PositionScoutBalanceConcededAverageLast5: positionScoutBalanceConcededAverageLast5,
            PositionScoutGoalsConcededAverageLast3: positionScoutGoalsConcededAverageLast3,
            PositionScoutGoalsConcededAverageLast5: positionScoutGoalsConcededAverageLast5,
            PositionScoutAssistsConcededAverageLast3: positionScoutAssistsConcededAverageLast3,
            PositionScoutAssistsConcededAverageLast5: positionScoutAssistsConcededAverageLast5,
            PositionScoutTacklesConcededAverageLast3: positionScoutTacklesConcededAverageLast3,
            PositionScoutTacklesConcededAverageLast5: positionScoutTacklesConcededAverageLast5,
            ExpectedPointsLast3: expectedPointsLast3,
            WinRateLast5: Rate(last5.Select(item => item.Points == 3m)),
            DrawRateLast5: Rate(last5.Select(item => item.Points == 1m)),
            LossRateLast5: Rate(last5.Select(item => item.Points == 0m)),
            ExpectedPointsLast5: expectedPointsLast5,
            AttackStrengthLast3: ComputeAttackStrength(goalsForPerMatchLast3, fantasyPointsForAverageLast3, positionFantasyPointsForAverageLast3),
            AttackStrengthLast5: ComputeAttackStrength(goalsForPerMatchLast5, fantasyPointsForAverageLast5, positionFantasyPointsForAverageLast5),
            DefenseStrengthLast3: ComputeDefenseStrength(cleanSheetRateLast3, goalsAgainstPerMatchLast3, fantasyPointsAgainstAverageLast3, positionFantasyPointsAgainstAverageLast3),
            DefenseStrengthLast5: ComputeDefenseStrength(cleanSheetRateLast5, goalsAgainstPerMatchLast5, fantasyPointsAgainstAverageLast5, positionFantasyPointsAgainstAverageLast5));
    }

    private static decimal Lookup<TKey>(IReadOnlyDictionary<TKey, decimal> dictionary, TKey key)
        where TKey : notnull
        => dictionary.TryGetValue(key, out var value) ? value : 0m;

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

    private static decimal ComputePriceTrendLast3(IEnumerable<decimal> values)
    {
        var materialized = values.ToList();
        if (materialized.Count < 2)
        {
            return 0m;
        }

        return materialized[0] - Average(materialized.Skip(1));
    }

    // Valores maiores significam um ataque recente mais forte, baseado em gols mais o desempenho no fantasy.
    private static decimal ComputeAttackStrength(decimal goalsForPerMatch, decimal fantasyPointsForAverage, decimal positionFantasyPointsForAverage)
        => goalsForPerMatch + (fantasyPointsForAverage / 10m) + (positionFantasyPointsForAverage / 10m);

    // Valores maiores significam uma defesa recente mais forte, baseada em jogos sem sofrer gols, gols sofridos e pontos de fantasy cedidos.
    private static decimal ComputeDefenseStrength(decimal cleanSheetRate, decimal goalsAgainstPerMatch, decimal fantasyPointsAgainstAverage, decimal positionFantasyPointsAgainstAverage)
        => (2m * cleanSheetRate) - goalsAgainstPerMatch - (fantasyPointsAgainstAverage / 10m) - (positionFantasyPointsAgainstAverage / 10m);

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
        if (materialized.Count == 0)
        {
            return 0m;
        }

        return (decimal)materialized.Count(item => item) / materialized.Count;
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
}
