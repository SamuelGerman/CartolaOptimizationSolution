namespace CartolaOptimization.Application.Models;

/// <summary>
/// Defines the tuneable hyperparameters for the LightGBM regression trainer used in the
/// full-context athlete-score model. Values correspond directly to the options accepted by
/// <c>LightGbmRegressionTrainer.Options</c> in ML.NET.
/// </summary>
public sealed record LightGbmHyperparameters
{
    /// <summary>Number of boosting iterations (trees). Higher values increase capacity but may overfit.</summary>
    public int NumberOfIterations { get; init; } = 300;

    /// <summary>Step size shrinkage applied to each tree's contribution. Lower values require more iterations.</summary>
    public double LearningRate { get; init; } = 0.05;

    /// <summary>Maximum number of leaves per tree. Controls model complexity.</summary>
    public int NumberOfLeaves { get; init; } = 63;

    /// <summary>Minimum number of training examples required to form a leaf node. Acts as regularization.</summary>
    public int MinimumExampleCountPerLeaf { get; init; } = 20;

    /// <summary>L1 (Lasso) regularization coefficient. Promotes sparsity in leaf weights.</summary>
    public double L1Regularization { get; init; } = 0.0;

    /// <summary>L2 (Ridge) regularization coefficient. Shrinks leaf weights toward zero.</summary>
    public double L2Regularization { get; init; } = 0.0;
}
