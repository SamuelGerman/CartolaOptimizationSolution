namespace CartolaOptimization.Application.Models;

/// <summary>
/// Represents the outcome of a Cartola synchronization execution.
/// </summary>
public sealed class CartolaSyncResult
{
    /// <summary>
    /// Gets the persisted synchronization execution identifier.
    /// </summary>
    public int? SyncExecutionId { get; init; }

    /// <summary>
    /// Gets the correlation id shared with diagnostics emitted during the execution.
    /// </summary>
    public string CorrelationId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the final synchronization execution status.
    /// </summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// Gets the UTC timestamp in which the synchronization started.
    /// </summary>
    public DateTime StartedAtUtc { get; init; }

    /// <summary>
    /// Gets the UTC timestamp in which the synchronization finished.
    /// </summary>
    public DateTime FinishedAtUtc { get; init; }

    /// <summary>
    /// Gets the total synchronization duration in milliseconds.
    /// </summary>
    public long DurationMs { get; init; }

    /// <summary>
    /// Gets a value indicating whether the execution satisfied its quality criteria.
    /// </summary>
    public bool QualityCriteriaSatisfied { get; init; }

    /// <summary>
    /// Gets the human-readable quality criteria applied to the execution.
    /// </summary>
    public string QualityCriteriaDescription { get; init; } = string.Empty;

    /// <summary>
    /// Gets the season reported by the Cartola market status endpoint.
    /// </summary>
    public int Season { get; init; }

    /// <summary>
    /// Gets the current round reported by the Cartola market status endpoint.
    /// </summary>
    public int CurrentRound { get; init; }

    /// <summary>
    /// Gets the round used to synchronize matches.
    /// </summary>
    public int MatchesRound { get; init; }

    /// <summary>
    /// Gets the athlete rounds affected by the synchronization.
    /// </summary>
    public IReadOnlyCollection<int> AthleteRounds { get; init; } = Array.Empty<int>();

    /// <summary>
    /// Gets the total number of club records available after the upsert stage.
    /// </summary>
    public int ClubsUpserted { get; init; }

    /// <summary>
    /// Gets the total number of position records available after the upsert stage.
    /// </summary>
    public int PositionsUpserted { get; init; }

    /// <summary>
    /// Gets the total number of athlete status records available after the upsert stage.
    /// </summary>
    public int StatusesUpserted { get; init; }

    /// <summary>
    /// Gets the total number of athlete records available after the upsert stage.
    /// </summary>
    public int AthletesUpserted { get; init; }

    /// <summary>
    /// Gets the number of match rows processed in the synchronization.
    /// </summary>
    public int MatchesUpserted { get; init; }

    /// <summary>
    /// Gets the number of athlete round rows processed in the synchronization.
    /// </summary>
    public int AthleteRoundsUpserted { get; init; }

    /// <summary>
    /// Gets the number of scout rows recreated for the synchronized athlete rounds.
    /// </summary>
    public int ScoutsUpserted { get; init; }

    /// <summary>
    /// Gets the number of existing local athlete rounds that were finalized with zero points because the closed Cartola payload did not contain them explicitly.
    /// </summary>
    public int AthleteRoundsMissingScorePayload { get; init; }

    /// <summary>
    /// Gets the number of final score payload rows that could not be mapped to a local athlete round baseline.
    /// </summary>
    public int ScorePayloadRowsWithoutLocalRound { get; init; }

    /// <summary>
    /// Gets additional execution notes that help interpret partial or historical synchronizations.
    /// </summary>
    public string Notes { get; init; } = string.Empty;
}
