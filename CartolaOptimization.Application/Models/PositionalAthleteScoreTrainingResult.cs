namespace CartolaOptimization.Application.Models;

/// <summary>
/// Resultado agregado de uma execução de treino do modelo posicional de pontuação de atletas nas seis posições do Cartola.
/// </summary>
public sealed class PositionalAthleteScoreTrainingResult
{
    public string Message { get; init; } = string.Empty;

    public int? FromRound { get; init; }

    public int? ToRound { get; init; }

    public int ValidationRounds { get; init; }

    /// <summary>Número de posições para as quais um modelo foi treinado e persistido com sucesso.</summary>
    public int TrainedPositions { get; init; }

    /// <summary>Número de posições descartadas por dados insuficientes.</summary>
    public int SkippedPositions { get; init; }

    /// <summary>Resultados de treino por posição, ordenados por <c>PositionCartolaId</c>.</summary>
    public IReadOnlyList<PositionalAthleteScoreModelResult> Positions { get; init; } = [];
}
