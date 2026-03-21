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
            .Include(item => item.Context)
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
            TeamAverageScore = (float)(athleteRound.Context?.TeamAverageScore ?? 0m),
            PointsPerCartoletaCurrent = (float)(athleteRound.Context?.PointsPerCartoletaCurrent ?? 0m),
            GrossOverallMovingAverageCurrent = (float)(athleteRound.Context?.GrossOverallMovingAverageCurrent ?? 0m),
            NetOverallMovingAverageCurrent = (float)(athleteRound.Context?.NetOverallMovingAverageCurrent ?? 0m),
            ValuationCurrent = (float)(athleteRound.Context?.ValuationCurrent ?? 0m),
            MinimumToAppreciateCurrent = (float)(athleteRound.Context?.MinimumToAppreciateCurrent ?? 0m),
            IndexPointsPerCartoleta = (float)(athleteRound.Context?.IndexPointsPerCartoleta ?? 0m),
            IndexOffensiveEfficiency = (float)(athleteRound.Context?.IndexOffensiveEfficiency ?? 0m),
            IndexDefensiveEfficiency = (float)(athleteRound.Context?.IndexDefensiveEfficiency ?? 0m),
            IndexShotsPerGame = (float)(athleteRound.Context?.IndexShotsPerGame ?? 0m),
            IndexShotsPerGoal = (float)(athleteRound.Context?.IndexShotsPerGoal ?? 0m),
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
            ClubPreviousOverallAverage = (float)(context.ClubRound.Context?.PreviousOverallAverage ?? 0m),
            ClubRankingsHomeAverage = (float)(context.ClubRound.Context?.RankingsHomeAverage ?? 0m),
            ClubRankingsAwayAverage = (float)(context.ClubRound.Context?.RankingsAwayAverage ?? 0m),
            ClubHistoricalWinRate = (float)(context.ClubRound.Context?.HistoricalWinRate ?? 0m),
            ClubFavoriteWinProbability = (float)(context.ClubRound.Context?.FavoriteWinProbability ?? 0m),
            ClubForecastExpectedResult = context.ClubRound.Context?.ForecastExpectedResult ?? 0,
            ClubForecastRiskScore = MapRiskLevelToScore(context.ClubRound.Context?.ForecastRiskLevel),
            ClubPositionDistributionValue = (float)context.ClubPositionStats.DistributionValue,
            ClubAverageInsightConfidence = (float)(context.ClubRound.Context?.AverageInsightConfidence ?? 0m),
            ClubPositiveInsightNet = (context.ClubRound.Context?.PositiveInsightCount ?? 0) - (context.ClubRound.Context?.NegativeInsightCount ?? 0),
            ClubTeamTipsCount = context.ClubRound.Context?.TeamTipsCount ?? 0,
            ClubMatchTipsCount = context.ClubRound.Context?.MatchTipsCount ?? 0,
            ClubPositionPointsConcededAverage = (float)context.ClubPositionStats.PointsConcededAverage,
            ClubPositionPointsConqueredAverage = (float)context.ClubPositionStats.PointsConqueredAverage,
            OpponentWinProbability = (float)context.OpponentClubRound.WinProbability,
            OpponentDrawProbability = (float)context.OpponentClubRound.DrawProbability,
            OpponentLoseProbability = (float)context.OpponentClubRound.LoseProbability,
            OpponentOverallAverage = (float)context.OpponentClubRound.OverallAverage,
            OpponentMovingAverage = (float)context.OpponentClubRound.MovingAverage,
            OpponentExpectedPoints = (float)context.OpponentClubRound.ExpectedPoints,
            OpponentPreviousOverallAverage = (float)(context.OpponentClubRound.Context?.PreviousOverallAverage ?? 0m),
            OpponentRankingsHomeAverage = (float)(context.OpponentClubRound.Context?.RankingsHomeAverage ?? 0m),
            OpponentRankingsAwayAverage = (float)(context.OpponentClubRound.Context?.RankingsAwayAverage ?? 0m),
            OpponentHistoricalWinRate = (float)(context.OpponentClubRound.Context?.HistoricalWinRate ?? 0m),
            OpponentFavoriteWinProbability = (float)(context.OpponentClubRound.Context?.FavoriteWinProbability ?? 0m),
            OpponentForecastExpectedResult = context.OpponentClubRound.Context?.ForecastExpectedResult ?? 0,
            OpponentForecastRiskScore = MapRiskLevelToScore(context.OpponentClubRound.Context?.ForecastRiskLevel),
            OpponentPositionDistributionValue = (float)context.OpponentPositionStats.DistributionValue,
            OpponentAverageInsightConfidence = (float)(context.OpponentClubRound.Context?.AverageInsightConfidence ?? 0m),
            OpponentPositiveInsightNet = (context.OpponentClubRound.Context?.PositiveInsightCount ?? 0) - (context.OpponentClubRound.Context?.NegativeInsightCount ?? 0),
            OpponentTeamTipsCount = context.OpponentClubRound.Context?.TeamTipsCount ?? 0,
            OpponentMatchTipsCount = context.OpponentClubRound.Context?.MatchTipsCount ?? 0,
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
                TeamAverageScore = (float)(baseline.Context?.TeamAverageScore ?? 0m),
                PointsPerCartoletaCurrent = (float)(baseline.Context?.PointsPerCartoletaCurrent ?? 0m),
                GrossOverallMovingAverageCurrent = (float)(baseline.Context?.GrossOverallMovingAverageCurrent ?? 0m),
                NetOverallMovingAverageCurrent = (float)(baseline.Context?.NetOverallMovingAverageCurrent ?? 0m),
                ValuationCurrent = (float)(baseline.Context?.ValuationCurrent ?? 0m),
                MinimumToAppreciateCurrent = (float)(baseline.Context?.MinimumToAppreciateCurrent ?? 0m),
                IndexPointsPerCartoleta = (float)(baseline.Context?.IndexPointsPerCartoleta ?? 0m),
                IndexOffensiveEfficiency = (float)(baseline.Context?.IndexOffensiveEfficiency ?? 0m),
                IndexDefensiveEfficiency = (float)(baseline.Context?.IndexDefensiveEfficiency ?? 0m),
                IndexShotsPerGame = (float)(baseline.Context?.IndexShotsPerGame ?? 0m),
                IndexShotsPerGoal = (float)(baseline.Context?.IndexShotsPerGoal ?? 0m),
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
                ClubPreviousOverallAverage = (float)(context.ClubRound.Context?.PreviousOverallAverage ?? 0m),
                ClubRankingsHomeAverage = (float)(context.ClubRound.Context?.RankingsHomeAverage ?? 0m),
                ClubRankingsAwayAverage = (float)(context.ClubRound.Context?.RankingsAwayAverage ?? 0m),
                ClubHistoricalWinRate = (float)(context.ClubRound.Context?.HistoricalWinRate ?? 0m),
                ClubFavoriteWinProbability = (float)(context.ClubRound.Context?.FavoriteWinProbability ?? 0m),
                ClubForecastExpectedResult = context.ClubRound.Context?.ForecastExpectedResult ?? 0,
                ClubForecastRiskScore = MapRiskLevelToScore(context.ClubRound.Context?.ForecastRiskLevel),
                ClubPositionDistributionValue = (float)context.ClubPositionStats.DistributionValue,
                ClubAverageInsightConfidence = (float)(context.ClubRound.Context?.AverageInsightConfidence ?? 0m),
                ClubPositiveInsightNet = (context.ClubRound.Context?.PositiveInsightCount ?? 0) - (context.ClubRound.Context?.NegativeInsightCount ?? 0),
                ClubTeamTipsCount = context.ClubRound.Context?.TeamTipsCount ?? 0,
                ClubMatchTipsCount = context.ClubRound.Context?.MatchTipsCount ?? 0,
                ClubPositionPointsConcededAverage = (float)context.ClubPositionStats.PointsConcededAverage,
                ClubPositionPointsConqueredAverage = (float)context.ClubPositionStats.PointsConqueredAverage,
                OpponentWinProbability = (float)context.OpponentClubRound.WinProbability,
                OpponentDrawProbability = (float)context.OpponentClubRound.DrawProbability,
                OpponentLoseProbability = (float)context.OpponentClubRound.LoseProbability,
                OpponentOverallAverage = (float)context.OpponentClubRound.OverallAverage,
                OpponentMovingAverage = (float)context.OpponentClubRound.MovingAverage,
                OpponentExpectedPoints = (float)context.OpponentClubRound.ExpectedPoints,
                OpponentPreviousOverallAverage = (float)(context.OpponentClubRound.Context?.PreviousOverallAverage ?? 0m),
                OpponentRankingsHomeAverage = (float)(context.OpponentClubRound.Context?.RankingsHomeAverage ?? 0m),
                OpponentRankingsAwayAverage = (float)(context.OpponentClubRound.Context?.RankingsAwayAverage ?? 0m),
                OpponentHistoricalWinRate = (float)(context.OpponentClubRound.Context?.HistoricalWinRate ?? 0m),
                OpponentFavoriteWinProbability = (float)(context.OpponentClubRound.Context?.FavoriteWinProbability ?? 0m),
                OpponentForecastExpectedResult = context.OpponentClubRound.Context?.ForecastExpectedResult ?? 0,
                OpponentForecastRiskScore = MapRiskLevelToScore(context.OpponentClubRound.Context?.ForecastRiskLevel),
                OpponentPositionDistributionValue = (float)context.OpponentPositionStats.DistributionValue,
                OpponentAverageInsightConfidence = (float)(context.OpponentClubRound.Context?.AverageInsightConfidence ?? 0m),
                OpponentPositiveInsightNet = (context.OpponentClubRound.Context?.PositiveInsightCount ?? 0) - (context.OpponentClubRound.Context?.NegativeInsightCount ?? 0),
                OpponentTeamTipsCount = context.OpponentClubRound.Context?.TeamTipsCount ?? 0,
                OpponentMatchTipsCount = context.OpponentClubRound.Context?.MatchTipsCount ?? 0,
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

    private static int MapRiskLevelToScore(string? riskLevel)
    {
        return (riskLevel ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "baixo" => 1,
            "medio" => 2,
            "médio" => 2,
            "alto" => 3,
            _ => 0,
        };
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
