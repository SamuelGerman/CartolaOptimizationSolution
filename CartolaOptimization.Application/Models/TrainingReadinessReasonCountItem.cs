namespace CartolaOptimization.Application.Models;

/// <summary>
/// Represents a grouped count of a readiness blocking reason.
/// </summary>
public sealed class TrainingReadinessReasonCountItem
{
    public string Reason { get; init; } = string.Empty;

    public int Count { get; init; }
}
