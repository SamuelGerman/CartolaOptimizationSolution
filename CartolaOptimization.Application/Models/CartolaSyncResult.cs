namespace CartolaOptimization.Application.Models;

/// <summary>
/// Represents the outcome of a Cartola synchronization execution.
/// </summary>
public sealed class CartolaSyncResult
{
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
}
