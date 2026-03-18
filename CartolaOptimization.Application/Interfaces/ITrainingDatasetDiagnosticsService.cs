using CartolaOptimization.Application.Models;

namespace CartolaOptimization.Application.Interfaces;

/// <summary>
/// Defines administrative read operations for the persisted training dataset readiness metadata.
/// </summary>
public interface ITrainingDatasetDiagnosticsService
{
    Task<TrainingDatasetCoverageSummary> GetCoverageSummaryAsync(
        int? fromRound = null,
        int? toRound = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<TrainingAthleteRoundListItem>> GetAthleteRoundsAsync(
        int? fromRound = null,
        int? toRound = null,
        string? readinessStatus = null,
        string? dataPhase = null,
        bool? isTrainingEligible = null,
        int take = 100,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<TrainingClubRoundListItem>> GetClubRoundsAsync(
        int? fromRound = null,
        int? toRound = null,
        string? readinessStatus = null,
        bool? isTrainingEligible = null,
        int take = 100,
        CancellationToken cancellationToken = default);
}
