namespace CartolaOptimization.Infrastructure.Diagnostics;

/// <summary>
/// Representa uma inconsistência detectada ao traduzir payloads externos para o modelo relacional local.
/// </summary>
public sealed class DataCollectionIntegrityException : Exception
{
    public DataCollectionIntegrityException(
        string sourceSystem,
        string stepName,
        string errorLocation,
        string message,
        IReadOnlyDictionary<string, object?>? contextData = null,
        Exception? innerException = null) : base(message, innerException)
    {
        SourceSystem = sourceSystem;
        StepName = stepName;
        ErrorLocation = errorLocation;
        ContextData = contextData ?? new Dictionary<string, object?>();
    }

    public string SourceSystem { get; }

    public string StepName { get; }

    public string ErrorLocation { get; }

    public IReadOnlyDictionary<string, object?> ContextData { get; }
}
