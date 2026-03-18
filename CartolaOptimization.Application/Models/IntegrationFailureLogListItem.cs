namespace CartolaOptimization.Application.Models;

/// <summary>
/// Represents a compact view of a persisted integration failure log.
/// </summary>
public sealed class IntegrationFailureLogListItem
{
    public int Id { get; init; }

    public DateTime OccurredAtUtc { get; init; }

    public string CorrelationId { get; init; } = string.Empty;

    public string SourceSystem { get; init; } = string.Empty;

    public string ProcessName { get; init; } = string.Empty;

    public string StepName { get; init; } = string.Empty;

    public string ErrorCode { get; init; } = string.Empty;

    public string Severity { get; init; } = string.Empty;

    public string ExceptionType { get; init; } = string.Empty;

    public string ExceptionMessage { get; init; } = string.Empty;

    public int? ResponseStatusCode { get; init; }
}
