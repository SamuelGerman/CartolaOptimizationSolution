namespace CartolaOptimization.Infrastructure.Diagnostics;

/// <summary>
/// Representa o payload de diagnóstico persistido quando ocorre uma falha de sincronização.
/// </summary>
public sealed class IntegrationFailureLogEntry
{
    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;

    public string CorrelationId { get; init; } = string.Empty;

    public string SourceSystem { get; init; } = string.Empty;

    public string ProcessName { get; init; } = string.Empty;

    public string StepName { get; init; } = string.Empty;

    public string ErrorLocation { get; init; } = string.Empty;

    public string RequestMethod { get; init; } = string.Empty;

    public string RequestUrl { get; init; } = string.Empty;

    public string RequestHeadersJson { get; init; } = string.Empty;

    public string RequestPayload { get; init; } = string.Empty;

    public int? ResponseStatusCode { get; init; }

    public string ResponseReasonPhrase { get; init; } = string.Empty;

    public string ResponseHeadersJson { get; init; } = string.Empty;

    public string ResponsePayload { get; init; } = string.Empty;

    public string ExecutedStepsJson { get; init; } = string.Empty;

    public string ContextJson { get; init; } = string.Empty;

    public string ErrorCode { get; init; } = string.Empty;

    public string Severity { get; init; } = string.Empty;

    public string ExceptionType { get; init; } = string.Empty;

    public string ExceptionMessage { get; init; } = string.Empty;

    public string InnerExceptionMessage { get; init; } = string.Empty;

    public string StackTrace { get; init; } = string.Empty;

    public bool TransactionRolledBack { get; init; }
}
