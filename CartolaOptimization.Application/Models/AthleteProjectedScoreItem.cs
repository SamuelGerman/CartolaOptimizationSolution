namespace CartolaOptimization.Application.Models;

/// <summary>
/// Represents the expected-score projection generated for a single athlete in a target round.
/// </summary>
public sealed class AthleteProjectedScoreItem
{
    public int AthleteCartolaId { get; init; }

    public string AthleteName { get; init; } = string.Empty;

    public string AthleteNickname { get; init; } = string.Empty;

    public int ClubCartolaId { get; init; }

    public string ClubName { get; init; } = string.Empty;

    public int PositionCartolaId { get; init; }

    public string PositionName { get; init; } = string.Empty;

    public int StatusCartolaId { get; init; }

    public string StatusName { get; init; } = string.Empty;

    public int BaselineRound { get; init; }

    public int? OpponentClubCartolaId { get; init; }

    public string OpponentClubName { get; init; } = string.Empty;

    public bool? IsHomeMatch { get; init; }

    public decimal ExpectedScore { get; init; }

    public decimal ConfidenceScore { get; init; }

    public decimal BaselineComponent { get; init; }

    public decimal TeamContextAdjustment { get; init; }

    public decimal OpponentPositionAdjustment { get; init; }

    public decimal PriceTrendAdjustment { get; init; }

    public decimal StatusMultiplier { get; init; }

    public decimal MovingAverage { get; init; }

    public decimal AverageScore { get; init; }

    public decimal ContextAverage { get; init; }

    public decimal ClubExpectedPoints { get; init; }

    public decimal ClubWinProbability { get; init; }

    public decimal ClubLoseProbability { get; init; }

    public decimal OpponentPointsConcededAverage { get; init; }

    public string ProjectionNotes { get; init; } = string.Empty;
}
