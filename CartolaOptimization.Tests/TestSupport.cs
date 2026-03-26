using CartolaOptimization.Application.Interfaces;
using CartolaOptimization.Application.Models;
using CartolaOptimization.Infrastructure.Context;
using CartolaOptimization.Infrastructure.Diagnostics;
using CartolaOptimization.Infrastructure.Integrations.Cartola;
using CartolaOptimization.Infrastructure.Integrations.Cartola.Models;
using CartolaOptimization.Infrastructure.Integrations.GatoMestre.Models;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Primitives;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CartolaOptimization.Tests;

internal static class TestDbContextFactory
{
    public static AppDbContext Create()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new AppDbContext(options);
    }
}

internal sealed class RecordingIntegrationFailureLogService : IIntegrationFailureLogService
{
    public List<IntegrationFailureLogEntry> Entries { get; } = [];

    public Task<int?> TryLogAsync(IntegrationFailureLogEntry entry, CancellationToken cancellationToken = default)
    {
        Entries.Add(entry);
        return Task.FromResult<int?>(Entries.Count);
    }
}

internal sealed class RecordingSyncExecutionLogService : ISyncExecutionLogService
{
    public List<SyncExecutionLogEntry> StartedEntries { get; } = [];

    public List<(int ExecutionId, SyncExecutionLogEntry Entry)> UpdatedEntries { get; } = [];

    public Task<int?> StartAsync(SyncExecutionLogEntry entry, CancellationToken cancellationToken = default)
    {
        StartedEntries.Add(entry);
        return Task.FromResult<int?>(1000 + StartedEntries.Count);
    }

    public Task UpdateAsync(int executionId, SyncExecutionLogEntry entry, CancellationToken cancellationToken = default)
    {
        UpdatedEntries.Add((executionId, entry));
        return Task.CompletedTask;
    }
}

internal sealed class FakeCartolaApiClient : ICartolaApiClient
{
    public CartolaMarketStatusResponse MarketStatus { get; init; } = new();

    public Dictionary<int, CartolaClubResponse> Clubs { get; init; } = [];

    public CartolaAthletesMarketResponse AthletesMarket { get; init; } = new();

    public CartolaAthleteScoresResponse AthleteScores { get; init; } = new();

    public CartolaMatchesResponse Matches { get; init; } = new();

    public Task<CartolaMarketStatusResponse> GetMarketStatusAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(MarketStatus);

    public Task<Dictionary<int, CartolaClubResponse>> GetClubsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(Clubs);

    public Task<CartolaAthletesMarketResponse> GetAthletesMarketAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(AthletesMarket);

    public Task<CartolaAthleteScoresResponse> GetAthleteScoresAsync(int round, CancellationToken cancellationToken = default)
        => Task.FromResult(AthleteScores);

    public Task<CartolaMatchesResponse> GetMatchesAsync(int? round = null, CancellationToken cancellationToken = default)
        => Task.FromResult(Matches);
}

internal sealed class FakeCartolaSyncService : ICartolaSyncService
{
    public Func<int?, CancellationToken, Task<CartolaSyncResult>> OnSyncAsync { get; init; } =
        (matchesRound, _) => Task.FromResult(new CartolaSyncResult
        {
            Status = SyncExecutionStatus.Success,
            MatchesRound = matchesRound ?? 0,
        });

    public Func<int, CancellationToken, Task<CartolaSyncResult>> OnSyncClosedRoundAsync { get; init; } =
        (round, _) => Task.FromResult(new CartolaSyncResult
        {
            Status = SyncExecutionStatus.Success,
            MatchesRound = round,
        });

    public Task<CartolaSyncResult> SyncAsync(int? matchesRound = null, CancellationToken cancellationToken = default)
        => OnSyncAsync(matchesRound, cancellationToken);

    public Task<CartolaSyncResult> SyncClosedRoundAsync(int round, CancellationToken cancellationToken = default)
        => OnSyncClosedRoundAsync(round, cancellationToken);
}

internal sealed class FakeGatoMestreSyncService : IGatoMestreSyncService
{
    public Func<int, int, CancellationToken, Task<GatoMestreSyncResult>> OnSyncRoundAsync { get; init; } =
        (season, round, _) => Task.FromResult(new GatoMestreSyncResult
        {
            Season = season,
            Round = round,
            Status = SyncExecutionStatus.Success,
        });

    public Task<GatoMestreSyncResult> SyncRoundAsync(int season, int round, CancellationToken cancellationToken = default)
        => OnSyncRoundAsync(season, round, cancellationToken);
}

internal sealed class FakeTrainingDatasetPreparationService : ITrainingDatasetPreparationService
{
    public List<(int FromRound, int ToRound)> RecomputeReadinessCalls { get; } = [];

    public Func<int, int, CancellationToken, Task<TrainingDatasetReadinessResult>> OnRecomputeReadinessAsync { get; init; } =
        (fromRound, toRound, _) => Task.FromResult(new TrainingDatasetReadinessResult
        {
            FromRound = fromRound,
            ToRound = toRound,
        });

    public Task<TrainingDatasetReadinessResult> RecomputeReadinessAsync(int fromRound, int toRound, CancellationToken cancellationToken = default)
    {
        RecomputeReadinessCalls.Add((fromRound, toRound));
        return OnRecomputeReadinessAsync(fromRound, toRound, cancellationToken);
    }

    public Task<CartolaBackfillResult> BackfillCartolaResultsAsync(int fromRound, int toRound, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This fake is only intended for readiness recomputation tests.");

    public Task<GatoMestreBackfillResult> BackfillGatoMestreAsync(int season, int fromRound, int toRound, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This fake is only intended for readiness recomputation tests.");
}

internal sealed class FakeAthleteContextAthleteScoreModelTrainingService : IAthleteContextAthleteScoreModelTrainingService
{
    public List<(int? FromRound, int? ToRound, int ValidationRounds)> TrainCalls { get; } = [];

    public Func<int?, int?, int, CancellationToken, Task<AthleteScoreModelTrainingResult>> OnTrainAsync { get; init; } =
        (fromRound, toRound, validationRounds, _) => Task.FromResult(new AthleteScoreModelTrainingResult
        {
            Message = "trained",
            ModelInfo = new AthleteScoreModelInfo
            {
                ModelVersion = "athlete-score-v1-athlete-context",
                DatasetKey = "athlete-context",
                FromRound = fromRound,
                ToRound = toRound,
                ValidationRounds = validationRounds,
            },
        });

    public Func<CancellationToken, Task<AthleteScoreModelInfo?>> OnGetCurrentModelInfoAsync { get; init; } =
        _ => Task.FromResult<AthleteScoreModelInfo?>(null);

    public Task<AthleteScoreModelTrainingResult> TrainAsync(int? fromRound = null, int? toRound = null, int validationRounds = 1, CancellationToken cancellationToken = default)
    {
        TrainCalls.Add((fromRound, toRound, validationRounds));
        return OnTrainAsync(fromRound, toRound, validationRounds, cancellationToken);
    }

    public Task<AthleteScoreModelInfo?> GetCurrentModelInfoAsync(CancellationToken cancellationToken = default)
        => OnGetCurrentModelInfoAsync(cancellationToken);
}

internal sealed class FakeFullContextAthleteScorePredictionService : IFullContextAthleteScorePredictionService
{
    public Func<int, int?, int?, int?, int, CancellationToken, Task<AthleteScorePredictionResult>> OnGetPredictedScoresAsync { get; init; } =
        (_, _, _, _, _, _) => Task.FromResult(new AthleteScorePredictionResult());

    public Task<AthleteScorePredictionResult> GetPredictedScoresAsync(
        int round,
        int? statusCartolaId = null,
        int? clubCartolaId = null,
        int? positionCartolaId = null,
        int take = 100,
        CancellationToken cancellationToken = default)
        => OnGetPredictedScoresAsync(round, statusCartolaId, clubCartolaId, positionCartolaId, take, cancellationToken);
}

internal sealed class FakeHostEnvironment(string contentRootPath) : IHostEnvironment
{
    public string EnvironmentName { get; set; } = "Development";

    public string ApplicationName { get; set; } = "CartolaOptimization.Tests";

    public string ContentRootPath { get; set; } = contentRootPath;

    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();

    private sealed class NullFileProvider : IFileProvider
    {
        public IDirectoryContents GetDirectoryContents(string subpath) => NotFoundDirectoryContents.Singleton;

        public IFileInfo GetFileInfo(string subpath) => new NotFoundFileInfo(subpath);

        public IChangeToken Watch(string filter) => NullChangeToken.Singleton;
    }
}
