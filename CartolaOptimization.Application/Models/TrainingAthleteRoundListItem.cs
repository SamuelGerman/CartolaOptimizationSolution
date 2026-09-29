namespace CartolaOptimization.Application.Models;

/// <summary>
/// Representa uma linha compacta usada para inspecionar a prontidão de atleta-rodada no conjunto de dados de treino.
/// </summary>
public sealed class TrainingAthleteRoundListItem
{
    public long Id { get; init; }

    public int RoundId { get; init; }

    public int AthleteCartolaId { get; init; }

    public string AthleteName { get; init; } = string.Empty;

    public string AthleteNickname { get; init; } = string.Empty;

    public int ClubCartolaId { get; init; }

    public string ClubName { get; init; } = string.Empty;

    public string PositionName { get; init; } = string.Empty;

    public decimal ScoreNum { get; init; }

    public decimal PriceNum { get; init; }

    public bool WasInGame { get; init; }

    public string DataPhase { get; init; } = string.Empty;

    public string TrainingReadinessStatus { get; init; } = string.Empty;

    public string TrainingReadinessReason { get; init; } = string.Empty;

    public bool IsTrainingEligible { get; init; }
}
