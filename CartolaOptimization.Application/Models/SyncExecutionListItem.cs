namespace CartolaOptimization.Application.Models;

/// <summary>
/// Representa uma visão compacta de uma execução de sincronização persistida.
/// </summary>
public sealed class SyncExecutionListItem
{
    public int Id { get; init; }

    public string CorrelationId { get; init; } = string.Empty;

    public string SourceSystem { get; init; } = string.Empty;

    public string ProcessName { get; init; } = string.Empty;

    public DateTime StartedAtUtc { get; init; }

    public DateTime? FinishedAtUtc { get; init; }

    public long? DurationMs { get; init; }

    public string Status { get; init; } = string.Empty;

    public int? Season { get; init; }

    public int? Round { get; init; }

    public string ErrorCode { get; init; } = string.Empty;

    public decimal? CoverageRate { get; init; }

    public decimal? SkippedRate { get; init; }

    public bool? QualityCriteriaSatisfied { get; init; }

    public int WarningLogsCount { get; init; }

    public int ErrorLogsCount { get; init; }

    public string Notes { get; init; } = string.Empty;
}
