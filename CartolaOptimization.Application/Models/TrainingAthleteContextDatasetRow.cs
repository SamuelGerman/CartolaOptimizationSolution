namespace CartolaOptimization.Application.Models;

/// <summary>
/// Representa uma linha histórica de atleta-rodada enriquecida com contexto do Gato Mestre em nível de atleta e contexto de partida do Cartola.
/// </summary>
public sealed class TrainingAthleteContextDatasetRow
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

    public bool HasAthleteContext { get; init; }

    public decimal TeamAverageScore { get; init; }

    public decimal TeamHomeAverageCurrent { get; init; }

    public decimal TeamHomeAveragePrevious { get; init; }

    public decimal TeamAwayAverageCurrent { get; init; }

    public decimal TeamAwayAveragePrevious { get; init; }

    public decimal PointsPerCartoletaCurrent { get; init; }

    public decimal PointsPerCartoletaPrevious { get; init; }

    public decimal GrossOverallAverageCurrent { get; init; }

    public decimal GrossOverallMovingAverageCurrent { get; init; }

    public decimal GrossOverallAveragePrevious { get; init; }

    public decimal GrossOverallMovingAveragePrevious { get; init; }

    public decimal NetOverallAverageCurrent { get; init; }

    public decimal NetOverallMovingAverageCurrent { get; init; }

    public decimal NetOverallAveragePrevious { get; init; }

    public decimal NetOverallMovingAveragePrevious { get; init; }

    public decimal ValuationCurrent { get; init; }

    public decimal MinimumToAppreciateCurrent { get; init; }

    public decimal ValuationPrevious { get; init; }

    public decimal MinimumToAppreciatePrevious { get; init; }

    public decimal IndexPointsPerCartoleta { get; init; }

    public decimal IndexOffensiveUsage { get; init; }

    public decimal IndexOffensiveRepresentation { get; init; }

    public decimal IndexPointsRepresentation { get; init; }

    public decimal IndexOffensiveEfficiency { get; init; }

    public decimal IndexDefensiveEfficiency { get; init; }

    public decimal IndexTackleDuelPercent { get; init; }

    public decimal IndexShotsPerGame { get; init; }

    public decimal IndexShotsPerGoal { get; init; }

    public bool IsHomeMatch { get; init; }

    public int ClubTablePosition { get; init; }

    public int OpponentClubCartolaId { get; init; }

    public string OpponentClubName { get; init; } = string.Empty;

    public int OpponentClubTablePosition { get; init; }

    public string ClubRecentForm { get; init; } = string.Empty;

    public string OpponentRecentForm { get; init; } = string.Empty;

    public decimal TargetScore { get; init; }

    public bool TargetWasInGame { get; init; }
}
