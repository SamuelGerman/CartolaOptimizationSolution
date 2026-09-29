using CartolaOptimization.Application.Models;

namespace CartolaOptimization.Application.Interfaces;

/// <summary>
/// Define operações de leitura que estimam a pontuação esperada de cada atleta para uma rodada-alvo.
/// </summary>
public interface IAthleteProjectionService
{
    Task<AthleteRoundProjectionResult> GetRoundProjectionAsync(
        int round,
        int? positionCartolaId = null,
        int? clubCartolaId = null,
        int? statusCartolaId = null,
        int take = 100,
        CancellationToken cancellationToken = default);
}
