using System.Text.Json;
using CartolaOptimization.Application.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CartolaOptimization.ML;

public sealed class FullContextAthleteScoreModelArtifactStore(
    IHostEnvironment hostEnvironment,
    IOptions<FullContextAthleteScoreModelOptions> options)
{
    private readonly FullContextAthleteScoreModelOptions options = options.Value;

    public string GetModelPath()
        => Path.Combine(GetArtifactsDirectory(), options.ModelFileName);

    public string GetMetadataPath()
        => Path.Combine(GetArtifactsDirectory(), options.MetadataFileName);

    public bool ModelExists()
        => File.Exists(GetModelPath()) && File.Exists(GetMetadataPath());

    internal async Task SaveMetadataAsync(AthleteScoreModelArtifactMetadata metadata, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(GetArtifactsDirectory());

        await using var stream = File.Create(GetMetadataPath());
        await JsonSerializer.SerializeAsync(stream, metadata, cancellationToken: cancellationToken);
    }

    internal async Task<AthleteScoreModelArtifactMetadata?> LoadMetadataAsync(CancellationToken cancellationToken = default)
    {
        var metadataPath = GetMetadataPath();
        if (!File.Exists(metadataPath))
        {
            return null;
        }

        await using var stream = File.OpenRead(metadataPath);
        return await JsonSerializer.DeserializeAsync<AthleteScoreModelArtifactMetadata>(stream, cancellationToken: cancellationToken);
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
        };

    private string GetArtifactsDirectory()
        => Path.IsPathRooted(options.ArtifactsDirectory)
            ? options.ArtifactsDirectory
            : Path.Combine(hostEnvironment.ContentRootPath, options.ArtifactsDirectory);
}
