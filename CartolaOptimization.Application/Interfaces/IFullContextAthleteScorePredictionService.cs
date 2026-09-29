using CartolaOptimization.Application.Models;

namespace CartolaOptimization.Application.Interfaces;

/// <summary>
/// Define operações de leitura que retornam previsões de pontuação de atletas por aprendizado de máquina full-context para uma rodada-alvo.
/// </summary>
public interface IFullContextAthleteScorePredictionService
{
    Task<AthleteScorePredictionResult> GetPredictedScoresAsync(
        int round,
        int? statusCartolaId = null,
        int? clubCartolaId = null,
        int? positionCartolaId = null,
        int take = 100,
        CancellationToken cancellationToken = default);
}
