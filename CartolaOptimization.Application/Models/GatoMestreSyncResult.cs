namespace CartolaOptimization.Application.Models;

/// <summary>
/// Represents the outcome of a Gato Mestre synchronization execution.
/// </summary>
public sealed class GatoMestreSyncResult
{
    /// <summary>
    /// Gets the synchronized season.
    /// </summary>
    public int Season { get; init; }

    /// <summary>
    /// Gets the synchronized round.
    /// </summary>
    public int Round { get; init; }

    /// <summary>
    /// Gets the total number of clubs available after the team catalog upsert stage.
    /// </summary>
    public int ClubsUpserted { get; init; }

    /// <summary>
    /// Gets the number of club round rows processed for the synchronized round.
    /// </summary>
    public int ClubRoundsUpserted { get; init; }

    /// <summary>
    /// Gets the number of club round position rows processed for the synchronized round.
    /// </summary>
    public int ClubRoundPositionsUpserted { get; init; }

    /// <summary>
    /// Gets the number of athlete round rows enriched or created for the synchronized round.
    /// </summary>
    public int AthleteRoundsUpserted { get; init; }

    /// <summary>
    /// Gets the number of athlete rows skipped because the available payloads were not sufficient to build a reliable round record.
    /// </summary>
    public int AthleteRoundsSkipped { get; init; }

    /// <summary>
    /// Gets the number of athlete detail requests required to complete missing round data.
    /// </summary>
    public int AthleteDetailsFetched { get; init; }

    /// <summary>
    /// Gets the number of warning logs persisted while handling non-fatal athlete enrichment issues.
    /// </summary>
    public int AthleteFailuresLogged { get; init; }

    /// <summary>
    /// Gets the athlete coverage percentage achieved during the synchronization.
    /// </summary>
    public decimal AthleteCoverageRate { get; init; }

    /// <summary>
    /// Gets a value indicating whether the team confrontation data was available for the requested round.
    /// </summary>
    public bool ClubRoundDataAvailable { get; init; }

    /// <summary>
    /// Gets a value indicating whether the synchronization completed with partial enrichment.
    /// </summary>
    public bool IsPartialSync { get; init; }

    /// <summary>
    /// Gets an optional note describing relevant execution details, such as endpoint limitations for historical confrontation data.
    /// </summary>
    public string Notes { get; init; } = string.Empty;
}
