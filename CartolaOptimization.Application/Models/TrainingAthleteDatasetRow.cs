namespace CartolaOptimization.Application.Models;

/// <summary>
/// Representa uma linha histórica de atleta-rodada que pode ser usada por um conjunto de dados de treino de referência apenas com atletas.
/// </summary>
public sealed class TrainingAthleteDatasetRow
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

    public decimal TargetScore { get; init; }

    public bool TargetWasInGame { get; init; }
}
