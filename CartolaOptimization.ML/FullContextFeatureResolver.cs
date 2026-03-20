using CartolaOptimization.Domain.Entities;
using CartolaOptimization.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CartolaOptimization.ML;

internal static class FullContextFeatureResolver
{
    internal static async Task<Dictionary<(int Round, int ClubId), ClubRound>> LoadCompleteClubRoundsAsync(
        AppDbContext dbContext,
        IReadOnlyCollection<int> rounds,
        CancellationToken cancellationToken)
    {
        var clubRounds = await dbContext.ClubRounds
            .AsNoTracking()
            .Include(item => item.Club)
            .Include(item => item.PositionStats)
            .Where(item => rounds.Contains(item.Round))
            .ToListAsync(cancellationToken);

        return clubRounds
            .Where(item => item.PositionStats.Count > 0)
            .ToDictionary(item => (item.Round, item.ClubId));
    }

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
        IReadOnlyDictionary<(int Round, int ClubId), ClubRound> completeClubRounds,
        IReadOnlyDictionary<(int Round, int ClubId), Match> matchByRoundAndClubId,
        out FullContextAthleteScoreTrainingRow row)
    {
        row = null!;

        if (!TryResolveContext(
                athleteRound.RoundId,
                athleteRound,
                completeClubRounds,
                matchByRoundAndClubId,
                out var context))
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
            MovingAverage = (float)athleteRound.MovingAverage,
            AverageScore = (float)athleteRound.AverageScoreNum,
            HomeAverageScore = (float)athleteRound.HomeAverageScore,
            AwayAverageScore = (float)athleteRound.AwayAverageScore,
            PriceNum = (float)athleteRound.PriceNum,
            VariationNum = (float)athleteRound.VariationNum,
            IsHomeMatch = context.IsHomeMatch ? 1f : 0f,
            ClubTablePosition = context.ClubTablePosition,
            OpponentClubTablePosition = context.OpponentClubTablePosition,
            ClubRecentForm = context.ClubRecentForm,
            OpponentRecentForm = context.OpponentRecentForm,
            ClubWinProbability = (float)context.ClubRound.WinProbability,
            ClubDrawProbability = (float)context.ClubRound.DrawProbability,
            ClubLoseProbability = (float)context.ClubRound.LoseProbability,
            ClubOverallAverage = (float)context.ClubRound.OverallAverage,
            ClubMovingAverage = (float)context.ClubRound.MovingAverage,
            ClubExpectedPoints = (float)context.ClubRound.ExpectedPoints,
            ClubPositionPointsConcededAverage = (float)context.ClubPositionStats.PointsConcededAverage,
            ClubPositionPointsConqueredAverage = (float)context.ClubPositionStats.PointsConqueredAverage,
            OpponentWinProbability = (float)context.OpponentClubRound.WinProbability,
            OpponentDrawProbability = (float)context.OpponentClubRound.DrawProbability,
            OpponentLoseProbability = (float)context.OpponentClubRound.LoseProbability,
            OpponentOverallAverage = (float)context.OpponentClubRound.OverallAverage,
            OpponentMovingAverage = (float)context.OpponentClubRound.MovingAverage,
            OpponentExpectedPoints = (float)context.OpponentClubRound.ExpectedPoints,
            OpponentPositionPointsConcededAverage = (float)context.OpponentPositionStats.PointsConcededAverage,
            OpponentPositionPointsConqueredAverage = (float)context.OpponentPositionStats.PointsConqueredAverage,
            Label = (float)athleteRound.ScoreNum,
        };

        return true;
    }

    internal static bool TryBuildPredictionCandidate(
        int round,
        AthleteRound baseline,
        IReadOnlyDictionary<(int Round, int ClubId), ClubRound> completeClubRounds,
        IReadOnlyDictionary<(int Round, int ClubId), Match> matchByRoundAndClubId,
        out FullContextPredictionCandidate candidate)
    {
        candidate = null!;

        if (!TryResolveContext(
                round,
                baseline,
                completeClubRounds,
                matchByRoundAndClubId,
                out var context))
        {
            return false;
        }

        candidate = new FullContextPredictionCandidate
        {
            Baseline = baseline,
            Input = new FullContextAthleteScorePredictionInput
            {
                RoundId = round,
                ClubCartolaId = baseline.Club.CartolaId,
                OpponentClubCartolaId = context.OpponentClubCartolaId,
                PositionCartolaId = baseline.Position.CartolaId,
                StatusCartolaId = baseline.Status.CartolaId,
                GamesNum = baseline.GamesNum,
                HomeGamesNum = baseline.HomeGamesNum,
                AwayGamesNum = baseline.AwayGamesNum,
                MovingAverage = (float)baseline.MovingAverage,
                AverageScore = (float)baseline.AverageScoreNum,
                HomeAverageScore = (float)baseline.HomeAverageScore,
                AwayAverageScore = (float)baseline.AwayAverageScore,
                PriceNum = (float)baseline.PriceNum,
                VariationNum = (float)baseline.VariationNum,
                IsHomeMatch = context.IsHomeMatch ? 1f : 0f,
                ClubTablePosition = context.ClubTablePosition,
                OpponentClubTablePosition = context.OpponentClubTablePosition,
                ClubRecentForm = context.ClubRecentForm,
                OpponentRecentForm = context.OpponentRecentForm,
                ClubWinProbability = (float)context.ClubRound.WinProbability,
                ClubDrawProbability = (float)context.ClubRound.DrawProbability,
                ClubLoseProbability = (float)context.ClubRound.LoseProbability,
                ClubOverallAverage = (float)context.ClubRound.OverallAverage,
                ClubMovingAverage = (float)context.ClubRound.MovingAverage,
                ClubExpectedPoints = (float)context.ClubRound.ExpectedPoints,
                ClubPositionPointsConcededAverage = (float)context.ClubPositionStats.PointsConcededAverage,
                ClubPositionPointsConqueredAverage = (float)context.ClubPositionStats.PointsConqueredAverage,
                OpponentWinProbability = (float)context.OpponentClubRound.WinProbability,
                OpponentDrawProbability = (float)context.OpponentClubRound.DrawProbability,
                OpponentLoseProbability = (float)context.OpponentClubRound.LoseProbability,
                OpponentOverallAverage = (float)context.OpponentClubRound.OverallAverage,
                OpponentMovingAverage = (float)context.OpponentClubRound.MovingAverage,
                OpponentExpectedPoints = (float)context.OpponentClubRound.ExpectedPoints,
                OpponentPositionPointsConcededAverage = (float)context.OpponentPositionStats.PointsConcededAverage,
                OpponentPositionPointsConqueredAverage = (float)context.OpponentPositionStats.PointsConqueredAverage,
            },
            IsHomeMatch = context.IsHomeMatch,
            ClubTablePosition = context.ClubTablePosition,
            OpponentClubCartolaId = context.OpponentClubCartolaId,
            OpponentClubName = context.OpponentClubName,
            OpponentClubTablePosition = context.OpponentClubTablePosition,
            ClubWinProbability = context.ClubRound.WinProbability,
            ClubExpectedPoints = context.ClubRound.ExpectedPoints,
            OpponentWinProbability = context.OpponentClubRound.WinProbability,
            OpponentExpectedPoints = context.OpponentClubRound.ExpectedPoints,
        };

        return true;
    }

    private static bool TryResolveContext(
        int targetRound,
        AthleteRound athleteRound,
        IReadOnlyDictionary<(int Round, int ClubId), ClubRound> completeClubRounds,
        IReadOnlyDictionary<(int Round, int ClubId), Match> matchByRoundAndClubId,
        out ResolvedFullContext context)
    {
        context = default;

        if (!completeClubRounds.TryGetValue((targetRound, athleteRound.ClubId), out var clubRound))
        {
            return false;
        }

        if (!matchByRoundAndClubId.TryGetValue((targetRound, athleteRound.ClubId), out var match))
        {
            return false;
        }

        var isHomeMatch = match.HomeClubId == athleteRound.ClubId;
        var opponentClubId = isHomeMatch ? match.AwayClubId : match.HomeClubId;

        if (!completeClubRounds.TryGetValue((targetRound, opponentClubId), out var opponentClubRound))
        {
            return false;
        }

        var clubPositionStats = clubRound.PositionStats.FirstOrDefault(item => item.PositionId == athleteRound.PositionId);
        var opponentPositionStats = opponentClubRound.PositionStats.FirstOrDefault(item => item.PositionId == athleteRound.PositionId);

        if (clubPositionStats is null || opponentPositionStats is null)
        {
            return false;
        }

        context = new ResolvedFullContext(
            clubRound,
            opponentClubRound,
            clubPositionStats,
            opponentPositionStats,
            isHomeMatch,
            isHomeMatch ? match.HomeClubPosition : match.AwayClubPosition,
            isHomeMatch ? match.AwayClubPosition : match.HomeClubPosition,
            isHomeMatch ? match.HomeForm : match.AwayForm,
            isHomeMatch ? match.AwayForm : match.HomeForm,
            isHomeMatch ? match.AwayClub.CartolaId : match.HomeClub.CartolaId,
            isHomeMatch ? match.AwayClub.Nickname : match.HomeClub.Nickname);

        return true;
    }

    private readonly record struct ResolvedFullContext(
        ClubRound ClubRound,
        ClubRound OpponentClubRound,
        ClubRoundPosition ClubPositionStats,
        ClubRoundPosition OpponentPositionStats,
        bool IsHomeMatch,
        int ClubTablePosition,
        int OpponentClubTablePosition,
        string ClubRecentForm,
        string OpponentRecentForm,
        int OpponentClubCartolaId,
        string OpponentClubName);
}
