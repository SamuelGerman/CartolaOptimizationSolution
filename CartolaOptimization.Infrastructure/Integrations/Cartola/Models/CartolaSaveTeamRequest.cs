namespace CartolaOptimization.Infrastructure.Integrations.Cartola.Models;

public sealed class CartolaSaveTeamRequest
{
    public IReadOnlyCollection<int> Atletas { get; init; } = [];

    public int Capitao { get; init; }

    public int Esquema { get; init; }

    public IReadOnlyDictionary<int, int>? Reservas { get; init; }

    public int? ReservaLuxoId { get; init; }
}
