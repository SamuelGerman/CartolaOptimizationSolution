namespace CartolaOptimization.Application.Models;

/// <summary>
/// Represents per-position training, validation and calibration diagnostics for a trained athlete-score model.
/// </summary>
public sealed class AthleteScorePositionDiagnostics
{
    public int PositionCartolaId { get; init; }

    public string PositionCode { get; init; } = string.Empty;

    public string PositionName { get; init; } = string.Empty;

    public int TrainingRows { get; init; }

    public int ValidationRows { get; init; }

    public double TrainingMeanScore { get; init; }

    public double ValidationMeanActualScore { get; init; }

    public double ValidationMeanPredictedScore { get; init; }

    public double ValidationMeanPredictedScoreCalibrated { get; init; }

    public double ValidationMeanAbsoluteError { get; init; }

    public double ValidationRootMeanSquaredError { get; init; }

    public double ValidationBias { get; init; }

    public double CalibrationSlope { get; init; }

    public double CalibrationOffset { get; init; }
}
