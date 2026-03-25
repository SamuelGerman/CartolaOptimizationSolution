using CartolaOptimization.Domain.Entities;
using CartolaOptimization.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CartolaOptimization.ML;

internal static class AthleteContextFeatureResolver
{
    internal static async Task<Dictionary<(int Round, int ClubId), Match>> LoadMatchesByRoundAndClubIdAsync(
        AppDbContext dbContext,
        IReadOnlyCollection<int> rounds,
        CancellationToken cancellationToken)
    {
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

        return matchByRoundAndClubId;
    }

    internal static bool TryBuildTrainingRow(
        AthleteRound athleteRound,
        IReadOnlyDictionary<(int Round, int ClubId), Match> matchByRoundAndClubId,
        out AthleteContextAthleteScoreTrainingRow row)
    {
        row = null!;

        if (!TryResolveMatchContext(athleteRound.RoundId, athleteRound, matchByRoundAndClubId, out var context))
        {
            return false;
        }

        row = new AthleteContextAthleteScoreTrainingRow
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
            TeamVenueAverageCurrent = context.IsHomeMatch
                ? (float)(athleteRound.Context?.TeamHomeAverageCurrent ?? 0m)
                : (float)(athleteRound.Context?.TeamAwayAverageCurrent ?? 0m),
            TeamVenueAveragePrevious = context.IsHomeMatch
                ? (float)(athleteRound.Context?.TeamHomeAveragePrevious ?? 0m)
                : (float)(athleteRound.Context?.TeamAwayAveragePrevious ?? 0m),
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
            Label = (float)athleteRound.ScoreNum,
        };

        return true;
    }

    internal static bool TryBuildPredictionCandidate(
        int round,
        AthleteRound baseline,
        IReadOnlyDictionary<(int Round, int ClubId), Match> matchByRoundAndClubId,
        out AthleteContextPredictionCandidate candidate)
    {
        candidate = null!;

        if (!TryResolveMatchContext(round, baseline, matchByRoundAndClubId, out var context))
        {
            return false;
        }

        candidate = new AthleteContextPredictionCandidate
        {
            Baseline = baseline,
            Input = new AthleteContextAthleteScorePredictionInput
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
                TeamVenueAverageCurrent = context.IsHomeMatch
                    ? (float)(baseline.Context?.TeamHomeAverageCurrent ?? 0m)
                    : (float)(baseline.Context?.TeamAwayAverageCurrent ?? 0m),
                TeamVenueAveragePrevious = context.IsHomeMatch
                    ? (float)(baseline.Context?.TeamHomeAveragePrevious ?? 0m)
                    : (float)(baseline.Context?.TeamAwayAveragePrevious ?? 0m),
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
            },
            IsHomeMatch = context.IsHomeMatch,
            ClubTablePosition = context.ClubTablePosition,
            OpponentClubCartolaId = context.OpponentClubCartolaId,
            OpponentClubName = context.OpponentClubName,
            OpponentClubTablePosition = context.OpponentClubTablePosition,
        };

        return true;
    }

    private static bool TryResolveMatchContext(
        int targetRound,
        AthleteRound athleteRound,
        IReadOnlyDictionary<(int Round, int ClubId), Match> matchByRoundAndClubId,
        out ResolvedAthleteContext context)
    {
        context = default;

        if (!matchByRoundAndClubId.TryGetValue((targetRound, athleteRound.ClubId), out var match))
        {
            return false;
        }

        var isHomeMatch = match.HomeClubId == athleteRound.ClubId;
        var opponentClub = isHomeMatch ? match.AwayClub : match.HomeClub;
        var clubForm = isHomeMatch ? match.HomeForm : match.AwayForm;
        var opponentForm = isHomeMatch ? match.AwayForm : match.HomeForm;

        context = new ResolvedAthleteContext(
            isHomeMatch,
            isHomeMatch ? match.HomeClubPosition : match.AwayClubPosition,
            isHomeMatch ? match.AwayClubPosition : match.HomeClubPosition,
            opponentClub.CartolaId,
            opponentClub.Nickname,
            ConvertFormToPoints(clubForm),
            ConvertFormToPoints(opponentForm));

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

    private readonly record struct ResolvedAthleteContext(
        bool IsHomeMatch,
        int ClubTablePosition,
        int OpponentClubTablePosition,
        int OpponentClubCartolaId,
        string OpponentClubName,
        float ClubRecentFormPoints,
        float OpponentRecentFormPoints);
}
