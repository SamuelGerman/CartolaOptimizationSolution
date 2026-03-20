namespace CartolaOptimization.Application.Models;

/// <summary>
/// Represents a ranked list of athlete machine-learning score predictions for a target round.
/// </summary>
public sealed class AthleteScorePredictionResult
{
    public int Round { get; init; }

    public string ModelVersion { get; init; } = string.Empty;

    public DateTime GeneratedAtUtc { get; init; }

    public DateTime ModelTrainedAtUtc { get; init; }

    public int TotalAthletesConsidered { get; init; }

    public int TotalAthletesPredicted { get; init; }

    public int? StatusCartolaIdFilter { get; init; }

    public int? ClubCartolaIdFilter { get; init; }

    public int? PositionCartolaIdFilter { get; init; }

    public double ValidationMeanAbsoluteError { get; init; }

    public double ValidationRootMeanSquaredError { get; init; }

    public IReadOnlyCollection<AthleteScorePredictionItem> Athletes { get; init; } = [];
}
