namespace CartolaOptimization.Domain.Constants;

/// <summary>
/// Defines the readiness values used to indicate whether a row can be used by the training dataset.
/// </summary>
public static class TrainingReadinessStatusValues
{
    public const string Complete = "Complete";
    public const string Partial = "Partial";
    public const string UnfitForTraining = "UnfitForTraining";
}
