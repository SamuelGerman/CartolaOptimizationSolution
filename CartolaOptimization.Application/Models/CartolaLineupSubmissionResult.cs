using System.Text.Json;

namespace CartolaOptimization.Application.Models;

/// <summary>
/// Represents the result of sending an optimized lineup to the authenticated Cartola team endpoint.
/// </summary>
public sealed class CartolaLineupSubmissionResult
{
    public bool Submitted { get; init; }

    public DateTime SubmittedAtUtc { get; init; }

    public int SchemeId { get; init; }

    public int CaptainAthleteCartolaId { get; init; }

    public int? ReserveLuxuryAthleteCartolaId { get; init; }

    public string Message { get; init; } = string.Empty;

    public IReadOnlyCollection<CartolaReserveSelection> ReserveSelections { get; init; } = [];

    public JsonElement? CartolaResponse { get; init; }
}
