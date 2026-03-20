using CartolaOptimization.Application.Models;
using CartolaOptimization.Domain.Constants;
using CartolaOptimization.Domain.Entities;
using CartolaOptimization.Infrastructure.Context;
using CartolaOptimization.ML;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

namespace CartolaOptimization.Tests;

public sealed class AthleteScoreModelServicesTests
{
    [Fact]
    public async Task TrainAsync_PersistsModelArtifactAndMetadata()
    {
        var dbContextFactory = CreateDbContextFactory();
        var artifactDirectory = CreateArtifactDirectory();
        try
        {
            await SeedAthleteScoreTrainingDataAsync(await dbContextFactory.CreateDbContextAsync());

            var store = CreateArtifactStore(artifactDirectory);
            var service = new AthleteScoreModelTrainingService(
                dbContextFactory,
                store,
                Options.Create(new AthleteScoreModelOptions
                {
                    ArtifactsDirectory = artifactDirectory,
                    ModelVersion = "athlete-score-v0",
                }));

            var result = await service.TrainAsync(fromRound: 1, toRound: 3, validationRounds: 1);
            var metadata = await service.GetCurrentModelInfoAsync();

            Assert.Contains("trained successfully", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.NotNull(metadata);
            Assert.True(File.Exists(metadata!.ModelArtifactPath));
            Assert.True(File.Exists(metadata.MetadataArtifactPath));
            Assert.Equal(4, metadata.TrainingRows);
            Assert.Equal(2, metadata.ValidationRows);
            Assert.Equal(2, metadata.TrainingDistinctRounds);
            Assert.Equal(1, metadata.ValidationDistinctRounds);
        }
        finally
        {
            DeleteArtifactDirectory(artifactDirectory);
        }
    }

    [Fact]
    public async Task GetPredictedScoresAsync_ReturnsPredictionsFilteredByCurrentStatusClubAndPosition()
    {
        var dbContextFactory = CreateDbContextFactory();
        var artifactDirectory = CreateArtifactDirectory();
        try
        {
            await SeedAthleteScoreTrainingDataAsync(await dbContextFactory.CreateDbContextAsync());

            var options = Options.Create(new AthleteScoreModelOptions
            {
                ArtifactsDirectory = artifactDirectory,
                ModelVersion = "athlete-score-v0",
            });
            var store = CreateArtifactStore(artifactDirectory);
            var trainingService = new AthleteScoreModelTrainingService(dbContextFactory, store, options);
            await trainingService.TrainAsync(fromRound: 1, toRound: 3, validationRounds: 1);

            var predictionService = new AthleteScorePredictionService(dbContextFactory, store);
            var result = await predictionService.GetPredictedScoresAsync(
                round: 4,
                statusCartolaId: 7,
                clubCartolaId: 293,
                positionCartolaId: 3,
                take: 10);

            Assert.Equal(4, result.Round);
            Assert.Equal("athlete-score-v0", result.ModelVersion);
            Assert.Equal(1, result.TotalAthletesConsidered);
            Assert.Single(result.Athletes);
            Assert.Equal(1001, result.Athletes.Single().AthleteCartolaId);
            Assert.Equal(293, result.Athletes.Single().ClubCartolaId);
            Assert.Equal(3, result.Athletes.Single().PositionCartolaId);
            Assert.Equal(7, result.Athletes.Single().StatusCartolaId);
            Assert.NotEqual(0m, result.Athletes.Single().PredictedScore);
        }
        finally
        {
            DeleteArtifactDirectory(artifactDirectory);
        }
    }

    private static AthleteScoreModelArtifactStore CreateArtifactStore(string artifactDirectory)
        => new(
            new FakeHostEnvironment(artifactDirectory),
            Options.Create(new AthleteScoreModelOptions
            {
                ArtifactsDirectory = artifactDirectory,
                ModelVersion = "athlete-score-v0",
            }));

    private static SharedContextFactory CreateDbContextFactory()
    {
        var databaseRoot = new InMemoryDatabaseRoot();
        var databaseName = Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName, databaseRoot)
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new SharedContextFactory(options);
    }

    private static async Task SeedAthleteScoreTrainingDataAsync(AppDbContext dbContext)
    {
        await using var _ = dbContext;

        var clubA = new Club(0, 293, "CAP", "athletico-pr", "Athletico-PR", "https://example.com/cap.png");
        var clubB = new Club(0, 262, "FLA", "flamengo", "Flamengo", "https://example.com/fla.png");
        var defender = new Position(0, 3, "Zagueiro", "zag");
        var striker = new Position(0, 5, "Atacante", "ata");
        var probable = new AthleteStatus(0, 7, "Provavel");
        var doubtful = new AthleteStatus(0, 2, "Duvida");
        var athleteA = new Athlete(0, 1001, "Atleta A", "A");
        var athleteB = new Athlete(0, 1002, "Atleta B", "B");

        dbContext.Clubs.AddRange(clubA, clubB);
        dbContext.Positions.AddRange(defender, striker);
        dbContext.AthleteStatuses.AddRange(probable, doubtful);
        dbContext.Athletes.AddRange(athleteA, athleteB);
        await dbContext.SaveChangesAsync();

        dbContext.AthleteRounds.AddRange(
            CreateHistoricalAthleteRound(1, athleteA.Id, clubA.Id, defender.Id, probable.Id, 4.1m, 4.0m, 10m, 0.2m, 4.5m),
            CreateHistoricalAthleteRound(2, athleteA.Id, clubA.Id, defender.Id, probable.Id, 4.6m, 4.3m, 10.4m, 0.3m, 5.0m),
            CreateHistoricalAthleteRound(3, athleteA.Id, clubA.Id, defender.Id, probable.Id, 5.0m, 4.8m, 10.8m, 0.4m, 5.4m),
            CreateHistoricalAthleteRound(1, athleteB.Id, clubB.Id, striker.Id, doubtful.Id, 6.5m, 6.2m, 14m, -0.1m, 6.1m),
            CreateHistoricalAthleteRound(2, athleteB.Id, clubB.Id, striker.Id, doubtful.Id, 6.2m, 6.0m, 13.8m, -0.2m, 5.8m),
            CreateHistoricalAthleteRound(3, athleteB.Id, clubB.Id, striker.Id, doubtful.Id, 5.9m, 5.7m, 13.5m, -0.3m, 5.4m),
            new AthleteRound(0, 4, athleteA.Id, clubA.Id, defender.Id, probable.Id, 7, 4, 3, 0m, 5.2m, 5.0m, 5.6m, 4.4m, 0.2m, 11.1m, false)
            {
                DataPhase = RoundDataPhaseValues.CurrentOpen,
                Scouts = [],
            },
            new AthleteRound(0, 4, athleteB.Id, clubB.Id, striker.Id, doubtful.Id, 7, 3, 4, 0m, 5.7m, 5.5m, 6.0m, 5.1m, -0.2m, 13.1m, false)
            {
                DataPhase = RoundDataPhaseValues.CurrentOpen,
                Scouts = [],
            });

        await dbContext.SaveChangesAsync();
    }

    private static AthleteRound CreateHistoricalAthleteRound(
        int round,
        int athleteId,
        int clubId,
        int positionId,
        int statusId,
        decimal movingAverage,
        decimal averageScore,
        decimal priceNum,
        decimal variationNum,
        decimal scoreNum)
        => new(0, round, athleteId, clubId, positionId, statusId, 6, 3, 3, scoreNum, movingAverage, averageScore, averageScore + 0.4m, averageScore - 0.4m, variationNum, priceNum, true)
        {
            DataPhase = RoundDataPhaseValues.HistoricalClosed,
            Scouts = [],
        };

    private static string CreateArtifactDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "cartola-ml-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteArtifactDirectory(string artifactDirectory)
    {
        if (Directory.Exists(artifactDirectory))
        {
            Directory.Delete(artifactDirectory, recursive: true);
        }
    }

    private sealed class SharedContextFactory(DbContextOptions<AppDbContext> options) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(options);

        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }
}
