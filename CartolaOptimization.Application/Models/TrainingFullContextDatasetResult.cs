namespace CartolaOptimization.Application.Models;

/// <summary>
/// Represents an export-ready full-context training dataset slice.
/// </summary>
public sealed class TrainingFullContextDatasetResult
{
    public string DatasetKey { get; init; } = "full-context";

    public int? FromRound { get; init; }

    public int? ToRound { get; init; }

    public int RequestedTake { get; init; }

    public int TotalHistoricalRowsConsidered { get; init; }

    public int TotalExportableRows { get; init; }

    public int ExcludedRows { get; init; }

    public int DistinctAthletes { get; init; }

    public int ReturnedRows { get; init; }

    public string EligibilityRule { get; init; } = string.Empty;

    public IReadOnlyCollection<TrainingFullContextDatasetRow> Rows { get; init; } = [];
}
