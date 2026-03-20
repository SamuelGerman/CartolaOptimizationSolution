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

    public decimal ClubPositionPointsConcededAverage { get; init; }

    public decimal ClubPositionPointsConqueredAverage { get; init; }

    public decimal OpponentWinProbability { get; init; }

    public decimal OpponentDrawProbability { get; init; }

    public decimal OpponentLoseProbability { get; init; }

    public decimal OpponentOverallAverage { get; init; }

    public decimal OpponentMovingAverage { get; init; }

    public decimal OpponentExpectedPoints { get; init; }

    public decimal OpponentPositionPointsConcededAverage { get; init; }

    public decimal OpponentPositionPointsConqueredAverage { get; init; }

    public decimal TargetScore { get; init; }

    public bool TargetWasInGame { get; init; }
}
