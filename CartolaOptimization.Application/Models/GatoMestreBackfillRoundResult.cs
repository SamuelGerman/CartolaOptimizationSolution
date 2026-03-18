namespace CartolaOptimization.Application.Models;

/// <summary>
/// Represents a single round processed by the historical Gato Mestre backfill.
/// </summary>
public sealed class GatoMestreBackfillRoundResult
{
    public int Round { get; init; }

    public string Status { get; init; } = string.Empty;

    public int? SyncExecutionId { get; init; }

    public string CorrelationId { get; init; } = string.Empty;

    public string Notes { get; init; } = string.Empty;

    public string ErrorMessage { get; init; } = string.Empty;

    public int? ErrorLogId { get; init; }
}
