namespace CartolaOptimization.Infrastructure.Diagnostics;

/// <summary>
/// Define os valores de status do ciclo de vida usados pelas linhas de execução de sincronização.
/// </summary>
public static class SyncExecutionStatus
{
    public const string Running = "Running";
    public const string Success = "Success";
    public const string Partial = "Partial";
    public const string Failed = "Failed";
}
