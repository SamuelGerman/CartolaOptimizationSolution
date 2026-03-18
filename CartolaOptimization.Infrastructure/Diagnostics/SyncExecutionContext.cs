using System.Text.Json;

namespace CartolaOptimization.Infrastructure.Diagnostics;

/// <summary>
/// Tracks execution breadcrumbs and business context for a synchronization run.
/// </summary>
public sealed class SyncExecutionContext
{
    private readonly List<SyncExecutionStep> steps = [];
    private readonly Dictionary<string, object?> contextData = [];

    public SyncExecutionContext(string sourceSystem, string processName)
    {
        SourceSystem = sourceSystem;
        ProcessName = processName;
        CorrelationId = Guid.NewGuid().ToString("N");
    }

    public string CorrelationId { get; }

    public string SourceSystem { get; }

    public string ProcessName { get; }

    public string CurrentStep { get; private set; } = "Initialization";

    public void MarkStep(string stepName)
    {
        CurrentStep = stepName;
        steps.Add(new SyncExecutionStep(DateTime.UtcNow, stepName));
    }

    public void SetContext(string key, object? value)
    {
        contextData[key] = value;
    }

    public IReadOnlyDictionary<string, object?> GetContextData()
        => contextData;

    public string ToExecutedStepsJson()
        => JsonSerializer.Serialize(steps);

    public string ToContextJson(IReadOnlyDictionary<string, object?>? extraContext = null)
    {
        var merged = new Dictionary<string, object?>(contextData)
        {
            ["correlationId"] = CorrelationId,
            ["sourceSystem"] = SourceSystem,
            ["processName"] = ProcessName,
            ["currentStep"] = CurrentStep,
        };

        if (extraContext is not null)
        {
            foreach (var pair in extraContext)
            {
                merged[pair.Key] = pair.Value;
            }
        }

        return JsonSerializer.Serialize(merged);
    }

    private sealed record SyncExecutionStep(DateTime OccurredAtUtc, string Name);
}
