namespace CartolaOptimization.Application.Models;

public sealed class CartolaSyncResult
{
    public int Season { get; init; }

    public int CurrentRound { get; init; }

    public int MatchesRound { get; init; }

    public IReadOnlyCollection<int> AthleteRounds { get; init; } = Array.Empty<int>();

    public int ClubsUpserted { get; init; }

    public int PositionsUpserted { get; init; }

    public int StatusesUpserted { get; init; }

    public int AthletesUpserted { get; init; }

    public int MatchesUpserted { get; init; }

    public int AthleteRoundsUpserted { get; init; }

    public int ScoutsUpserted { get; init; }
}
