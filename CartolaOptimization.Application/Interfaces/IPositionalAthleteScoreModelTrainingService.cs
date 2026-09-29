using CartolaOptimization.Application.Models;

namespace CartolaOptimization.Application.Interfaces;

/// <summary>
/// Treina e carrega um modelo LightGBM de pontuação de atletas separado para cada posição do Cartola
/// (GOL, LAT, ZAG, MEI, ATA, TEC). Cada modelo usa o mesmo conjunto de features full-context
/// mas é treinado exclusivamente com atletas da sua própria posição.
/// </summary>
public interface IPositionalAthleteScoreModelTrainingService
{
    /// <summary>
    /// Treina um modelo por posição suportada do Cartola. Posições com menos linhas que o
    /// limiar mínimo configurado são descartadas em vez de produzir um artefato subajustado.
    /// </summary>
    Task<PositionalAthleteScoreTrainingResult> TrainAllAsync(
        int? fromRound = null,
        int? toRound = null,
        int validationRounds = 1,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retorna os metadados de cada artefato de modelo posicional persistido, ordenados por
    /// <c>PositionCartolaId</c>. Posições cujo artefato não existe são omitidas.
    /// </summary>
    Task<IReadOnlyList<AthleteScoreModelInfo>> GetAllModelInfosAsync(CancellationToken cancellationToken = default);
}
