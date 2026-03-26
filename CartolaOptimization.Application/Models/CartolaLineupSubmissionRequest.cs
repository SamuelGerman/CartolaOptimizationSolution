namespace CartolaOptimization.Application.Models;

/// <summary>
/// Represents the optional request that submits an optimized lineup to the authenticated Cartola team endpoint.
/// </summary>
public sealed class CartolaLineupSubmissionRequest
{
    public bool SaveToCartola { get; init; }

    public int? CaptainAthleteCartolaId { get; init; }

    public bool IncludeReserves { get; init; } = true;

    public IReadOnlyCollection<CartolaReserveSelection>? ReserveOverrides { get; init; }

    public int? ReserveLuxuryAthleteCartolaId { get; init; }
}
