using CartolaOptimization.Application.Models;

namespace CartolaOptimization.Application.Interfaces;

/// <summary>
/// Define a operação de busca de hiperparâmetros para o modelo de pontuação de atletas full-context v1.
/// Realiza busca aleatória com validação cruzada walk-forward para identificar os melhores hiperparâmetros
/// combinação de hiperparâmetros, e então opcionalmente retreina o modelo persistido com esses valores.
/// </summary>
public interface IFullContextAthleteScoreModelHyperparameterTuningService
{
    /// <summary>
    /// Executa uma otimização de hiperparâmetros por busca aleatória usando validação cruzada temporal walk-forward
    /// validação cruzada sobre as rodadas de treino disponíveis.
    /// </summary>
    /// <param name="fromRound">Rodada inicial opcional a incluir no conjunto de dados da busca.</param>
    /// <param name="toRound">Rodada final opcional a incluir no conjunto de dados da busca.</param>
    /// <param name="numberOfTrials">
    /// Número de combinações aleatórias de hiperparâmetros a avaliar. Mais trials melhoram a
    /// chance de encontrar o ótimo global mas aumentam o tempo de execução proporcionalmente.
    /// </param>
    /// <param name="validationFolds">
    /// Número de folds walk-forward. Cada fold usa todas as rodadas até o fold <em>k</em> para
    /// treino e a rodada <em>k+1</em> como holdout, avançando a partir do fim do conjunto de dados.
    /// Deve ser no mínimo 1 e menor que o número total de rodadas de treino distintas menos 1.
    /// </param>
    /// <param name="applyBest">
    /// Quando <c>true</c>, retreina e persiste o modelo full-context com a melhor
    /// combinação de hiperparâmetros encontrada, usando o mesmo ajuste <c>topNFeatures</c> configurado
    /// em <c>MachineLearning:FullContext:DefaultTopNFeatures</c>.
    /// </param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Resultados da busca, incluindo as pontuações de todos os trials, a melhor combinação e os metadados do modelo final.</returns>
    Task<HyperparameterSearchResult> SearchAsync(
        int? fromRound = null,
        int? toRound = null,
        int numberOfTrials = 30,
        int validationFolds = 3,
        bool applyBest = true,
        CancellationToken cancellationToken = default);
}
