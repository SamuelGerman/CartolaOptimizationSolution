namespace CartolaOptimization.ML;

internal sealed class FullContextAthleteScoreTrainingRow
{
    public float RoundId { get; init; }

    public float ClubCartolaId { get; init; }

    public float OpponentClubCartolaId { get; init; }

    public float PositionCartolaId { get; init; }

    public float StatusCartolaId { get; init; }

    public float GamesNum { get; init; }

    public float HomeGamesNum { get; init; }

    public float AwayGamesNum { get; init; }

    public float MovingAverage { get; init; }

    public float AverageScore { get; init; }

    public float HomeAverageScore { get; init; }

    public float AwayAverageScore { get; init; }

    public float PriceNum { get; init; }

    public float VariationNum { get; init; }

    public float TeamAverageScore { get; init; }

    public float PointsPerCartoletaCurrent { get; init; }

    public float GrossOverallMovingAverageCurrent { get; init; }

    public float NetOverallMovingAverageCurrent { get; init; }

    public float ValuationCurrent { get; init; }

    public float MinimumToAppreciateCurrent { get; init; }

    public float IndexPointsPerCartoleta { get; init; }

    public float IndexOffensiveEfficiency { get; init; }

    public float IndexDefensiveEfficiency { get; init; }

    public float IndexShotsPerGame { get; init; }

    public float IndexShotsPerGoal { get; init; }

    public float IsHomeMatch { get; init; }

    public float ClubTablePosition { get; init; }

    public float OpponentClubTablePosition { get; init; }

    public string ClubRecentForm { get; init; } = string.Empty;

    public string OpponentRecentForm { get; init; } = string.Empty;

    public float ClubWinProbability { get; init; }

    public float ClubDrawProbability { get; init; }

    public float ClubLoseProbability { get; init; }

    public float ClubOverallAverage { get; init; }

    public float ClubMovingAverage { get; init; }

    public float ClubExpectedPoints { get; init; }

    public float ClubPreviousOverallAverage { get; init; }

    public float ClubRankingsHomeAverage { get; init; }

    public float ClubRankingsAwayAverage { get; init; }

    public float ClubHistoricalWinRate { get; init; }

    public float ClubFavoriteWinProbability { get; init; }

    public float ClubForecastExpectedResult { get; init; }

    public float ClubForecastRiskScore { get; init; }

    public float ClubPositionDistributionValue { get; init; }

    public float ClubAverageInsightConfidence { get; init; }

    public float ClubPositiveInsightNet { get; init; }

    public float ClubTeamTipsCount { get; init; }

    public float ClubMatchTipsCount { get; init; }

    public float ClubPositionPointsConcededAverage { get; init; }

    public float ClubPositionPointsConqueredAverage { get; init; }

    public float OpponentWinProbability { get; init; }

    public float OpponentDrawProbability { get; init; }

    public float OpponentLoseProbability { get; init; }

    public float OpponentOverallAverage { get; init; }

    public float OpponentMovingAverage { get; init; }

    public float OpponentExpectedPoints { get; init; }

    public float OpponentPreviousOverallAverage { get; init; }

    public float OpponentRankingsHomeAverage { get; init; }

    public float OpponentRankingsAwayAverage { get; init; }

    public float OpponentHistoricalWinRate { get; init; }

    public float OpponentFavoriteWinProbability { get; init; }

    public float OpponentForecastExpectedResult { get; init; }

    public float OpponentForecastRiskScore { get; init; }

    public float OpponentPositionDistributionValue { get; init; }

    public float OpponentAverageInsightConfidence { get; init; }

    public float OpponentPositiveInsightNet { get; init; }

    public float OpponentTeamTipsCount { get; init; }

    public float OpponentMatchTipsCount { get; init; }

    public float OpponentPositionPointsConcededAverage { get; init; }

    public float OpponentPositionPointsConqueredAverage { get; init; }

    public float Label { get; init; }
}
