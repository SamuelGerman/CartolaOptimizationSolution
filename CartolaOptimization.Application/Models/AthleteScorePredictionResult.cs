namespace CartolaOptimization.Application.Models;

/// <summary>
/// Representa uma lista ordenada de previsões de pontuação de atletas por aprendizado de máquina para uma rodada-alvo.
/// </summary>
public sealed class AthleteScorePredictionResult
{
    public int Round { get; init; }

    public string ModelVersion { get; init; } = string.Empty;

    public string DatasetKey { get; init; } = string.Empty;

    public DateTime GeneratedAtUtc { get; init; }

    public DateTime ModelTrainedAtUtc { get; init; }

    public int TotalAthletesConsidered { get; init; }

    public int ExcludedAthletesWithoutContext { get; init; }

    public int TotalAthletesPredicted { get; init; }

    public int? StatusCartolaIdFilter { get; init; }

    public int? ClubCartolaIdFilter { get; init; }

    public int? PositionCartolaIdFilter { get; init; }

    public double ValidationMeanAbsoluteError { get; init; }

    public double ValidationRootMeanSquaredError { get; init; }

    public string PredictionNotes { get; init; } = string.Empty;

    public IReadOnlyCollection<AthleteScorePredictionItem> Athletes { get; init; } = [];
}
