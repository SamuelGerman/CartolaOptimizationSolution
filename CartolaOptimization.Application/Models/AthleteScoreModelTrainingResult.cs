namespace CartolaOptimization.Application.Models;

/// <summary>
/// Representa o resultado de treinar e persistir o modelo de pontuação de atletas v0.
/// </summary>
public sealed class AthleteScoreModelTrainingResult
{
    public string Message { get; init; } = string.Empty;

    public AthleteScoreModelInfo ModelInfo { get; init; } = new();
}
