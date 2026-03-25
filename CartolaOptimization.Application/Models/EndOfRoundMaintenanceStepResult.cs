namespace CartolaOptimization.Application.Models;

/// <summary>
/// Represents a single step executed or skipped during the automated end-of-round maintenance workflow.
/// </summary>
public sealed class EndOfRoundMaintenanceStepResult
{
    public string Step { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string Reason { get; init; } = string.Empty;

    public IReadOnlyCollection<int> Rounds { get; init; } = Array.Empty<int>();
}
