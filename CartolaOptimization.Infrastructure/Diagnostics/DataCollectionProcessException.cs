namespace CartolaOptimization.Infrastructure.Diagnostics;

/// <summary>
/// Represents a synchronization failure that was already correlated and optionally persisted to the failure log table.
/// </summary>
public sealed class DataCollectionProcessException : Exception
{
    public DataCollectionProcessException(
        string sourceSystem,
        string processName,
        string stepName,
        string correlationId,
        int? errorLogId,
        string message,
        Exception innerException) : base(message, innerException)
    {
        SourceSystem = sourceSystem;
        ProcessName = processName;
        StepName = stepName;
        CorrelationId = correlationId;
        ErrorLogId = errorLogId;
    }

    public string SourceSystem { get; }

    public string ProcessName { get; }

    public string StepName { get; }

    public string CorrelationId { get; }

    public int? ErrorLogId { get; }
}
