namespace CartolaOptimization.Application.Models;

/// <summary>
/// Representa um recorte do conjunto de dados de treino apenas com atletas, pronto para exportação.
/// </summary>
public sealed class TrainingAthleteDatasetResult
{
    public string DatasetKey { get; init; } = "athlete-only";

    public int? FromRound { get; init; }

    public int? ToRound { get; init; }

    public int RequestedTake { get; init; }

    public int TotalHistoricalRowsConsidered { get; init; }

    public int TotalExportableRows { get; init; }

    public int ExcludedRows { get; init; }

    public int DistinctAthletes { get; init; }

    public int ReturnedRows { get; init; }

    public string EligibilityRule { get; init; } = string.Empty;

    public IReadOnlyCollection<TrainingAthleteDatasetRow> Rows { get; init; } = [];
}
