namespace CartolaOptimization.Application.Models;

/// <summary>
/// Represents the projected scores of athletes for a given round.
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

    public IReadOnlyCollection<AthleteProjectedScoreItem> Athletes { get; init; } = [];
}
