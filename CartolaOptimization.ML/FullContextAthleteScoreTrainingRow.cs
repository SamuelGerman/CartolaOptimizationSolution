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

    public float ClubPositionPointsConcededAverage { get; init; }

    public float ClubPositionPointsConqueredAverage { get; init; }

    public float OpponentWinProbability { get; init; }

    public float OpponentDrawProbability { get; init; }

    public float OpponentLoseProbability { get; init; }

    public float OpponentOverallAverage { get; init; }

    public float OpponentMovingAverage { get; init; }

    public float OpponentExpectedPoints { get; init; }

    public float OpponentPositionPointsConcededAverage { get; init; }

    public float OpponentPositionPointsConqueredAverage { get; init; }

    public float Label { get; init; }
}
