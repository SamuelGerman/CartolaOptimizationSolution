namespace CartolaOptimization.Application.Models;

/// <summary>
/// Represents an athlete selected for the optimized predicted lineup.
/// </summary>
public sealed class PredictedLineupAthleteItem
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

    public decimal PredictedScore { get; init; }

    public decimal PredictionConfidence { get; init; }

    public string PredictionNotes { get; init; } = string.Empty;
}
