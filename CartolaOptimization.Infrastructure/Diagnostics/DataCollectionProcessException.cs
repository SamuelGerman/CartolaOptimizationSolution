namespace CartolaOptimization.Infrastructure.Diagnostics;

/// <summary>
/// Representa uma falha de sincronização que já foi correlacionada e opcionalmente persistida na tabela de log de falhas.
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
