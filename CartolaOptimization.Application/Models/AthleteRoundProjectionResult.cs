namespace CartolaOptimization.Application.Models;

/// <summary>
/// Representa as pontuações projetadas dos atletas para uma dada rodada.
/// </summary>
public sealed class AthleteRoundProjectionResult
{
    public int Round { get; init; }

    public string ModelVersion { get; init; } = string.Empty;

    public DateTime GeneratedAtUtc { get; init; }

    public int TotalAthletesConsidered { get; init; }

    public int TotalAthletesProjected { get; init; }

    public int? PositionCartolaIdFilter { get; init; }

    public int? ClubCartolaIdFilter { get; init; }

    public int? StatusCartolaIdFilter { get; init; }

    public IReadOnlyCollection<AthleteProjectedScoreItem> Athletes { get; init; } = [];
}
