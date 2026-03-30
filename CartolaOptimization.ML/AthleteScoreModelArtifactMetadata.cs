namespace CartolaOptimization.ML;

internal sealed class AthleteScoreModelArtifactMetadata
{
    public string ModelVersion { get; init; } = string.Empty;

    public string DatasetKey { get; init; } = "athlete-only";

    public string TrainerName { get; init; } = string.Empty;

    public DateTime TrainedAtUtc { get; init; }

    public int? FromRound { get; init; }

    public int? ToRound { get; init; }

    public int ValidationRounds { get; init; }

    public int TrainingRows { get; init; }

    public int ValidationRows { get; init; }

    public int TrainingDistinctRounds { get; init; }

    public int ValidationDistinctRounds { get; init; }

    public double MeanAbsoluteError { get; init; }

    public double RootMeanSquaredError { get; init; }

    public double RSquared { get; init; }

    public string ModelArtifactPath { get; init; } = string.Empty;

    public string MetadataArtifactPath { get; init; } = string.Empty;
}
