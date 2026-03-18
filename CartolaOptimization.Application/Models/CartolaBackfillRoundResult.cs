namespace CartolaOptimization.Application.Models;

/// <summary>
/// Represents the outcome of a single historical Cartola round backfill.
/// </summary>
public sealed class CartolaBackfillRoundResult
{
    public int Round { get; init; }

    public string Status { get; init; } = string.Empty;

    public int? SyncExecutionId { get; init; }

    public string CorrelationId { get; init; } = string.Empty;

    public string Notes { get; init; } = string.Empty;

    public string ErrorMessage { get; init; } = string.Empty;

    public int? ErrorLogId { get; init; }
}
