namespace CartolaOptimization.Application.Models;

/// <summary>
/// Describes an export-ready historical athlete-context dataset slice.
/// </summary>
public sealed class TrainingAthleteContextDatasetResult
{
    public string DatasetKey { get; init; } = "athlete-context";

    public int? FromRound { get; init; }

    public int? ToRound { get; init; }

    public int RequestedTake { get; init; }

    public int TotalHistoricalRowsConsidered { get; init; }

    public int TotalExportableRows { get; init; }

    public int ExcludedRows { get; init; }

    public int DistinctAthletes { get; init; }

    public int ReturnedRows { get; init; }

    public string EligibilityRule { get; init; } = string.Empty;

    public IReadOnlyCollection<TrainingAthleteContextDatasetRow> Rows { get; init; } = [];
}
