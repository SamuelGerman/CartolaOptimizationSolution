using CartolaOptimization.Application.Models;

namespace CartolaOptimization.Application.Interfaces;

/// <summary>
/// Define operações de leitura que retornam previsões de pontuação por aprendizado de máquina de contexto de atleta para uma rodada-alvo.
/// </summary>
public interface IAthleteContextAthleteScorePredictionService
{
    Task<AthleteScorePredictionResult> GetPredictedScoresAsync(
        int round,
        int? statusCartolaId = null,
        int? clubCartolaId = null,
        int? positionCartolaId = null,
        int take = 100,
        CancellationToken cancellationToken = default);
}
