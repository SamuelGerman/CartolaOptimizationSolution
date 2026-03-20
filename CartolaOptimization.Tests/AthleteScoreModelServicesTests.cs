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

    [Fact]
    public async Task GetPredictedScoresAsync_FallsBackToLatestPersistedBaseline_WhenCurrentRoundRowsDoNotExist()
    {
        var dbContextFactory = CreateDbContextFactory();
        var artifactDirectory = CreateArtifactDirectory();
        try
        {
            await SeedHistoricalOnlyTrainingDataAsync(await dbContextFactory.CreateDbContextAsync());

            var options = Options.Create(new AthleteScoreModelOptions
            {
                ArtifactsDirectory = artifactDirectory,
                ModelVersion = "athlete-score-v0",
            });
            var store = CreateArtifactStore(artifactDirectory);
            var trainingService = new AthleteScoreModelTrainingService(dbContextFactory, store, options);
            await trainingService.TrainAsync(fromRound: 1, toRound: 3, validationRounds: 1);

            var predictionService = new AthleteScorePredictionService(dbContextFactory, store);
            var result = await predictionService.GetPredictedScoresAsync(round: 4, take: 10);

            Assert.Equal(2, result.TotalAthletesConsidered);
            Assert.Equal(2, result.TotalAthletesPredicted);
            Assert.All(result.Athletes, item => Assert.Equal(3, item.BaselineRound));
            Assert.All(
                result.Athletes,
                item => Assert.Contains("latest persisted athlete baseline before round 4", item.PredictionNotes, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            DeleteArtifactDirectory(artifactDirectory);
        }
    }

    [Fact]
    public async Task GetOptimalLineupAsync_ReturnsBestProbableLineupWithinBudgetAndFormation()
    {
        var dbContextFactory = CreateDbContextFactory();
        var artifactDirectory = CreateArtifactDirectory();
        try
        {
            await SeedOptimizationTrainingDataAsync(await dbContextFactory.CreateDbContextAsync());

            var options = Options.Create(new AthleteScoreModelOptions
            {
                ArtifactsDirectory = artifactDirectory,
                ModelVersion = "athlete-score-v0",
            });
            var store = CreateArtifactStore(artifactDirectory);
            var trainingService = new AthleteScoreModelTrainingService(dbContextFactory, store, options);
            await trainingService.TrainAsync(fromRound: 1, toRound: 3, validationRounds: 1);

            var predictionService = new AthleteScorePredictionService(dbContextFactory, store);
            var optimizerService = new PredictedLineupOptimizationService(predictionService);

            var predictions = await predictionService.GetPredictedScoresAsync(round: 4, statusCartolaId: 7, take: 5000);
            var defenderCandidates = predictions.Athletes.Where(item => item.PositionCartolaId == 3).ToList();
            var strikerCandidates = predictions.Athletes.Where(item => item.PositionCartolaId == 5).ToList();
            var expected = (
                from defender in defenderCandidates
                from striker in strikerCandidates
                let totalCost = defender.PriceNum + striker.PriceNum
                where totalCost <= 20m
                let totalScore = defender.PredictedScore + striker.PredictedScore
                orderby totalScore descending, totalCost ascending
                select new
                {
                    AthleteIds = new[] { defender.AthleteCartolaId, striker.AthleteCartolaId }.OrderBy(item => item).ToArray(),
                    TotalCost = totalCost,
                    TotalScore = totalScore,
                })
                .First();

            var result = await optimizerService.GetOptimalLineupAsync(
                4,
                new PredictedLineupRequest
                {
                    Budget = 20m,
                    Formation =
                    [
                        new PredictedLineupPositionRequirement { PositionCartolaId = 3, Quantity = 1 },
                        new PredictedLineupPositionRequirement { PositionCartolaId = 5, Quantity = 1 },
                    ],
                });

            Assert.Equal(2, result.TotalPlayers);
            Assert.Equal(7, result.ProbableStatusCartolaId);
            Assert.All(result.Athletes, item => Assert.Equal(7, item.StatusCartolaId));
            Assert.Equal(expected.TotalCost, result.TotalCost);
            Assert.Equal(expected.TotalScore, result.TotalPredictedScore);
            Assert.Equal(expected.AthleteIds, result.Athletes.Select(item => item.AthleteCartolaId).OrderBy(item => item).ToArray());
        }
        finally
        {
            DeleteArtifactDirectory(artifactDirectory);
        }
    }

    [Fact]
    public async Task GetOptimalLineupAsync_Throws_WhenBudgetCannotFitRequestedFormation()
    {
        var dbContextFactory = CreateDbContextFactory();
        var artifactDirectory = CreateArtifactDirectory();
        try
        {
            await SeedOptimizationTrainingDataAsync(await dbContextFactory.CreateDbContextAsync());

            var options = Options.Create(new AthleteScoreModelOptions
            {
                ArtifactsDirectory = artifactDirectory,
                ModelVersion = "athlete-score-v0",
            });
            var store = CreateArtifactStore(artifactDirectory);
            var trainingService = new AthleteScoreModelTrainingService(dbContextFactory, store, options);
            await trainingService.TrainAsync(fromRound: 1, toRound: 3, validationRounds: 1);

            var predictionService = new AthleteScorePredictionService(dbContextFactory, store);
            var optimizerService = new PredictedLineupOptimizationService(predictionService);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => optimizerService.GetOptimalLineupAsync(
                4,
                new PredictedLineupRequest
                {
                    Budget = 5m,
                    Formation =
                    [
                        new PredictedLineupPositionRequirement { PositionCartolaId = 3, Quantity = 1 },
                        new PredictedLineupPositionRequirement { PositionCartolaId = 5, Quantity = 1 },
                    ],
                }));

            Assert.Contains("No feasible lineup", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteArtifactDirectory(artifactDirectory);
        }
    }

    [Fact]
    public async Task TrainAsync_PersistsFullContextModelArtifactAndMetadata()
    {
        var dbContextFactory = CreateDbContextFactory();
        var artifactDirectory = CreateArtifactDirectory();
        try
        {
            await SeedFullContextTrainingDataAsync(await dbContextFactory.CreateDbContextAsync(), includeCurrentRoundAthleteBaselines: true);

            var store = CreateFullContextArtifactStore(artifactDirectory);
            var service = new FullContextAthleteScoreModelTrainingService(
                dbContextFactory,
                store,
                Options.Create(new FullContextAthleteScoreModelOptions
                {
                    ArtifactsDirectory = artifactDirectory,
                    ModelVersion = "athlete-score-v1-full-context",
                }));

            var result = await service.TrainAsync(fromRound: 1, toRound: 3, validationRounds: 1);
            var metadata = await service.GetCurrentModelInfoAsync();

            Assert.Contains("trained successfully", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.NotNull(metadata);
            Assert.True(File.Exists(metadata!.ModelArtifactPath));
            Assert.True(File.Exists(metadata.MetadataArtifactPath));
            Assert.Equal("full-context", metadata.DatasetKey);
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
    public async Task GetPredictedScoresAsync_ReturnsFullContextPredictionsWithTargetRoundContext()
    {
        var dbContextFactory = CreateDbContextFactory();
        var artifactDirectory = CreateArtifactDirectory();
        try
        {
            await SeedFullContextTrainingDataAsync(await dbContextFactory.CreateDbContextAsync(), includeCurrentRoundAthleteBaselines: true);

            var store = CreateFullContextArtifactStore(artifactDirectory);
            var trainingService = new FullContextAthleteScoreModelTrainingService(
                dbContextFactory,
                store,
                Options.Create(new FullContextAthleteScoreModelOptions
                {
                    ArtifactsDirectory = artifactDirectory,
                    ModelVersion = "athlete-score-v1-full-context",
                }));
            await trainingService.TrainAsync(fromRound: 1, toRound: 3, validationRounds: 1);

            var predictionService = new FullContextAthleteScorePredictionService(dbContextFactory, store);
            var result = await predictionService.GetPredictedScoresAsync(
                round: 4,
                statusCartolaId: 7,
                clubCartolaId: 293,
                positionCartolaId: 3,
                take: 10);

            Assert.Equal(4, result.Round);
            Assert.Equal("athlete-score-v1-full-context", result.ModelVersion);
            Assert.Equal("full-context", result.DatasetKey);
            Assert.Equal(1, result.TotalAthletesConsidered);
            Assert.Equal(0, result.ExcludedAthletesWithoutContext);
            Assert.Single(result.Athletes);
            Assert.Contains("current pre-round athlete baseline", result.PredictionNotes, StringComparison.OrdinalIgnoreCase);

            var athlete = result.Athletes.Single();
            Assert.Equal(1001, athlete.AthleteCartolaId);
            Assert.Equal(262, athlete.OpponentClubCartolaId);
            Assert.Equal("Flamengo", athlete.OpponentClubName);
            Assert.True(athlete.IsHomeMatch);
            Assert.NotNull(athlete.ClubWinProbability);
            Assert.NotNull(athlete.OpponentWinProbability);
            Assert.NotEqual(0m, athlete.PredictedScore);
        }
        finally
        {
            DeleteArtifactDirectory(artifactDirectory);
        }
    }

    [Fact]
    public async Task GetPredictedScoresAsync_FullContextFallsBackToLatestPersistedBaseline_WhenCurrentRoundRowsDoNotExist()
    {
        var dbContextFactory = CreateDbContextFactory();
        var artifactDirectory = CreateArtifactDirectory();
        try
        {
            await SeedFullContextTrainingDataAsync(await dbContextFactory.CreateDbContextAsync(), includeCurrentRoundAthleteBaselines: false);

            var store = CreateFullContextArtifactStore(artifactDirectory);
            var trainingService = new FullContextAthleteScoreModelTrainingService(
                dbContextFactory,
                store,
                Options.Create(new FullContextAthleteScoreModelOptions
                {
                    ArtifactsDirectory = artifactDirectory,
                    ModelVersion = "athlete-score-v1-full-context",
                }));
            await trainingService.TrainAsync(fromRound: 1, toRound: 3, validationRounds: 1);

            var predictionService = new FullContextAthleteScorePredictionService(dbContextFactory, store);
            var result = await predictionService.GetPredictedScoresAsync(round: 4, take: 10);

            Assert.Equal(2, result.TotalAthletesConsidered);
            Assert.Equal(0, result.ExcludedAthletesWithoutContext);
            Assert.Equal(2, result.TotalAthletesPredicted);
            Assert.Contains("latest persisted athlete baselines before round 4", result.PredictionNotes, StringComparison.OrdinalIgnoreCase);
            Assert.All(result.Athletes, item => Assert.Equal(3, item.BaselineRound));
            Assert.All(result.Athletes, item => Assert.NotNull(item.OpponentClubCartolaId));
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

    private static FullContextAthleteScoreModelArtifactStore CreateFullContextArtifactStore(string artifactDirectory)
        => new(
            new FakeHostEnvironment(artifactDirectory),
            Options.Create(new FullContextAthleteScoreModelOptions
            {
                ArtifactsDirectory = artifactDirectory,
                ModelVersion = "athlete-score-v1-full-context",
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

    private static async Task SeedHistoricalOnlyTrainingDataAsync(AppDbContext dbContext)
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
            CreateHistoricalAthleteRound(3, athleteB.Id, clubB.Id, striker.Id, doubtful.Id, 5.9m, 5.7m, 13.5m, -0.3m, 5.4m));

        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedOptimizationTrainingDataAsync(AppDbContext dbContext)
    {
        await using var _ = dbContext;

        var clubA = new Club(0, 293, "CAP", "athletico-pr", "Athletico-PR", "https://example.com/cap.png");
        var clubB = new Club(0, 262, "FLA", "flamengo", "Flamengo", "https://example.com/fla.png");
        var clubC = new Club(0, 275, "PAL", "palmeiras", "Palmeiras", "https://example.com/pal.png");
        var defender = new Position(0, 3, "Zagueiro", "zag");
        var striker = new Position(0, 5, "Atacante", "ata");
        var probable = new AthleteStatus(0, 7, "Provavel");
        var doubtful = new AthleteStatus(0, 2, "Duvida");
        var athleteA = new Athlete(0, 1001, "Zagueiro A", "ZA");
        var athleteB = new Athlete(0, 1002, "Zagueiro B", "ZB");
        var athleteC = new Athlete(0, 1003, "Atacante A", "AA");
        var athleteD = new Athlete(0, 1004, "Atacante B", "AB");
        var athleteE = new Athlete(0, 1005, "Atacante C", "AC");

        dbContext.Clubs.AddRange(clubA, clubB, clubC);
        dbContext.Positions.AddRange(defender, striker);
        dbContext.AthleteStatuses.AddRange(probable, doubtful);
        dbContext.Athletes.AddRange(athleteA, athleteB, athleteC, athleteD, athleteE);
        await dbContext.SaveChangesAsync();

        dbContext.AthleteRounds.AddRange(
            CreateHistoricalAthleteRound(1, athleteA.Id, clubA.Id, defender.Id, probable.Id, 5.0m, 5.0m, 9.5m, 0.2m, 5.2m),
            CreateHistoricalAthleteRound(2, athleteA.Id, clubA.Id, defender.Id, probable.Id, 5.3m, 5.3m, 9.7m, 0.2m, 5.5m),
            CreateHistoricalAthleteRound(3, athleteA.Id, clubA.Id, defender.Id, probable.Id, 5.6m, 5.6m, 10.0m, 0.2m, 5.8m),
            CreateHistoricalAthleteRound(1, athleteB.Id, clubB.Id, defender.Id, probable.Id, 4.2m, 4.2m, 8.4m, 0.1m, 4.3m),
            CreateHistoricalAthleteRound(2, athleteB.Id, clubB.Id, defender.Id, probable.Id, 4.4m, 4.4m, 8.6m, 0.1m, 4.5m),
            CreateHistoricalAthleteRound(3, athleteB.Id, clubB.Id, defender.Id, probable.Id, 4.5m, 4.5m, 8.8m, 0.1m, 4.6m),
            CreateHistoricalAthleteRound(1, athleteC.Id, clubC.Id, striker.Id, probable.Id, 7.0m, 7.0m, 11.5m, 0.3m, 7.2m),
            CreateHistoricalAthleteRound(2, athleteC.Id, clubC.Id, striker.Id, probable.Id, 7.3m, 7.3m, 11.9m, 0.3m, 7.5m),
            CreateHistoricalAthleteRound(3, athleteC.Id, clubC.Id, striker.Id, probable.Id, 7.6m, 7.6m, 12.2m, 0.3m, 7.9m),
            CreateHistoricalAthleteRound(1, athleteD.Id, clubB.Id, striker.Id, probable.Id, 5.7m, 5.7m, 7.5m, 0.2m, 5.8m),
            CreateHistoricalAthleteRound(2, athleteD.Id, clubB.Id, striker.Id, probable.Id, 6.0m, 6.0m, 7.8m, 0.2m, 6.1m),
            CreateHistoricalAthleteRound(3, athleteD.Id, clubB.Id, striker.Id, probable.Id, 6.2m, 6.2m, 8.0m, 0.2m, 6.3m),
            CreateHistoricalAthleteRound(1, athleteE.Id, clubA.Id, striker.Id, doubtful.Id, 8.0m, 8.0m, 6.5m, 0.1m, 8.1m),
            CreateHistoricalAthleteRound(2, athleteE.Id, clubA.Id, striker.Id, doubtful.Id, 8.1m, 8.1m, 6.7m, 0.1m, 8.2m),
            CreateHistoricalAthleteRound(3, athleteE.Id, clubA.Id, striker.Id, doubtful.Id, 8.2m, 8.2m, 6.9m, 0.1m, 8.3m),
            new AthleteRound(0, 4, athleteA.Id, clubA.Id, defender.Id, probable.Id, 7, 4, 3, 0m, 5.9m, 5.9m, 6.1m, 5.5m, 0.1m, 10.3m, false)
            {
                DataPhase = RoundDataPhaseValues.CurrentOpen,
                Scouts = [],
            },
            new AthleteRound(0, 4, athleteB.Id, clubB.Id, defender.Id, probable.Id, 7, 4, 3, 0m, 4.7m, 4.7m, 4.9m, 4.4m, 0.1m, 8.9m, false)
            {
                DataPhase = RoundDataPhaseValues.CurrentOpen,
                Scouts = [],
            },
            new AthleteRound(0, 4, athleteC.Id, clubC.Id, striker.Id, probable.Id, 7, 4, 3, 0m, 7.8m, 7.8m, 8.0m, 7.5m, 0.2m, 12.5m, false)
            {
                DataPhase = RoundDataPhaseValues.CurrentOpen,
                Scouts = [],
            },
            new AthleteRound(0, 4, athleteD.Id, clubB.Id, striker.Id, probable.Id, 7, 4, 3, 0m, 6.4m, 6.4m, 6.6m, 6.0m, 0.2m, 8.2m, false)
            {
                DataPhase = RoundDataPhaseValues.CurrentOpen,
                Scouts = [],
            },
            new AthleteRound(0, 4, athleteE.Id, clubA.Id, striker.Id, doubtful.Id, 7, 4, 3, 0m, 8.4m, 8.4m, 8.6m, 8.1m, 0.1m, 7.1m, false)
            {
                DataPhase = RoundDataPhaseValues.CurrentOpen,
                Scouts = [],
            });

        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedFullContextTrainingDataAsync(AppDbContext dbContext, bool includeCurrentRoundAthleteBaselines)
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
            CreateHistoricalAthleteRound(3, athleteB.Id, clubB.Id, striker.Id, doubtful.Id, 5.9m, 5.7m, 13.5m, -0.3m, 5.4m));

        if (includeCurrentRoundAthleteBaselines)
        {
            dbContext.AthleteRounds.AddRange(
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
        }

        dbContext.Matches.AddRange(
            new Match(0, 10001, 1, new DateTime(2026, 3, 1), "Arena", true, clubA.Id, 6, "VVEED", 1, clubB.Id, 12, "DEEVV", 0),
            new Match(0, 10002, 2, new DateTime(2026, 3, 8), "Maracanã", true, clubB.Id, 11, "DEEVV", 2, clubA.Id, 5, "VVEED", 1),
            new Match(0, 10003, 3, new DateTime(2026, 3, 15), "Arena", true, clubA.Id, 4, "VVVED", 1, clubB.Id, 13, "DDEEV", 1),
            new Match(0, 10004, 4, new DateTime(2026, 3, 22), "Arena", true, clubA.Id, 3, "VVVEE", null, clubB.Id, 14, "DDEED", null));

        await dbContext.SaveChangesAsync();

        var clubRounds = new List<ClubRound>();
        for (var round = 1; round <= 4; round++)
        {
            clubRounds.Add(new ClubRound(
                0,
                round,
                clubA.Id,
                0.48m + (round * 0.01m),
                0.24m,
                0.28m - (round * 0.01m),
                6.2m + round,
                6.0m + round,
                1.8m + (round * 0.1m)));
            clubRounds.Add(new ClubRound(
                0,
                round,
                clubB.Id,
                0.30m,
                0.27m,
                0.43m,
                4.8m + round,
                4.6m + round,
                1.2m + (round * 0.05m)));
        }

        dbContext.ClubRounds.AddRange(clubRounds);
        await dbContext.SaveChangesAsync();

        foreach (var clubRound in clubRounds)
        {
            var isClubA = clubRound.ClubId == clubA.Id;
            dbContext.ClubRoundPositions.AddRange(
                new ClubRoundPosition(
                    0,
                    clubRound.Id,
                    defender.Id,
                    isClubA ? 3.2m + clubRound.Round : 5.1m + clubRound.Round,
                    isClubA ? 5.4m + clubRound.Round : 3.6m + clubRound.Round),
                new ClubRoundPosition(
                    0,
                    clubRound.Id,
                    striker.Id,
                    isClubA ? 4.0m + clubRound.Round : 4.8m + clubRound.Round,
                    isClubA ? 6.3m + clubRound.Round : 4.2m + clubRound.Round));
        }

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
