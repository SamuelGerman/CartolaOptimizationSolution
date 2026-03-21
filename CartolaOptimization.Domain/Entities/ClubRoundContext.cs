namespace CartolaOptimization.Domain.Entities
{
    /// <summary>
    /// Stores additional club-round enrichment signals collected from Gato Mestre.
    /// </summary>
    public class ClubRoundContext
    {
        public ClubRoundContext()
        {
        }

        public ClubRoundContext(
            long id,
            long clubRoundId,
            decimal previousOverallAverage,
            decimal rankingsOverallAverage,
            decimal rankingsMovingAverage,
            decimal rankingsHomeAverage,
            decimal rankingsHomeMovingAverage,
            decimal rankingsAwayAverage,
            decimal rankingsAwayMovingAverage,
            decimal generalPointsConcededAverage,
            decimal generalPointsConqueredAverage,
            decimal homePointsConcededAverage,
            decimal homePointsConqueredAverage,
            decimal awayPointsConcededAverage,
            decimal awayPointsConqueredAverage,
            decimal historicalWinRate,
            decimal historicalDrawRate,
            decimal historicalLossRate,
            decimal favoriteWinProbability,
            decimal favoriteDrawProbability,
            decimal favoriteLoseProbability,
            int forecastExpectedResult,
            string forecastRiskLevel,
            int teamTipsCount,
            int hotTeamTipsCount,
            int matchTipsCount,
            int hotMatchTipsCount,
            int insightCount,
            int hotInsightCount,
            decimal averageInsightConfidence,
            int positiveInsightCount,
            int negativeInsightCount)
        {
            Id = id;
            ClubRoundId = clubRoundId;
            PreviousOverallAverage = previousOverallAverage;
            RankingsOverallAverage = rankingsOverallAverage;
            RankingsMovingAverage = rankingsMovingAverage;
            RankingsHomeAverage = rankingsHomeAverage;
            RankingsHomeMovingAverage = rankingsHomeMovingAverage;
            RankingsAwayAverage = rankingsAwayAverage;
            RankingsAwayMovingAverage = rankingsAwayMovingAverage;
            GeneralPointsConcededAverage = generalPointsConcededAverage;
            GeneralPointsConqueredAverage = generalPointsConqueredAverage;
            HomePointsConcededAverage = homePointsConcededAverage;
            HomePointsConqueredAverage = homePointsConqueredAverage;
            AwayPointsConcededAverage = awayPointsConcededAverage;
            AwayPointsConqueredAverage = awayPointsConqueredAverage;
            HistoricalWinRate = historicalWinRate;
            HistoricalDrawRate = historicalDrawRate;
            HistoricalLossRate = historicalLossRate;
            FavoriteWinProbability = favoriteWinProbability;
            FavoriteDrawProbability = favoriteDrawProbability;
            FavoriteLoseProbability = favoriteLoseProbability;
            ForecastExpectedResult = forecastExpectedResult;
            ForecastRiskLevel = forecastRiskLevel;
            TeamTipsCount = teamTipsCount;
            HotTeamTipsCount = hotTeamTipsCount;
            MatchTipsCount = matchTipsCount;
            HotMatchTipsCount = hotMatchTipsCount;
            InsightCount = insightCount;
            HotInsightCount = hotInsightCount;
            AverageInsightConfidence = averageInsightConfidence;
            PositiveInsightCount = positiveInsightCount;
            NegativeInsightCount = negativeInsightCount;
        }

        public long Id { get; set; }

        public long ClubRoundId { get; set; }

        public decimal PreviousOverallAverage { get; set; }

        public decimal RankingsOverallAverage { get; set; }

        public decimal RankingsMovingAverage { get; set; }

        public decimal RankingsHomeAverage { get; set; }

        public decimal RankingsHomeMovingAverage { get; set; }

        public decimal RankingsAwayAverage { get; set; }

        public decimal RankingsAwayMovingAverage { get; set; }

        public decimal GeneralPointsConcededAverage { get; set; }

        public decimal GeneralPointsConqueredAverage { get; set; }

        public decimal HomePointsConcededAverage { get; set; }

        public decimal HomePointsConqueredAverage { get; set; }

        public decimal AwayPointsConcededAverage { get; set; }

        public decimal AwayPointsConqueredAverage { get; set; }

        public decimal HistoricalWinRate { get; set; }

        public decimal HistoricalDrawRate { get; set; }

        public decimal HistoricalLossRate { get; set; }

        public decimal FavoriteWinProbability { get; set; }

        public decimal FavoriteDrawProbability { get; set; }

        public decimal FavoriteLoseProbability { get; set; }

        public int ForecastExpectedResult { get; set; }

        public string ForecastRiskLevel { get; set; } = string.Empty;

        public int TeamTipsCount { get; set; }

        public int HotTeamTipsCount { get; set; }

        public int MatchTipsCount { get; set; }

        public int HotMatchTipsCount { get; set; }

        public int InsightCount { get; set; }

        public int HotInsightCount { get; set; }

        public decimal AverageInsightConfidence { get; set; }

        public int PositiveInsightCount { get; set; }

        public int NegativeInsightCount { get; set; }

        public ClubRound ClubRound { get; set; } = null!;
    }
}
