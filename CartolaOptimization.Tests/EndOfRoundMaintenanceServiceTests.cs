using CartolaOptimization.Application.Models;
using CartolaOptimization.Domain.Constants;
using CartolaOptimization.Domain.Entities;
using CartolaOptimization.Infrastructure.Context;
using CartolaOptimization.Infrastructure.Diagnostics;
using CartolaOptimization.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CartolaOptimization.Tests;

public sealed class EndOfRoundMaintenanceServiceTests
{
    [Fact]
    public async Task RunAsync_BackfillsOnlyMissingRoundsAndRetrainsModel()
    {
        var options = CreateOptions();
        await using (var seedContext = new AppDbContext(options))
        {
            seedContext.SyncExecutions.AddRange(
                CreateSyncExecution("Cartola", "CartolaSyncService.SyncClosedRoundAsync", 2026, 1, SyncExecutionStatus.Success),
                CreateSyncExecution("Cartola", "CartolaSyncService.SyncClosedRoundAsync", 2026, 2, SyncExecutionStatus.Partial),
                CreateSyncExecution("GatoMestre", "GatoMestreSyncService.SyncRoundAsync", 2026, 1, SyncExecutionStatus.Partial),
                CreateSyncExecution("GatoMestre", "GatoMestreSyncService.SyncRoundAsync", 2026, 2, SyncExecutionStatus.Success));

            var club = new Club(0, 293, "CAP", "athletico-pr", "Athletico-PR", "https://example.com/cap.png");
            var position = new Position(0, 3, "Zagueiro", "zag");
            var status = new AthleteStatus(0, 7, "Provavel");
            var athlete = new Athlete(0, 1001, "Atleta", "Atleta");
            seedContext.Clubs.Add(club);
            seedContext.Positions.Add(position);
            seedContext.AthleteStatuses.Add(status);
            seedContext.Athletes.Add(athlete);
            await seedContext.SaveChangesAsync();

            var round1 = new AthleteRound(0, 1, athlete.Id, club.Id, position.Id, status.Id, 1, 1, 0, 5m, 5m, 5m, 5m, 5m, 0m, 10m, true)
            {
                DataPhase = RoundDataPhaseValues.HistoricalClosed,
                Scouts = [],
            };
            var round2 = new AthleteRound(0, 2, athlete.Id, club.Id, position.Id, status.Id, 2, 1, 1, 6m, 5.5m, 5.5m, 6m, 5m, 0m, 11m, true)
            {
                DataPhase = RoundDataPhaseValues.HistoricalClosed,
                Scouts = [],
            };

            seedContext.AthleteRounds.AddRange(round1, round2);
            await seedContext.SaveChangesAsync();

            seedContext.AthleteRoundContexts.AddRange(
                new AthleteRoundContext(0, round1.Id, 50m, 51m, 49m, 48m, 47m, 0.5m, 0.4m, 7m, 6.5m, 6.3m, 6.1m, 6m, 5.8m, 5.6m, 5.4m, 0.8m, 8m, 0.7m, 7m, 0.4m, 0.5m, 0.6m, 0.7m, 0.8m, 0.9m, 0.3m, 1m, 4m),
                new AthleteRoundContext(0, round2.Id, 51m, 52m, 50m, 49m, 48m, 0.6m, 0.5m, 7.1m, 6.6m, 6.4m, 6.2m, 6.1m, 5.9m, 5.7m, 5.5m, 0.9m, 8.1m, 0.8m, 7.1m, 0.5m, 0.6m, 0.7m, 0.8m, 0.9m, 1m, 0.4m, 1.1m, 4.1m));
            await seedContext.SaveChangesAsync();
        }

        var cartolaClosedRounds = new List<int>();
        var gatoRounds = new List<int>();

        var cartolaSyncService = new FakeCartolaSyncService
        {
            OnSyncAsync = (_, _) => Task.FromResult(new CartolaSyncResult
            {
                Status = SyncExecutionStatus.Success,
                Season = 2026,
                CurrentRound = 5,
                MatchesRound = 5,
                AthleteRounds = [5],
            }),
            OnSyncClosedRoundAsync = (round, _) =>
            {
                cartolaClosedRounds.Add(round);
                return Task.FromResult(new CartolaSyncResult
                {
                    Status = SyncExecutionStatus.Success,
                    Season = 2026,
                    CurrentRound = 5,
                    MatchesRound = round,
                    SyncExecutionId = 200 + round,
                    CorrelationId = $"cartola-{round}",
                });
            },
        };

        var gatoMestreSyncService = new FakeGatoMestreSyncService
        {
            OnSyncRoundAsync = (season, round, _) =>
            {
                gatoRounds.Add(round);
                return Task.FromResult(new GatoMestreSyncResult
                {
                    Season = season,
                    Round = round,
                    Status = SyncExecutionStatus.Partial,
                    IsPartialSync = true,
                    SyncExecutionId = 300 + round,
                    CorrelationId = $"gato-{round}",
                    Notes = "Athlete enrichment completed without club-round context.",
                });
            },
        };

        var preparationService = new FakeTrainingDatasetPreparationService();
        var trainingService = new FakeFullContextAthleteScoreModelTrainingService();

        var service = new EndOfRoundMaintenanceService(
            new TestAppDbContextFactory(options),
            cartolaSyncService,
            gatoMestreSyncService,
            preparationService,
            trainingService);

        var result = await service.RunAsync(validationRounds: 1);

        Assert.Equal(2026, result.Season);
        Assert.Equal(5, result.CurrentRound);
        Assert.Equal(4, result.LastClosedRound);
        Assert.True(result.HistoricalDataUpdated);
        Assert.True(result.ModelRetrained);
        Assert.Equal([3, 4], cartolaClosedRounds);
        Assert.Equal([3, 4], gatoRounds);
        Assert.Single(preparationService.RecomputeReadinessCalls);
        Assert.Equal((3, 4), preparationService.RecomputeReadinessCalls.Single());
        Assert.Single(trainingService.TrainCalls);
        Assert.Equal((null, 4, 1, (int?)null), trainingService.TrainCalls.Single());
        Assert.NotNull(result.CartolaClosedRoundBackfill);
        Assert.Equal(2, result.CartolaClosedRoundBackfill!.RequestedRounds);
        Assert.NotNull(result.GatoMestreBackfill);
        Assert.Equal(2, result.GatoMestreBackfill!.RequestedRounds);
        Assert.Equal(SyncExecutionStatus.Partial, result.GatoMestreBackfill.Rounds.First().Status);
        Assert.Contains(result.Steps, item => item.Step == "Train full-context model" && item.Status == "Executed");
        Assert.NotNull(result.PrimaryModelInfo);
        Assert.Equal("full-context", result.PrimaryModelInfo!.DatasetKey);
    }

    [Fact]
    public async Task RunAsync_SkipsHistoricalRefreshAndTraining_WhenEverythingIsAlreadyUpToDate()
    {
        var options = CreateOptions();
        await using (var seedContext = new AppDbContext(options))
        {
            seedContext.SyncExecutions.AddRange(
                CreateSyncExecution("Cartola", "CartolaSyncService.SyncClosedRoundAsync", 2026, 1, SyncExecutionStatus.Success),
                CreateSyncExecution("Cartola", "CartolaSyncService.SyncClosedRoundAsync", 2026, 2, SyncExecutionStatus.Success),
                CreateSyncExecution("Cartola", "CartolaSyncService.SyncClosedRoundAsync", 2026, 3, SyncExecutionStatus.Partial),
                CreateSyncExecution("Cartola", "CartolaSyncService.SyncClosedRoundAsync", 2026, 4, SyncExecutionStatus.Success),
                CreateSyncExecution("GatoMestre", "GatoMestreSyncService.SyncRoundAsync", 2026, 1, SyncExecutionStatus.Partial),
                CreateSyncExecution("GatoMestre", "GatoMestreSyncService.SyncRoundAsync", 2026, 2, SyncExecutionStatus.Partial),
                CreateSyncExecution("GatoMestre", "GatoMestreSyncService.SyncRoundAsync", 2026, 3, SyncExecutionStatus.Success),
                CreateSyncExecution("GatoMestre", "GatoMestreSyncService.SyncRoundAsync", 2026, 4, SyncExecutionStatus.Partial));

            var club = new Club(0, 293, "CAP", "athletico-pr", "Athletico-PR", "https://example.com/cap.png");
            var position = new Position(0, 3, "Zagueiro", "zag");
            var status = new AthleteStatus(0, 7, "Provavel");
            var athlete = new Athlete(0, 1001, "Atleta", "Atleta");
            seedContext.Clubs.Add(club);
            seedContext.Positions.Add(position);
            seedContext.AthleteStatuses.Add(status);
            seedContext.Athletes.Add(athlete);
            await seedContext.SaveChangesAsync();

            for (var round = 1; round <= 4; round++)
            {
                var athleteRound = new AthleteRound(0, round, athlete.Id, club.Id, position.Id, status.Id, round, 1, round - 1, 5m + round, 4m, 4m, 4m, 4m, 0m, 10m + round, true)
                {
                    DataPhase = RoundDataPhaseValues.HistoricalClosed,
                    Scouts = [],
                };

                seedContext.AthleteRounds.Add(athleteRound);
                await seedContext.SaveChangesAsync();

                seedContext.AthleteRoundContexts.Add(new AthleteRoundContext(0, athleteRound.Id, 50m, 51m, 49m, 48m, 47m, 0.5m, 0.4m, 7m, 6.5m, 6.3m, 6.1m, 6m, 5.8m, 5.6m, 5.4m, 0.8m, 8m, 0.7m, 7m, 0.4m, 0.5m, 0.6m, 0.7m, 0.8m, 0.9m, 0.3m, 1m, 4m));
                await seedContext.SaveChangesAsync();
            }
        }

        var cartolaClosedRounds = new List<int>();
        var gatoRounds = new List<int>();

        var cartolaSyncService = new FakeCartolaSyncService
        {
            OnSyncAsync = (_, _) => Task.FromResult(new CartolaSyncResult
            {
                Status = SyncExecutionStatus.Success,
                Season = 2026,
                CurrentRound = 5,
                MatchesRound = 5,
                AthleteRounds = [5],
            }),
            OnSyncClosedRoundAsync = (round, _) =>
            {
                cartolaClosedRounds.Add(round);
                return Task.FromResult(new CartolaSyncResult { Status = SyncExecutionStatus.Success });
            },
        };

        var gatoMestreSyncService = new FakeGatoMestreSyncService
        {
            OnSyncRoundAsync = (season, round, _) =>
            {
                gatoRounds.Add(round);
                return Task.FromResult(new GatoMestreSyncResult { Season = season, Round = round, Status = SyncExecutionStatus.Partial, IsPartialSync = true });
            },
        };

        var preparationService = new FakeTrainingDatasetPreparationService();
        var trainingService = new FakeFullContextAthleteScoreModelTrainingService
        {
            OnGetCurrentModelInfoAsync = _ => Task.FromResult<AthleteScoreModelInfo?>(new AthleteScoreModelInfo
            {
                ModelVersion = "athlete-score-v1-full-context",
                DatasetKey = "full-context",
                TrainerName = "LightGBM",
                ToRound = 4,
                ValidationRounds = 1,
            }),
        };

        var service = new EndOfRoundMaintenanceService(
            new TestAppDbContextFactory(options),
            cartolaSyncService,
            gatoMestreSyncService,
            preparationService,
            trainingService);

        var result = await service.RunAsync(validationRounds: 1);

        Assert.False(result.HistoricalDataUpdated);
        Assert.False(result.ModelRetrained);
        Assert.Empty(cartolaClosedRounds);
        Assert.Empty(gatoRounds);
        Assert.Empty(preparationService.RecomputeReadinessCalls);
        Assert.Empty(trainingService.TrainCalls);
        Assert.Contains(result.Steps, item => item.Step == "Finalize closed Cartola rounds" && item.Status == "Skipped");
        Assert.Contains(result.Steps, item => item.Step == "Train full-context model" && item.Status == "Skipped");
        Assert.NotNull(result.PrimaryModelInfo);
        Assert.Equal(4, result.PrimaryModelInfo!.ToRound);
    }

    private static DbContextOptions<AppDbContext> CreateOptions()
        => new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

    private static SyncExecution CreateSyncExecution(string sourceSystem, string processName, int season, int round, string status)
        => new()
        {
            CorrelationId = Guid.NewGuid().ToString("N"),
            SourceSystem = sourceSystem,
            ProcessName = processName,
            StartedAtUtc = DateTime.UtcNow,
            FinishedAtUtc = DateTime.UtcNow,
            Status = status,
            Season = season,
            Round = round,
        };

    private sealed class TestAppDbContextFactory(DbContextOptions<AppDbContext> options) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(options);

        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new AppDbContext(options));
    }
}
