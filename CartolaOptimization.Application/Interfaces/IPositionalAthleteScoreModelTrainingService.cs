using CartolaOptimization.Application.Models;

namespace CartolaOptimization.Application.Interfaces;

/// <summary>
/// Trains and loads a separate LightGBM athlete-score model for each Cartola position
/// (GOL, LAT, ZAG, MEI, ATA, TEC). Each model uses the same full-context feature set
/// but is trained exclusively on athletes of its own position.
/// </summary>
public interface IPositionalAthleteScoreModelTrainingService
{
    /// <summary>
    /// Trains one model per supported Cartola position. Positions with fewer rows than the
    /// configured minimum threshold are skipped instead of producing an underfitted artifact.
    /// </summary>
    Task<PositionalAthleteScoreTrainingResult> TrainAllAsync(
        int? fromRound = null,
        int? toRound = null,
        int validationRounds = 1,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the metadata for every persisted positional model artifact, ordered by
    /// <c>PositionCartolaId</c>. Positions whose artifact does not exist are omitted.
    /// </summary>
    Task<IReadOnlyList<AthleteScoreModelInfo>> GetAllModelInfosAsync(CancellationToken cancellationToken = default);
}
