namespace CartolaOptimization.Application.Models;

/// <summary>
/// Representa a importância baseada em permutação de um único grupo de features no modelo de pontuação de atletas full-context.
/// Um <see cref="MeanRSquaredDrop"/> mais negativo indica que a feature teve maior impacto na
/// acurácia preditiva: permutá-la causou a maior queda no R².
/// </summary>
public sealed class AthleteScoreFeatureImportance
{
    /// <summary>
    /// Nome da coluna da feature para features numéricas escalares, ou o nome do grupo agregado para features codificadas em one-hot
    /// features categóricas (por exemplo, "ClubEncoded", "PositionEncoded").
    /// </summary>
    public string FeatureName { get; init; } = string.Empty;

    /// <summary>
    /// Variação média do R² entre as execuções de permutação quando esta feature é embaralhada no conjunto de holdout.
    /// Um valor mais negativo indica uma feature mais importante (permutá-la degradou mais o modelo).
    /// Valores próximos de zero indicam features que contribuem pouco para a acurácia da previsão.
    /// </summary>
    public double MeanRSquaredDrop { get; init; }

    /// <summary>
    /// Desvio padrão da variação do R² entre as execuções de permutação, fornecendo uma estimativa de confiabilidade
    /// para a medição de importância.
    /// </summary>
    public double RSquaredDropStdDev { get; init; }

    /// <summary>
    /// Rank de importância em que 1 é o grupo de features mais importante.
    /// </summary>
    public int Rank { get; init; }

    /// <summary>
    /// Se esta feature foi incluída no modelo final treinado.
    /// Sempre verdadeiro quando nenhum filtro <c>topNFeatures</c> foi aplicado durante o treino.
    /// Falso para features numéricas descartadas por ficarem abaixo do corte top-N.
    /// As features categóricas codificadas (Club, Opponent, Position, Status) são sempre selecionadas.
    /// </summary>
    public bool IsSelected { get; init; }
}
