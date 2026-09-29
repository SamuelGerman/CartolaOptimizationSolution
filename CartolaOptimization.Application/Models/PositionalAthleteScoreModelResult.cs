namespace CartolaOptimization.Application.Models;

/// <summary>
/// Resultado de treinar (ou tentar treinar) o modelo de pontuação de atletas para uma única posição do Cartola.
/// </summary>
public sealed class PositionalAthleteScoreModelResult
{
    public int PositionCartolaId { get; init; }

    public string PositionCode { get; init; } = string.Empty;

    public string PositionName { get; init; } = string.Empty;

    /// <summary>Se o modelo desta posição foi treinado com sucesso nesta execução.</summary>
    public bool Trained { get; init; }

    /// <summary>Quando <see cref="Trained"/> é <c>false</c>, o motivo pelo qual o treino foi descartado.</summary>
    public string? SkipReason { get; init; }

    /// <summary>Linhas de treino usadas no modelo desta posição.</summary>
    public int TrainingRows { get; init; }

    /// <summary>Linhas de holdout usadas para validação.</summary>
    public int ValidationRows { get; init; }

    /// <summary>Metadados do modelo quando o treino teve sucesso; <c>null</c> quando descartado.</summary>
    public AthleteScoreModelInfo? ModelInfo { get; init; }
}
