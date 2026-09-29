namespace CartolaOptimization.Application.Models;

/// <summary>
/// Define os hiperparâmetros ajustáveis do treinador de regressão LightGBM usado no
/// modelo de pontuação de atletas full-context. Os valores correspondem diretamente às opções aceitas por
/// <c>LightGbmRegressionTrainer.Options</c> no ML.NET.
/// </summary>
public sealed record LightGbmHyperparameters
{
    /// <summary>Número de iterações de boosting (árvores). Valores maiores aumentam a capacidade mas podem causar overfitting.</summary>
    public int NumberOfIterations { get; init; } = 300;

    /// <summary>Encolhimento do passo (learning rate) aplicado à contribuição de cada árvore. Valores menores exigem mais iterações.</summary>
    public double LearningRate { get; init; } = 0.05;

    /// <summary>Número máximo de folhas por árvore. Controla a complexidade do modelo.</summary>
    public int NumberOfLeaves { get; init; } = 63;

    /// <summary>Número mínimo de exemplos de treino necessários para formar um nó folha. Atua como regularização.</summary>
    public int MinimumExampleCountPerLeaf { get; init; } = 20;

    /// <summary>Coeficiente de regularização L1 (Lasso). Promove esparsidade nos pesos das folhas.</summary>
    public double L1Regularization { get; init; } = 0.0;

    /// <summary>L2 (Ridge) regularization coefficient. Shrinks leaf weights toward zero.</summary>
    public double L2Regularization { get; init; } = 0.0;
}
