namespace CartolaOptimization.Application.Models;

/// <summary>
/// Representa uma previsão de pontuação por aprendizado de máquina para um único atleta em uma rodada-alvo.
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
    /// Número de rodadas em que este atleta de fato jogou (WasInGame = true) antes da rodada-alvo.
    /// Usado para ponderar a confiabilidade da previsão: uma pontuação alta baseada em um único jogo é menos confiável
    /// que a mesma pontuação construída a partir de muitas observações. Null quando não disponível (versões mais antigas do modelo).
    /// </summary>
    public int? PlayedMatchesSampleSize { get; init; }

    public decimal PredictedScore { get; init; }

    public decimal PredictionConfidence { get; init; }

    public string PredictionNotes { get; init; } = string.Empty;
}
