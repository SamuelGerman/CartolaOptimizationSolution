namespace CartolaOptimization.Application.Models;

/// <summary>
/// Represents a machine-learning score prediction for a single athlete in a target round.
/// </summary>
public sealed class AthleteScorePredictionItem
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

    public string BaselineDataPhase { get; init; } = string.Empty;

    public decimal PriceNum { get; init; }

    public decimal MovingAverage { get; init; }

    public decimal AverageScore { get; init; }

    public bool? IsHomeMatch { get; init; }

    public int? ClubTablePosition { get; init; }

    public int? OpponentClubCartolaId { get; init; }

    public string OpponentClubName { get; init; } = string.Empty;

    public int? OpponentClubTablePosition { get; init; }

    public decimal? ClubWinProbability { get; init; }

    public decimal? ClubExpectedPoints { get; init; }

    public decimal? OpponentWinProbability { get; init; }

    public decimal? OpponentExpectedPoints { get; init; }

    /// <summary>
    /// Number of rounds in which this athlete actually played (WasInGame = true) prior to the target round.
    /// Used to weight prediction reliability: a high score based on a single game is less trustworthy
    /// than the same score built from many observations. Null when not available (older model versions).
    /// </summary>
    public int? PlayedMatchesSampleSize { get; init; }

    public decimal PredictedScore { get; init; }

    public decimal PredictionConfidence { get; init; }

    public string PredictionNotes { get; init; } = string.Empty;
}
