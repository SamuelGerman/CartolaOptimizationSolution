using CartolaOptimization.Application.Interfaces;
using CartolaOptimization.Application.Models;
using CartolaOptimization.Domain.Constants;
using CartolaOptimization.Domain.Entities;
using CartolaOptimization.Infrastructure.Context;
using CartolaOptimization.ML;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Text.Json.Nodes;

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
            var optimizerService = new PredictedLineupOptimizationService(
                predictionService,
                new StubAthleteContextAthleteScorePredictionService(),
                new StubFullContextAthleteScorePredictionService(new AthleteScorePredictionResult()));

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
            var optimizerService = new PredictedLineupOptimizationService(
                predictionService,
                new StubAthleteContextAthleteScorePredictionService(),
                new StubFullContextAthleteScorePredictionService(new AthleteScorePredictionResult()));

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
    public async Task GetOptimalAthleteContextLineupAsync_ReturnsBestProbableLineupWithinBudgetAndFormation()
    {
        var dbContextFactory = CreateDbContextFactory();
        var artifactDirectory = CreateArtifactDirectory();
        try
        {
            await SeedAthleteContextTrainingDataAsync(await dbContextFactory.CreateDbContextAsync(), includeCurrentRoundAthleteBaselines: true);

            var options = Options.Create(new AthleteContextAthleteScoreModelOptions
            {
                ArtifactsDirectory = artifactDirectory,
                ModelVersion = "athlete-score-v1-athlete-context",
            });
            var store = CreateAthleteContextArtifactStore(artifactDirectory);
            var trainingService = new AthleteContextAthleteScoreModelTrainingService(dbContextFactory, store, options);
            await trainingService.TrainAsync(fromRound: 1, toRound: 3, validationRounds: 1);

            var athleteScoreOptions = Options.Create(new AthleteScoreModelOptions
            {
                ArtifactsDirectory = artifactDirectory,
                ModelVersion = "athlete-score-v0",
            });
            var athleteScoreStore = CreateArtifactStore(artifactDirectory);
            var athleteScoreTrainingService = new AthleteScoreModelTrainingService(dbContextFactory, athleteScoreStore, athleteScoreOptions);
            await athleteScoreTrainingService.TrainAsync(fromRound: 1, toRound: 3, validationRounds: 1);

            var athleteScorePredictionService = new AthleteScorePredictionService(dbContextFactory, athleteScoreStore);
            var athleteContextPredictionService = new AthleteContextAthleteScorePredictionService(dbContextFactory, store);
            var optimizerService = new PredictedLineupOptimizationService(
                athleteScorePredictionService,
                athleteContextPredictionService,
                new StubFullContextAthleteScorePredictionService(new AthleteScorePredictionResult()));

            var predictions = await athleteContextPredictionService.GetPredictedScoresAsync(round: 4, statusCartolaId: 7, take: 5000);
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

            var result = await optimizerService.GetOptimalAthleteContextLineupAsync(
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

            Assert.Equal("athlete-context", result.DatasetKey);
            Assert.Equal(2, result.TotalPlayers);
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
    public async Task GetOptimalAthleteContextLineupAsync_AutoSelectsBestSupportedFormation_WhenFormationIsOmitted()
    {
        var predictions = new AthleteScorePredictionResult
        {
            Round = 9,
            DatasetKey = "athlete-context",
            ModelVersion = "athlete-score-v1-athlete-context",
            ModelTrainedAtUtc = DateTime.UtcNow,
            ValidationMeanAbsoluteError = 1.11,
            ValidationRootMeanSquaredError = 1.66,
            TotalAthletesConsidered = 16,
            TotalAthletesPredicted = 16,
            Athletes =
            [
                CreatePredictionItem(1, "Goleiro A", 1, 5m, 5m),
                CreatePredictionItem(2, "Lateral A", 2, 6m, 7m),
                CreatePredictionItem(3, "Lateral B", 2, 6m, 6m),
                CreatePredictionItem(4, "Zagueiro A", 3, 4m, 8m),
                CreatePredictionItem(5, "Zagueiro B", 3, 4m, 7m),
                CreatePredictionItem(6, "Zagueiro C", 3, 4m, 6m),
                CreatePredictionItem(7, "Meia A", 4, 5m, 9m),
                CreatePredictionItem(8, "Meia B", 4, 5m, 8m),
                CreatePredictionItem(9, "Meia C", 4, 5m, 7m),
                CreatePredictionItem(10, "Meia D", 4, 5m, 6m),
                CreatePredictionItem(11, "Meia E", 4, 5m, 5m),
                CreatePredictionItem(12, "Atacante A", 5, 6m, 10m),
                CreatePredictionItem(13, "Atacante B", 5, 6m, 9m),
                CreatePredictionItem(14, "Atacante C", 5, 6m, 8m),
                CreatePredictionItem(15, "Tecnico A", 6, 1m, 1m),
                CreatePredictionItem(16, "Tecnico B", 6, 4m, 0.5m),
            ],
        };

        var optimizerService = new PredictedLineupOptimizationService(
            new StubAthleteScorePredictionService(predictions),
            new ConfigurableAthleteContextAthleteScorePredictionService(predictions),
            new StubFullContextAthleteScorePredictionService(predictions));

        var result = await optimizerService.GetOptimalAthleteContextLineupAsync(
            9,
            new PredictedLineupRequest
            {
                Budget = 60m,
            });

        Assert.Equal("athlete-context", result.DatasetKey);
        Assert.Equal(12, result.TotalPlayers);
        Assert.Equal(60m, result.Budget);
        Assert.Equal(59m, result.TotalCost);
        Assert.Equal(85m, result.TotalPredictedScore);
        Assert.Contains("automatically selected", result.OptimizationNotes, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            [1, 2, 2, 3, 3, 1],
            result.Formation
                .OrderBy(item => item.PositionCartolaId)
                .Select(item => item.Quantity)
                .ToArray());
    }

    [Fact]
    public async Task GetOptimalFullContextLineupAsync_ReturnsBestProbableLineupWithinBudgetAndFormation()
    {
        var predictions = new AthleteScorePredictionResult
        {
            Round = 9,
            DatasetKey = "full-context",
            ModelVersion = "athlete-score-v1-full-context",
            ModelTrainedAtUtc = DateTime.UtcNow,
            ValidationMeanAbsoluteError = 1.01,
            ValidationRootMeanSquaredError = 1.55,
            TotalAthletesConsidered = 6,
            TotalAthletesPredicted = 6,
            Athletes =
            [
                CreatePredictionItem(1, "Zagueiro A", 3, 7m, 8m),
                CreatePredictionItem(2, "Zagueiro B", 3, 6m, 7m),
                CreatePredictionItem(3, "Atacante A", 5, 9m, 10m),
                CreatePredictionItem(4, "Atacante B", 5, 8m, 8.5m),
                CreatePredictionItem(5, "Atacante C", 5, 5m, 4m),
                CreatePredictionItem(6, "Zagueiro C", 3, 5m, 5.5m),
            ],
        };

        var optimizerService = new PredictedLineupOptimizationService(
            new StubAthleteScorePredictionService(new AthleteScorePredictionResult()),
            new ConfigurableAthleteContextAthleteScorePredictionService(new AthleteScorePredictionResult()),
            new StubFullContextAthleteScorePredictionService(predictions));

        var result = await optimizerService.GetOptimalFullContextLineupAsync(
            9,
            new PredictedLineupRequest
            {
                Budget = 15m,
                Formation =
                [
                    new PredictedLineupPositionRequirement { PositionCartolaId = 3, Quantity = 1 },
                    new PredictedLineupPositionRequirement { PositionCartolaId = 5, Quantity = 1 },
                ],
            });

        Assert.Equal("full-context", result.DatasetKey);
        Assert.Equal("athlete-score-v1-full-context", result.ModelVersion);
        Assert.Equal(2, result.TotalPlayers);
        Assert.Equal(15m, result.TotalCost);
        Assert.Equal(17m, result.TotalPredictedScore);
        Assert.Equal([2, 3], result.Athletes.Select(item => item.AthleteCartolaId).OrderBy(item => item).ToArray());
    }

    [Fact]
    public async Task TrainAsync_PersistsAthleteContextModelArtifactAndMetadata()
    {
        var dbContextFactory = CreateDbContextFactory();
        var artifactDirectory = CreateArtifactDirectory();
        try
        {
            await SeedAthleteContextTrainingDataAsync(await dbContextFactory.CreateDbContextAsync(), includeCurrentRoundAthleteBaselines: true);

            var store = CreateAthleteContextArtifactStore(artifactDirectory);
            var service = new AthleteContextAthleteScoreModelTrainingService(
                dbContextFactory,
                store,
                Options.Create(new AthleteContextAthleteScoreModelOptions
                {
                    ArtifactsDirectory = artifactDirectory,
                    ModelVersion = "athlete-score-v1-athlete-context",
                }));

            var result = await service.TrainAsync(fromRound: 1, toRound: 3, validationRounds: 1);
            var metadata = await service.GetCurrentModelInfoAsync();

            Assert.Contains("trained successfully", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.NotNull(metadata);
            Assert.True(File.Exists(metadata!.ModelArtifactPath));
            Assert.True(File.Exists(metadata.MetadataArtifactPath));
            Assert.Equal("athlete-context", metadata.DatasetKey);
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
    public async Task GetPredictedScoresAsync_ReturnsAthleteContextPredictionsWithTargetRoundContext()
    {
        var dbContextFactory = CreateDbContextFactory();
        var artifactDirectory = CreateArtifactDirectory();
        try
        {
            await SeedAthleteContextTrainingDataAsync(await dbContextFactory.CreateDbContextAsync(), includeCurrentRoundAthleteBaselines: true);

            var store = CreateAthleteContextArtifactStore(artifactDirectory);
            var trainingService = new AthleteContextAthleteScoreModelTrainingService(
                dbContextFactory,
                store,
                Options.Create(new AthleteContextAthleteScoreModelOptions
                {
                    ArtifactsDirectory = artifactDirectory,
                    ModelVersion = "athlete-score-v1-athlete-context",
                }));
            await trainingService.TrainAsync(fromRound: 1, toRound: 3, validationRounds: 1);

            var predictionService = new AthleteContextAthleteScorePredictionService(dbContextFactory, store);
            var result = await predictionService.GetPredictedScoresAsync(
                round: 4,
                statusCartolaId: 7,
                clubCartolaId: 293,
                positionCartolaId: 3,
                take: 10);

            Assert.Equal(4, result.Round);
            Assert.Equal("athlete-score-v1-athlete-context", result.ModelVersion);
            Assert.Equal("athlete-context", result.DatasetKey);
            Assert.Equal(1, result.TotalAthletesConsidered);
            Assert.Equal(0, result.ExcludedAthletesWithoutContext);
            Assert.Single(result.Athletes);
            Assert.Contains("current pre-round athlete baseline", result.PredictionNotes, StringComparison.OrdinalIgnoreCase);

            var athlete = result.Athletes.Single();
            Assert.Equal(1001, athlete.AthleteCartolaId);
            Assert.Equal(262, athlete.OpponentClubCartolaId);
            Assert.Equal("Flamengo", athlete.OpponentClubName);
            Assert.True(athlete.IsHomeMatch);
            Assert.NotEqual(0m, athlete.PredictedScore);
        }
        finally
        {
            DeleteArtifactDirectory(artifactDirectory);
        }
    }

    [Fact]
    public async Task GetPredictedScoresAsync_AthleteContextFallsBackToLatestPersistedBaseline_WhenCurrentRoundRowsDoNotExist()
    {
        var dbContextFactory = CreateDbContextFactory();
        var artifactDirectory = CreateArtifactDirectory();
        try
        {
            await SeedAthleteContextTrainingDataAsync(await dbContextFactory.CreateDbContextAsync(), includeCurrentRoundAthleteBaselines: false);

            var store = CreateAthleteContextArtifactStore(artifactDirectory);
            var trainingService = new AthleteContextAthleteScoreModelTrainingService(
                dbContextFactory,
                store,
                Options.Create(new AthleteContextAthleteScoreModelOptions
                {
                    ArtifactsDirectory = artifactDirectory,
                    ModelVersion = "athlete-score-v1-athlete-context",
                }));
            await trainingService.TrainAsync(fromRound: 1, toRound: 3, validationRounds: 1);

            var predictionService = new AthleteContextAthleteScorePredictionService(dbContextFactory, store);
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
                    DefaultTopNFeatures = null, // use all features in unit tests for determinism
                }),
                NullLogger<FullContextAthleteScoreModelTrainingService>.Instance);

            var result = await service.TrainAsync(fromRound: 1, toRound: 3, validationRounds: 1);
            var metadata = await service.GetCurrentModelInfoAsync();

            Assert.Contains("trained successfully", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.NotNull(metadata);
            Assert.True(File.Exists(metadata!.ModelArtifactPath));
            Assert.True(File.Exists(metadata.MetadataArtifactPath));
            Assert.Equal("full-context", metadata.DatasetKey);
            Assert.Equal("LightGBM", metadata.TrainerName);
            Assert.Equal(6, metadata.TrainingRows);           // walk-forward CV: final model trains on all rows
            Assert.Equal(2, metadata.ValidationRows);
            Assert.Equal(3, metadata.TrainingDistinctRounds); // walk-forward CV: final model covers all rounds
            Assert.Equal(1, metadata.ValidationDistinctRounds);
            Assert.NotEmpty(metadata.PositionDiagnostics);
            Assert.All(metadata.PositionDiagnostics, item => Assert.True(item.CalibrationSlope > 0d));
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
                    DefaultTopNFeatures = null, // use all features in unit tests for determinism
                }),
                NullLogger<FullContextAthleteScoreModelTrainingService>.Instance);
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
            Assert.InRange(athlete.PredictionConfidence, 0.05m, 0.99m);
            Assert.True(athlete.MovingAverage > 0m);
            Assert.True(athlete.AverageScore > 0m);
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
                    DefaultTopNFeatures = null, // use all features in unit tests for determinism
                }),
                NullLogger<FullContextAthleteScoreModelTrainingService>.Instance);
            await trainingService.TrainAsync(fromRound: 1, toRound: 3, validationRounds: 1);

            var predictionService = new FullContextAthleteScorePredictionService(dbContextFactory, store);
            var result = await predictionService.GetPredictedScoresAsync(round: 4, take: 10);

            Assert.Equal(2, result.TotalAthletesConsidered);
            Assert.Equal(0, result.ExcludedAthletesWithoutContext);
            Assert.Equal(2, result.TotalAthletesPredicted);
            Assert.Contains("latest persisted athlete baselines before round 4", result.PredictionNotes, StringComparison.OrdinalIgnoreCase);
            Assert.All(result.Athletes, item => Assert.Equal(3, item.BaselineRound));
            Assert.All(result.Athletes, item => Assert.NotNull(item.OpponentClubCartolaId));
            Assert.All(result.Athletes, item => Assert.InRange(item.PredictionConfidence, 0.05m, 0.99m));
        }
        finally
        {
            DeleteArtifactDirectory(artifactDirectory);
        }
    }

    [Fact]
    public async Task GetPredictedScoresAsync_FullContext_IgnoresStoredCurrentRoundAggregatesThatCouldLeak()
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
                    DefaultTopNFeatures = null, // use all features in unit tests for determinism
                }),
                NullLogger<FullContextAthleteScoreModelTrainingService>.Instance);
            await trainingService.TrainAsync(fromRound: 1, toRound: 3, validationRounds: 1);

            var predictionService = new FullContextAthleteScorePredictionService(dbContextFactory, store);
            var before = await predictionService.GetPredictedScoresAsync(round: 4, statusCartolaId: 7, clubCartolaId: 293, positionCartolaId: 3, take: 10);
            var predictedScoreBefore = before.Athletes.Single(item => item.AthleteCartolaId == 1001).PredictedScore;

            await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
            {
                var currentBaseline = await dbContext.AthleteRounds.SingleAsync(item =>
                    item.RoundId == 4 &&
                    item.DataPhase == RoundDataPhaseValues.CurrentOpen &&
                    item.Athlete.CartolaId == 1001);

                currentBaseline.GamesNum = 999;
                currentBaseline.HomeGamesNum = 999;
                currentBaseline.AwayGamesNum = 999;
                currentBaseline.MovingAverage = 999m;
                currentBaseline.AverageScoreNum = 999m;
                currentBaseline.HomeAverageScore = 999m;
                currentBaseline.AwayAverageScore = 999m;
                await dbContext.SaveChangesAsync();
            }

            var after = await predictionService.GetPredictedScoresAsync(round: 4, statusCartolaId: 7, clubCartolaId: 293, positionCartolaId: 3, take: 10);
            var predictedScoreAfter = after.Athletes.Single(item => item.AthleteCartolaId == 1001).PredictedScore;

            Assert.Equal(Math.Round(predictedScoreBefore, 6), Math.Round(predictedScoreAfter, 6));
        }
        finally
        {
            DeleteArtifactDirectory(artifactDirectory);
        }
    }

    [Fact]
    public async Task GetPredictedScoresAsync_FullContext_AppliesPositionCalibrationStoredInMetadata()
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
                    DefaultTopNFeatures = null, // use all features in unit tests for determinism
                }),
                NullLogger<FullContextAthleteScoreModelTrainingService>.Instance);
            await trainingService.TrainAsync(fromRound: 1, toRound: 3, validationRounds: 1);

            var predictionService = new FullContextAthleteScorePredictionService(dbContextFactory, store);
            var before = await predictionService.GetPredictedScoresAsync(
                round: 4,
                statusCartolaId: 7,
                clubCartolaId: 293,
                positionCartolaId: 3,
                take: 10);
            var predictedScoreBefore = before.Athletes.Single(item => item.AthleteCartolaId == 1001).PredictedScore;

            var metadataPath = store.GetMetadataPath();
            var metadataJson = JsonNode.Parse(await File.ReadAllTextAsync(metadataPath))!.AsObject();
            metadataJson["PositionDiagnostics"] = new JsonArray
            {
                new JsonObject
                {
                    ["PositionCartolaId"] = 3,
                    ["PositionCode"] = "ZAG",
                    ["PositionName"] = "Zagueiro",
                    ["TrainingRows"] = 2,
                    ["ValidationRows"] = 1,
                    ["TrainingMeanScore"] = 4.0,
                    ["ValidationMeanActualScore"] = 4.0,
                    ["ValidationMeanPredictedScore"] = 1.0,
                    ["ValidationMeanPredictedScoreCalibrated"] = 6.0,
                    ["ValidationMeanAbsoluteError"] = 3.0,
                    ["ValidationRootMeanSquaredError"] = 3.0,
                    ["ValidationBias"] = -3.0,
                    ["CalibrationSlope"] = 1.0,
                    ["CalibrationOffset"] = 5.0,
                },
            };
            await File.WriteAllTextAsync(metadataPath, metadataJson.ToJsonString());

            var after = await predictionService.GetPredictedScoresAsync(
                round: 4,
                statusCartolaId: 7,
                clubCartolaId: 293,
                positionCartolaId: 3,
                take: 10);
            var predictedScoreAfter = after.Athletes.Single(item => item.AthleteCartolaId == 1001).PredictedScore;

            Assert.True(predictedScoreAfter > predictedScoreBefore + 4m);
        }
        finally
        {
            DeleteArtifactDirectory(artifactDirectory);
        }
    }

    [Fact]
    public async Task TrainAllAsync_PersistsPositionalModelArtifactsForPositionsWithSufficientData()
    {
        var dbContextFactory = CreateDbContextFactory();
        var artifactDirectory = CreateArtifactDirectory();
        try
        {
            await SeedFullContextTrainingDataAsync(await dbContextFactory.CreateDbContextAsync(), includeCurrentRoundAthleteBaselines: false);

            var positionalStore = CreatePositionalArtifactStore(artifactDirectory);
            var service = new PositionalAthleteScoreModelTrainingService(
                dbContextFactory,
                positionalStore,
                Options.Create(new PositionalAthleteScoreModelOptions
                {
                    ArtifactsDirectory = artifactDirectory,
                    MinimumTrainingRows = 1,       // low threshold so test data passes
                    DefaultTopNFeatures = null,    // use all features for determinism in tests
                }),
                NullLogger<PositionalAthleteScoreModelTrainingService>.Instance);

            var result = await service.TrainAllAsync(fromRound: 1, toRound: 3, validationRounds: 1);

            // Seed data has defenders (pos 3) and strikers (pos 5); other positions have 0 rows.
            Assert.Contains("trained", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(2, result.TrainedPositions);
            Assert.Equal(4, result.SkippedPositions);
            Assert.True(positionalStore.ModelExists(3), "Zagueiro model artifact should exist.");
            Assert.True(positionalStore.ModelExists(5), "Atacante model artifact should exist.");
            Assert.False(positionalStore.ModelExists(1), "Goleiro model should NOT exist (no data).");

            var zag = result.Positions.Single(p => p.PositionCartolaId == 3);
            Assert.True(zag.Trained);
            Assert.Equal("ZAG", zag.PositionCode);
            Assert.NotNull(zag.ModelInfo);
            Assert.Equal("positional-zag", zag.ModelInfo!.DatasetKey);
            Assert.True(File.Exists(zag.ModelInfo.ModelArtifactPath));
            Assert.True(File.Exists(zag.ModelInfo.MetadataArtifactPath));

            var gol = result.Positions.Single(p => p.PositionCartolaId == 1);
            Assert.False(gol.Trained);
            Assert.NotNull(gol.SkipReason);
        }
        finally
        {
            DeleteArtifactDirectory(artifactDirectory);
        }
    }

    [Fact]
    public async Task GetPredictedScoresAsync_AlwaysUsesGlobalModel()
    {
        var dbContextFactory = CreateDbContextFactory();
        var artifactDirectory = CreateArtifactDirectory();
        try
        {
            await SeedFullContextTrainingDataAsync(await dbContextFactory.CreateDbContextAsync(), includeCurrentRoundAthleteBaselines: true);

            // Train global model — the sole predictor; positional routing was removed after
            // empirical evaluation showed the global model outperforms position-specific models
            // across every position due to its larger training dataset.
            var globalStore = CreateFullContextArtifactStore(artifactDirectory);
            var globalTraining = new FullContextAthleteScoreModelTrainingService(
                dbContextFactory,
                globalStore,
                Options.Create(new FullContextAthleteScoreModelOptions
                {
                    ArtifactsDirectory = artifactDirectory,
                    ModelVersion = "athlete-score-v1-full-context",
                    DefaultTopNFeatures = null,
                }),
                NullLogger<FullContextAthleteScoreModelTrainingService>.Instance);
            await globalTraining.TrainAsync(fromRound: 1, toRound: 3, validationRounds: 1);

            var predictionService = new FullContextAthleteScorePredictionService(
                dbContextFactory, globalStore);
            var result = await predictionService.GetPredictedScoresAsync(
                round: 4,
                statusCartolaId: 7,
                clubCartolaId: 293,
                positionCartolaId: 3,
                take: 10);

            Assert.Equal(4, result.Round);
            Assert.Equal("athlete-score-v1-full-context", result.ModelVersion);
            Assert.Equal(1, result.TotalAthletesConsidered);
            Assert.Single(result.Athletes);
            Assert.NotEqual(0m, result.Athletes.Single().PredictedScore);
            Assert.DoesNotContain("Position-specific models were applied", result.PredictionNotes, StringComparison.OrdinalIgnoreCase);
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

    private static AthleteContextAthleteScoreModelArtifactStore CreateAthleteContextArtifactStore(string artifactDirectory)
        => new(
            new FakeHostEnvironment(artifactDirectory),
            Options.Create(new AthleteContextAthleteScoreModelOptions
            {
                ArtifactsDirectory = artifactDirectory,
                ModelVersion = "athlete-score-v1-athlete-context",
            }));

    private static FullContextAthleteScoreModelArtifactStore CreateFullContextArtifactStore(string artifactDirectory)
        => new(
            new FakeHostEnvironment(artifactDirectory),
            Options.Create(new FullContextAthleteScoreModelOptions
            {
                ArtifactsDirectory = artifactDirectory,
                ModelVersion = "athlete-score-v1-full-context",
            }));

    private static PositionalAthleteScoreModelArtifactStore CreatePositionalArtifactStore(string artifactDirectory)
        => new(
            new FakeHostEnvironment(artifactDirectory),
            Options.Create(new PositionalAthleteScoreModelOptions
            {
                ArtifactsDirectory = artifactDirectory,
                MinimumTrainingRows = 1,
                DefaultTopNFeatures = null,
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

    private static async Task SeedAthleteContextTrainingDataAsync(AppDbContext dbContext, bool includeCurrentRoundAthleteBaselines)
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

        var athleteRounds = new List<AthleteRound>
        {
            CreateHistoricalAthleteRound(1, athleteA.Id, clubA.Id, defender.Id, probable.Id, 4.1m, 4.0m, 10m, 0.2m, 4.5m),
            CreateHistoricalAthleteRound(2, athleteA.Id, clubA.Id, defender.Id, probable.Id, 4.6m, 4.3m, 10.4m, 0.3m, 5.0m),
            CreateHistoricalAthleteRound(3, athleteA.Id, clubA.Id, defender.Id, probable.Id, 5.0m, 4.8m, 10.8m, 0.4m, 5.4m),
            CreateHistoricalAthleteRound(1, athleteB.Id, clubB.Id, striker.Id, doubtful.Id, 6.5m, 6.2m, 14m, -0.1m, 6.1m),
            CreateHistoricalAthleteRound(2, athleteB.Id, clubB.Id, striker.Id, doubtful.Id, 6.2m, 6.0m, 13.8m, -0.2m, 5.8m),
            CreateHistoricalAthleteRound(3, athleteB.Id, clubB.Id, striker.Id, doubtful.Id, 5.9m, 5.7m, 13.5m, -0.3m, 5.4m),
        };

        if (includeCurrentRoundAthleteBaselines)
        {
            athleteRounds.AddRange(
            [
                new AthleteRound(0, 4, athleteA.Id, clubA.Id, defender.Id, probable.Id, 7, 4, 3, 0m, 5.2m, 5.0m, 5.6m, 4.4m, 0.2m, 11.1m, false)
                {
                    DataPhase = RoundDataPhaseValues.CurrentOpen,
                    Scouts = [],
                },
                new AthleteRound(0, 4, athleteB.Id, clubB.Id, striker.Id, probable.Id, 7, 3, 4, 0m, 5.7m, 5.5m, 6.0m, 5.1m, -0.2m, 7.5m, false)
                {
                    DataPhase = RoundDataPhaseValues.CurrentOpen,
                    Scouts = [],
                },
            ]);
        }

        dbContext.AthleteRounds.AddRange(athleteRounds);
        dbContext.Matches.AddRange(
            new Match(0, 10001, 1, new DateTime(2026, 3, 1), "Arena", true, clubA.Id, 6, "VVEED", 1, clubB.Id, 12, "DEEVV", 0),
            new Match(0, 10002, 2, new DateTime(2026, 3, 8), "MaracanÃ£", true, clubB.Id, 11, "DEEVV", 2, clubA.Id, 5, "VVEED", 1),
            new Match(0, 10003, 3, new DateTime(2026, 3, 15), "Arena", true, clubA.Id, 4, "VVVED", 1, clubB.Id, 13, "DDEEV", 1),
            new Match(0, 10004, 4, new DateTime(2026, 3, 22), "Arena", true, clubA.Id, 3, "VVVEE", null, clubB.Id, 14, "DDEED", null));

        await dbContext.SaveChangesAsync();

        var historicalRounds = athleteRounds
            .Where(item => item.RoundId <= 3)
            .ToList();

        dbContext.AthleteRoundContexts.AddRange(
            CreateAthleteRoundContext(historicalRounds[0].Id, 64.4m, 66.0m, 62.0m, 60.4m, 59.1m, 0.48m, 0.45m, 6.1m, 6.0m, 5.8m, 5.6m, 5.4m, 5.3m, 5.2m, 5.0m, 0.90m, 0.40m, 0.70m, 0.10m, 0.44m, 0.39m, 0.41m, 0.52m, 0.31m, 0.27m, 0.58m, 1.8m, 3.2m),
            CreateAthleteRoundContext(historicalRounds[1].Id, 65.7m, 67.5m, 63.2m, 61.5m, 60.3m, 0.50m, 0.46m, 6.4m, 6.2m, 6.0m, 5.8m, 5.7m, 5.5m, 5.3m, 5.2m, 1.10m, 0.55m, 0.80m, 0.20m, 0.46m, 0.42m, 0.44m, 0.55m, 0.33m, 0.29m, 0.61m, 2.0m, 3.4m),
            CreateAthleteRoundContext(historicalRounds[2].Id, 67.2m, 69.1m, 64.1m, 62.8m, 61.2m, 0.53m, 0.49m, 6.7m, 6.5m, 6.3m, 6.0m, 5.9m, 5.7m, 5.5m, 5.4m, 1.30m, 0.70m, 1.00m, 0.30m, 0.49m, 0.45m, 0.47m, 0.58m, 0.36m, 0.30m, 0.64m, 2.2m, 3.6m),
            CreateAthleteRoundContext(historicalRounds[3].Id, 58.3m, 60.1m, 57.0m, 56.8m, 55.2m, 0.36m, 0.34m, 5.2m, 5.0m, 4.9m, 4.7m, 4.5m, 4.4m, 4.2m, 4.0m, 0.60m, 0.15m, 0.40m, -0.05m, 0.35m, 0.28m, 0.26m, 0.44m, 0.25m, 0.20m, 0.49m, 1.4m, 2.5m),
            CreateAthleteRoundContext(historicalRounds[4].Id, 59.5m, 61.0m, 57.9m, 57.7m, 56.1m, 0.38m, 0.35m, 5.4m, 5.2m, 5.0m, 4.8m, 4.7m, 4.5m, 4.3m, 4.1m, 0.70m, 0.22m, 0.50m, 0.03m, 0.37m, 0.30m, 0.28m, 0.46m, 0.27m, 0.22m, 0.51m, 1.5m, 2.7m),
            CreateAthleteRoundContext(historicalRounds[5].Id, 60.2m, 61.8m, 58.6m, 58.1m, 56.8m, 0.39m, 0.36m, 5.6m, 5.4m, 5.2m, 5.0m, 4.9m, 4.7m, 4.4m, 4.2m, 0.78m, 0.28m, 0.57m, 0.08m, 0.39m, 0.32m, 0.30m, 0.48m, 0.29m, 0.24m, 0.53m, 1.6m, 2.9m));

        if (includeCurrentRoundAthleteBaselines)
        {
            var currentRounds = athleteRounds
                .Where(item => item.RoundId == 4)
                .ToList();

            dbContext.AthleteRoundContexts.AddRange(
                CreateAthleteRoundContext(currentRounds[0].Id, 68.0m, 69.8m, 65.0m, 63.0m, 61.9m, 0.55m, 0.51m, 6.9m, 6.7m, 6.4m, 6.1m, 6.1m, 5.9m, 5.6m, 5.5m, 1.42m, 0.82m, 1.10m, 0.36m, 0.51m, 0.47m, 0.49m, 0.60m, 0.38m, 0.31m, 0.66m, 2.3m, 3.8m),
                CreateAthleteRoundContext(currentRounds[1].Id, 60.9m, 62.4m, 59.1m, 58.7m, 57.4m, 0.40m, 0.37m, 5.8m, 5.5m, 5.4m, 5.1m, 5.0m, 4.8m, 4.5m, 4.4m, 0.84m, 0.32m, 0.61m, 0.12m, 0.40m, 0.33m, 0.31m, 0.50m, 0.30m, 0.25m, 0.55m, 1.7m, 3.0m));
        }

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

        var athleteRounds = await dbContext.AthleteRounds
            .OrderBy(item => item.RoundId)
            .ThenBy(item => item.AthleteId)
            .ToListAsync();

        var historicalRounds = athleteRounds
            .Where(item => item.RoundId <= 3)
            .ToList();

        dbContext.AthleteRoundContexts.AddRange(
            CreateAthleteRoundContext(historicalRounds[0].Id, 64.4m, 66.0m, 62.0m, 60.4m, 59.1m, 0.48m, 0.45m, 6.1m, 6.0m, 5.8m, 5.6m, 5.4m, 5.3m, 5.2m, 5.0m, 0.90m, 0.40m, 0.70m, 0.10m, 0.44m, 0.39m, 0.41m, 0.52m, 0.31m, 0.27m, 0.58m, 1.8m, 3.2m),
            CreateAthleteRoundContext(historicalRounds[1].Id, 65.7m, 67.5m, 63.2m, 61.5m, 60.3m, 0.50m, 0.46m, 6.4m, 6.2m, 6.0m, 5.8m, 5.7m, 5.5m, 5.3m, 5.2m, 1.10m, 0.55m, 0.80m, 0.20m, 0.46m, 0.42m, 0.44m, 0.55m, 0.33m, 0.29m, 0.61m, 2.0m, 3.4m),
            CreateAthleteRoundContext(historicalRounds[2].Id, 67.2m, 69.1m, 64.1m, 62.8m, 61.2m, 0.53m, 0.49m, 6.7m, 6.5m, 6.3m, 6.0m, 5.9m, 5.7m, 5.5m, 5.4m, 1.30m, 0.70m, 1.00m, 0.30m, 0.49m, 0.45m, 0.47m, 0.58m, 0.36m, 0.30m, 0.64m, 2.2m, 3.6m),
            CreateAthleteRoundContext(historicalRounds[3].Id, 58.3m, 60.1m, 57.0m, 56.8m, 55.2m, 0.36m, 0.34m, 5.2m, 5.0m, 4.9m, 4.7m, 4.5m, 4.4m, 4.2m, 4.0m, 0.60m, 0.15m, 0.40m, -0.05m, 0.35m, 0.28m, 0.26m, 0.44m, 0.25m, 0.20m, 0.49m, 1.4m, 2.5m),
            CreateAthleteRoundContext(historicalRounds[4].Id, 59.5m, 61.0m, 57.9m, 57.7m, 56.1m, 0.38m, 0.35m, 5.4m, 5.2m, 5.0m, 4.8m, 4.7m, 4.5m, 4.3m, 4.1m, 0.70m, 0.22m, 0.50m, 0.03m, 0.37m, 0.30m, 0.28m, 0.46m, 0.27m, 0.22m, 0.51m, 1.5m, 2.7m),
            CreateAthleteRoundContext(historicalRounds[5].Id, 60.2m, 61.8m, 58.6m, 58.1m, 56.8m, 0.39m, 0.36m, 5.6m, 5.4m, 5.2m, 5.0m, 4.9m, 4.7m, 4.4m, 4.2m, 0.78m, 0.28m, 0.57m, 0.08m, 0.39m, 0.32m, 0.30m, 0.48m, 0.29m, 0.24m, 0.53m, 1.6m, 2.9m));

        dbContext.AthleteRoundScouts.AddRange(
            new AthleteRoundScouts(0, historicalRounds[0].Id, "DS", 2m),
            new AthleteRoundScouts(0, historicalRounds[0].Id, "SG", 1m),
            new AthleteRoundScouts(0, historicalRounds[1].Id, "DS", 1m),
            new AthleteRoundScouts(0, historicalRounds[1].Id, "CA", 1m),
            new AthleteRoundScouts(0, historicalRounds[2].Id, "DS", 3m),
            new AthleteRoundScouts(0, historicalRounds[2].Id, "SG", 1m),
            new AthleteRoundScouts(0, historicalRounds[3].Id, "G", 1m),
            new AthleteRoundScouts(0, historicalRounds[3].Id, "FF", 2m),
            new AthleteRoundScouts(0, historicalRounds[4].Id, "A", 1m),
            new AthleteRoundScouts(0, historicalRounds[4].Id, "FD", 1m),
            new AthleteRoundScouts(0, historicalRounds[5].Id, "G", 1m),
            new AthleteRoundScouts(0, historicalRounds[5].Id, "FS", 2m));

        if (includeCurrentRoundAthleteBaselines)
        {
            var currentRounds = athleteRounds
                .Where(item => item.RoundId == 4)
                .ToList();

            dbContext.AthleteRoundContexts.AddRange(
                CreateAthleteRoundContext(currentRounds[0].Id, 68.0m, 69.8m, 65.0m, 63.0m, 61.9m, 0.55m, 0.51m, 6.9m, 6.7m, 6.4m, 6.1m, 6.1m, 5.9m, 5.6m, 5.5m, 1.42m, 0.82m, 1.10m, 0.36m, 0.51m, 0.47m, 0.49m, 0.60m, 0.38m, 0.31m, 0.66m, 2.3m, 3.8m),
                CreateAthleteRoundContext(currentRounds[1].Id, 60.9m, 62.4m, 59.1m, 58.7m, 57.4m, 0.40m, 0.37m, 5.8m, 5.5m, 5.4m, 5.1m, 5.0m, 4.8m, 4.5m, 4.4m, 0.84m, 0.32m, 0.61m, 0.12m, 0.40m, 0.33m, 0.31m, 0.50m, 0.30m, 0.25m, 0.55m, 1.7m, 3.0m));
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

    private static AthleteRoundContext CreateAthleteRoundContext(
        long athleteRoundId,
        decimal teamAverageScore,
        decimal teamHomeAverageCurrent,
        decimal teamHomeAveragePrevious,
        decimal teamAwayAverageCurrent,
        decimal teamAwayAveragePrevious,
        decimal pointsPerCartoletaCurrent,
        decimal pointsPerCartoletaPrevious,
        decimal grossOverallAverageCurrent,
        decimal grossOverallMovingAverageCurrent,
        decimal grossOverallAveragePrevious,
        decimal grossOverallMovingAveragePrevious,
        decimal netOverallAverageCurrent,
        decimal netOverallMovingAverageCurrent,
        decimal netOverallAveragePrevious,
        decimal netOverallMovingAveragePrevious,
        decimal valuationCurrent,
        decimal minimumToAppreciateCurrent,
        decimal valuationPrevious,
        decimal minimumToAppreciatePrevious,
        decimal indexPointsPerCartoleta,
        decimal indexOffensiveUsage,
        decimal indexOffensiveRepresentation,
        decimal indexPointsRepresentation,
        decimal indexOffensiveEfficiency,
        decimal indexDefensiveEfficiency,
        decimal indexTackleDuelPercent,
        decimal indexShotsPerGame,
        decimal indexShotsPerGoal)
        => new(
            0,
            athleteRoundId,
            teamAverageScore,
            teamHomeAverageCurrent,
            teamHomeAveragePrevious,
            teamAwayAverageCurrent,
            teamAwayAveragePrevious,
            pointsPerCartoletaCurrent,
            pointsPerCartoletaPrevious,
            grossOverallAverageCurrent,
            grossOverallMovingAverageCurrent,
            grossOverallAveragePrevious,
            grossOverallMovingAveragePrevious,
            netOverallAverageCurrent,
            netOverallMovingAverageCurrent,
            netOverallAveragePrevious,
            netOverallMovingAveragePrevious,
            valuationCurrent,
            minimumToAppreciateCurrent,
            valuationPrevious,
            minimumToAppreciatePrevious,
            indexPointsPerCartoleta,
            indexOffensiveUsage,
            indexOffensiveRepresentation,
            indexPointsRepresentation,
            indexOffensiveEfficiency,
            indexDefensiveEfficiency,
            indexTackleDuelPercent,
            indexShotsPerGame,
            indexShotsPerGoal);

    private static AthleteScorePredictionItem CreatePredictionItem(
        int athleteCartolaId,
        string athleteName,
        int positionCartolaId,
        decimal priceNum,
        decimal predictedScore)
        => new()
        {
            AthleteCartolaId = athleteCartolaId,
            AthleteName = athleteName,
            AthleteNickname = athleteName,
            ClubCartolaId = 293,
            ClubName = "Athletico-PR",
            PositionCartolaId = positionCartolaId,
            PositionName = positionCartolaId switch
            {
                1 => "Goleiro",
                2 => "Lateral",
                3 => "Zagueiro",
                4 => "Meia",
                5 => "Atacante",
                6 => "Tecnico",
                _ => "Desconhecido",
            },
            StatusCartolaId = 7,
            StatusName = "Provavel",
            BaselineRound = 9,
            BaselineDataPhase = RoundDataPhaseValues.CurrentOpen,
            PriceNum = priceNum,
            PredictedScore = predictedScore,
            PredictionNotes = "Prediction generated from the current pre-round athlete baseline for round 9.",
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

    private sealed class StubAthleteContextAthleteScorePredictionService : IAthleteContextAthleteScorePredictionService
    {
        public Task<AthleteScorePredictionResult> GetPredictedScoresAsync(
            int round,
            int? statusCartolaId = null,
            int? clubCartolaId = null,
            int? positionCartolaId = null,
            int take = 100,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Athlete-context predictions are not expected in this test.");
    }

    private sealed class StubAthleteScorePredictionService(AthleteScorePredictionResult result) : IAthleteScorePredictionService
    {
        public Task<AthleteScorePredictionResult> GetPredictedScoresAsync(
            int round,
            int? statusCartolaId = null,
            int? clubCartolaId = null,
            int? positionCartolaId = null,
            int take = 100,
            CancellationToken cancellationToken = default)
            => Task.FromResult(result);
    }

    private sealed class ConfigurableAthleteContextAthleteScorePredictionService(AthleteScorePredictionResult result) : IAthleteContextAthleteScorePredictionService
    {
        public Task<AthleteScorePredictionResult> GetPredictedScoresAsync(
            int round,
            int? statusCartolaId = null,
            int? clubCartolaId = null,
            int? positionCartolaId = null,
            int take = 100,
            CancellationToken cancellationToken = default)
            => Task.FromResult(result);
    }

    private sealed class StubFullContextAthleteScorePredictionService(AthleteScorePredictionResult result) : IFullContextAthleteScorePredictionService
    {
        public Task<AthleteScorePredictionResult> GetPredictedScoresAsync(
            int round,
            int? statusCartolaId = null,
            int? clubCartolaId = null,
            int? positionCartolaId = null,
            int take = 100,
            CancellationToken cancellationToken = default)
            => Task.FromResult(result);
    }
}
