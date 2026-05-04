using System.Text.Json;
using CartolaOptimization.Application.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CartolaOptimization.ML;

/// <summary>
/// Manages on-disk artifacts (.zip model and .metadata.json) for the six Cartola position-specific
/// athlete-score models. Each position maps to a deterministic file-name pair derived from the
/// position code (gol, lat, zag, mei, ata, tec).
/// </summary>
public sealed class PositionalAthleteScoreModelArtifactStore(
    IHostEnvironment hostEnvironment,
    IOptions<PositionalAthleteScoreModelOptions> options)
{
    private readonly PositionalAthleteScoreModelOptions options = options.Value;

    private static readonly IReadOnlyDictionary<int, string> PositionCodes =
        new Dictionary<int, string>
        {
            [1] = "gol",
            [2] = "lat",
            [3] = "zag",
            [4] = "mei",
            [5] = "ata",
            [6] = "tec",
        };

    /// <summary>All position IDs for which this store can produce artifact paths.</summary>
    public IReadOnlyCollection<int> SupportedPositionIds => PositionCodes.Keys.ToArray();

    public string GetModelPath(int positionCartolaId)
        => Path.Combine(GetArtifactsDirectory(), GetModelFileName(positionCartolaId));

    public string GetMetadataPath(int positionCartolaId)
        => Path.Combine(GetArtifactsDirectory(), GetMetadataFileName(positionCartolaId));

    public bool ModelExists(int positionCartolaId)
        => File.Exists(GetModelPath(positionCartolaId)) && File.Exists(GetMetadataPath(positionCartolaId));

    internal async Task SaveMetadataAsync(
        int positionCartolaId,
        AthleteScoreModelArtifactMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(GetArtifactsDirectory());

        await using var stream = File.Create(GetMetadataPath(positionCartolaId));
        await JsonSerializer.SerializeAsync(stream, metadata, cancellationToken: cancellationToken);
    }

    internal async Task<AthleteScoreModelArtifactMetadata?> LoadMetadataAsync(
        int positionCartolaId,
        CancellationToken cancellationToken = default)
    {
        var metadataPath = GetMetadataPath(positionCartolaId);
        if (!File.Exists(metadataPath))
        {
            return null;
        }

        await using var stream = File.OpenRead(metadataPath);
        return await JsonSerializer.DeserializeAsync<AthleteScoreModelArtifactMetadata>(
            stream, cancellationToken: cancellationToken);
    }

    internal AthleteScoreModelInfo ToModelInfo(AthleteScoreModelArtifactMetadata metadata)
        => new()
        {
            ModelVersion = metadata.ModelVersion,
            DatasetKey = metadata.DatasetKey,
            TrainerName = metadata.TrainerName,
            TrainedAtUtc = metadata.TrainedAtUtc,
            FromRound = metadata.FromRound,
            ToRound = metadata.ToRound,
            ValidationRounds = metadata.ValidationRounds,
            TrainingRows = metadata.TrainingRows,
            ValidationRows = metadata.ValidationRows,
            TrainingDistinctRounds = metadata.TrainingDistinctRounds,
            ValidationDistinctRounds = metadata.ValidationDistinctRounds,
            MeanAbsoluteError = metadata.MeanAbsoluteError,
            RootMeanSquaredError = metadata.RootMeanSquaredError,
            RSquared = metadata.RSquared,
            ModelArtifactPath = metadata.ModelArtifactPath,
            MetadataArtifactPath = metadata.MetadataArtifactPath,
            PositionDiagnostics = metadata.PositionDiagnostics,
            TotalNumericFeatureCount = metadata.TotalNumericFeatureCount,
            ActiveNumericFeatureCount = metadata.ActiveNumericFeatureCount,
            FeatureImportances = metadata.FeatureImportances,
            Hyperparameters = metadata.Hyperparameters,
        };

    private static string GetModelFileName(int positionCartolaId)
        => $"athlete-score-v1-positional-{GetPositionCode(positionCartolaId)}.zip";

    private static string GetMetadataFileName(int positionCartolaId)
        => $"athlete-score-v1-positional-{GetPositionCode(positionCartolaId)}.metadata.json";

    private static string GetPositionCode(int positionCartolaId)
        => PositionCodes.TryGetValue(positionCartolaId, out var code) ? code : $"pos{positionCartolaId}";

    private string GetArtifactsDirectory()
        => Path.IsPathRooted(options.ArtifactsDirectory)
            ? options.ArtifactsDirectory
            : Path.Combine(hostEnvironment.ContentRootPath, options.ArtifactsDirectory);
}
