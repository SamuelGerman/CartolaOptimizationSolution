namespace CartolaOptimization.Application.Models;

/// <summary>
/// Representa um único passo executado ou descartado durante o fluxo automatizado de manutenção de fim de rodada.
/// </summary>
public sealed class EndOfRoundMaintenanceStepResult
{
    public string Step { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string Reason { get; init; } = string.Empty;

    public IReadOnlyCollection<int> Rounds { get; init; } = Array.Empty<int>();
}
