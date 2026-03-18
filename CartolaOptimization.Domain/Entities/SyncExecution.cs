namespace CartolaOptimization.Domain.Entities;

/// <summary>
/// Persists the lifecycle and quality metrics of each synchronization execution.
/// </summary>
public sealed class SyncExecution
{
    /// <summary>
    /// Gets or sets the primary identifier of the execution row.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the correlation id shared with failure logs emitted by the same execution.
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the synchronized source system, such as Cartola or GatoMestre.
    /// </summary>
    public string SourceSystem { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the process name responsible for the synchronization.
    /// </summary>
    public string ProcessName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the UTC timestamp in which the execution started.
    /// </summary>
    public DateTime StartedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp in which the execution finished.
    /// </summary>
    public DateTime? FinishedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the total execution duration in milliseconds when available.
    /// </summary>
    public long? DurationMs { get; set; }

    /// <summary>
    /// Gets or sets the final execution status.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the most recent pipeline step reached by the execution.
    /// </summary>
    public string LastStepName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the requested season when the execution targets a seasonal dataset.
    /// </summary>
    public int? Season { get; set; }

    /// <summary>
    /// Gets or sets the requested round when the execution targets a round-specific dataset.
    /// </summary>
    public int? Round { get; set; }

    /// <summary>
    /// Gets or sets the normalized business error code associated with the execution when applicable.
    /// </summary>
    public string ErrorCode { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the final execution note shown to operators.
    /// </summary>
    public string Notes { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the serialized metrics produced by the execution.
    /// </summary>
    public string MetricsJson { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the serialized business context captured for the execution.
    /// </summary>
    public string ContextJson { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the serialized quality thresholds used to evaluate the execution.
    /// </summary>
    public string QualityCriteriaJson { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the achieved coverage rate when the metric applies.
    /// </summary>
    public decimal? CoverageRate { get; set; }

    /// <summary>
    /// Gets or sets the percentage of skipped items when the metric applies.
    /// </summary>
    public decimal? SkippedRate { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the execution met the configured quality criteria.
    /// </summary>
    public bool? QualityCriteriaSatisfied { get; set; }

    /// <summary>
    /// Gets or sets the total number of persisted warning logs linked to the execution.
    /// </summary>
    public int WarningLogsCount { get; set; }

    /// <summary>
    /// Gets or sets the total number of persisted error logs linked to the execution.
    /// </summary>
    public int ErrorLogsCount { get; set; }
}
