namespace CartolaOptimization.Application.Models;

/// <summary>
/// Represents a historical athlete-round row with full club, opponent and match context for supervised training.
/// </summary>
public sealed class TrainingFullContextDatasetRow
{
    public long AthleteRoundId { get; init; }

    public int RoundId { get; init; }

    public int AthleteCartolaId { get; init; }

    public string AthleteName { get; init; } = string.Empty;

    public string AthleteNickname { get; init; } = string.Empty;

    public int ClubCartolaId { get; init; }

    public string ClubName { get; init; } = string.Empty;

    public int PositionCartolaId { get; init; }

    public string PositionName { get; init; } = string.Empty;

    public int StatusCartolaId { get; init; }

    public string StatusName { get; init; } = string.Empty;

    public int GamesNum { get; init; }

    public int HomeGamesNum { get; init; }

    public int AwayGamesNum { get; init; }

    public decimal MovingAverage { get; init; }

    public decimal AverageScore { get; init; }

    public decimal HomeAverageScore { get; init; }

    public decimal AwayAverageScore { get; init; }

    public decimal PriceNum { get; init; }

    public decimal VariationNum { get; init; }

    public decimal TeamAverageScore { get; init; }

    public decimal PointsPerCartoletaCurrent { get; init; }

    public decimal GrossOverallMovingAverageCurrent { get; init; }

    public decimal NetOverallMovingAverageCurrent { get; init; }

    public decimal ValuationCurrent { get; init; }

    public decimal MinimumToAppreciateCurrent { get; init; }

    public decimal IndexPointsPerCartoleta { get; init; }

    public decimal IndexOffensiveEfficiency { get; init; }

    public decimal IndexDefensiveEfficiency { get; init; }

    public decimal IndexShotsPerGame { get; init; }

    public decimal IndexShotsPerGoal { get; init; }

    public bool IsHomeMatch { get; init; }

    public int ClubTablePosition { get; init; }

    public int OpponentClubCartolaId { get; init; }

    public string OpponentClubName { get; init; } = string.Empty;

    public int OpponentClubTablePosition { get; init; }

    public string ClubRecentForm { get; init; } = string.Empty;

    public string OpponentRecentForm { get; init; } = string.Empty;

    public decimal ClubWinProbability { get; init; }

    public decimal ClubDrawProbability { get; init; }

    public decimal ClubLoseProbability { get; init; }

    public decimal ClubOverallAverage { get; init; }

    public decimal ClubMovingAverage { get; init; }

    public decimal ClubExpectedPoints { get; init; }

    public decimal ClubPreviousOverallAverage { get; init; }

    public decimal ClubRankingsHomeAverage { get; init; }

    public decimal ClubRankingsAwayAverage { get; init; }

    public decimal ClubHistoricalWinRate { get; init; }

    public decimal ClubFavoriteWinProbability { get; init; }

    public int ClubForecastExpectedResult { get; init; }

    public int ClubForecastRiskScore { get; init; }

    public decimal ClubPositionDistributionValue { get; init; }

    public decimal ClubAverageInsightConfidence { get; init; }

    public int ClubPositiveInsightNet { get; init; }

    public int ClubTeamTipsCount { get; init; }

    public int ClubMatchTipsCount { get; init; }

    public decimal ClubPositionPointsConcededAverage { get; init; }

    public decimal ClubPositionPointsConqueredAverage { get; init; }

    public decimal OpponentWinProbability { get; init; }

    public decimal OpponentDrawProbability { get; init; }

    public decimal OpponentLoseProbability { get; init; }

    public decimal OpponentOverallAverage { get; init; }

    public decimal OpponentMovingAverage { get; init; }

    public decimal OpponentExpectedPoints { get; init; }

    public decimal OpponentPreviousOverallAverage { get; init; }

    public decimal OpponentRankingsHomeAverage { get; init; }

    public decimal OpponentRankingsAwayAverage { get; init; }

    public decimal OpponentHistoricalWinRate { get; init; }

    public decimal OpponentFavoriteWinProbability { get; init; }

    public int OpponentForecastExpectedResult { get; init; }

    public int OpponentForecastRiskScore { get; init; }

    public decimal OpponentPositionDistributionValue { get; init; }

    public decimal OpponentAverageInsightConfidence { get; init; }

    public int OpponentPositiveInsightNet { get; init; }

    public int OpponentTeamTipsCount { get; init; }

    public int OpponentMatchTipsCount { get; init; }

    public decimal OpponentPositionPointsConcededAverage { get; init; }

    public decimal OpponentPositionPointsConqueredAverage { get; init; }

    public decimal TargetScore { get; init; }

    public bool TargetWasInGame { get; init; }
}
