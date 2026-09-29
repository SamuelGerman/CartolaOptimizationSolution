namespace CartolaOptimization.Application.Models;

/// <summary>
/// Representa uma contagem agrupada de um motivo de bloqueio de prontidão.
/// </summary>
public sealed class TrainingReadinessReasonCountItem
{
    public string Reason { get; init; } = string.Empty;

    public int Count { get; init; }
}
