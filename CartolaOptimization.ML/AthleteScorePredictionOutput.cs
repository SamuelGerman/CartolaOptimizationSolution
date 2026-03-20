using Microsoft.ML.Data;

namespace CartolaOptimization.ML;

internal sealed class AthleteScorePredictionOutput
{
    [ColumnName("Score")]
    public float Score { get; init; }
}
