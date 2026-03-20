namespace CartolaOptimization.Application.Models;

/// <summary>
/// Represents the outcome of training and persisting the v0 athlete-score model.
/// </summary>
public sealed class AthleteScoreModelTrainingResult
{
    public string Message { get; init; } = string.Empty;

    public AthleteScoreModelInfo ModelInfo { get; init; } = new();
}
