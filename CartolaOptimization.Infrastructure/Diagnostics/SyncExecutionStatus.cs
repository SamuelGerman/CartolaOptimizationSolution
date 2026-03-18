namespace CartolaOptimization.Infrastructure.Diagnostics;

/// <summary>
/// Defines the lifecycle status values used by synchronization execution rows.
/// </summary>
public static class SyncExecutionStatus
{
    public const string Running = "Running";
    public const string Success = "Success";
    public const string Partial = "Partial";
    public const string Failed = "Failed";
}
