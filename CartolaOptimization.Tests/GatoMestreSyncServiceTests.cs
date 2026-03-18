using System.Text.Json;
using CartolaOptimization.Domain.Entities;
using CartolaOptimization.Infrastructure.Context;
using CartolaOptimization.Infrastructure.Diagnostics;
using CartolaOptimization.Infrastructure.Integrations.GatoMestre;
using CartolaOptimization.Infrastructure.Integrations.GatoMestre.Models;
using CartolaOptimization.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CartolaOptimization.Tests;

public sealed class GatoMestreSyncServiceTests
{
    [Fact]
    public async Task SyncRoundAsync_CreatesClubRoundAndPositionStats_WhenConfrontationDataMatchesRequestedRound()
    {
        await using var dbContext = CreateDbContext();
        var failureLogService = new FakeIntegrationFailureLogService();
        var syncExecutionLogService = new FakeSyncExecutionLogService();
        var service = new GatoMestreSyncService(dbContext, new FakeGatoMestreApiClient
        {
            Teams = new Dictionary<int, GatoMestreTeamResponse>
            {
                [284] = CreateTeam(284, "GRE", "gremio", "Grêmio"),
                [287] = CreateTeam(287, "VIT", "vitoria", "Vitória"),
            },
            AthletesRound = new GatoMestreAthletesRoundResponse(),
            ClubConfrontations = new Dictionary<int, GatoMestreClubConfrontationResponse>
            {
                [284] = new()
                {
                    Round = 7,
                    Home = CreateClubBlock(284, "mandante", 43.06m, 30.36m, 45.18m, 54.02m, 54.09m, [(1, 1.85m, 7.85m), (5, 14.75m, 24.60m)]),
                    Away = CreateClubBlock(287, "visitante", 43.06m, 30.36m, 84.81m, 47.18m, 47.18m, [(1, 1.90m, 4.05m), (5, 2.10m, 6.45m)]),
                },
                [287] = new()
                {
                    Round = 7,
                    Home = CreateClubBlock(284, "mandante", 43.06m, 30.36m, 45.18m, 54.02m, 54.09m, [(1, 1.85m, 7.85m), (5, 14.75m, 24.60m)]),
                    Away = CreateClubBlock(287, "visitante", 43.06m, 30.36m, 84.81m, 47.18m, 47.18m, [(1, 1.90m, 4.05m), (5, 2.10m, 6.45m)]),
                },
            },
        }, failureLogService, syncExecutionLogService);

        var result = await service.SyncRoundAsync(2026, 7);

        var clubRounds = await dbContext.ClubRounds.Include(round => round.PositionStats).OrderBy(round => round.ClubId).ToListAsync();

        Assert.True(result.ClubRoundDataAvailable);
        Assert.Equal(2, result.ClubRoundsUpserted);
        Assert.Equal(4, result.ClubRoundPositionsUpserted);
        Assert.Equal(2, clubRounds.Count);
        Assert.All(clubRounds, round => Assert.Equal(2, round.PositionStats.Count));
    }

    [Fact]
    public async Task SyncRoundAsync_CreatesAthleteRoundFromDetailFallback_WhenRoundRowDoesNotExist()
    {
        await using var dbContext = CreateDbContext();
        var failureLogService = new FakeIntegrationFailureLogService();
        var syncExecutionLogService = new FakeSyncExecutionLogService();
        var service = new GatoMestreSyncService(dbContext, new FakeGatoMestreApiClient
        {
            Teams = new Dictionary<int, GatoMestreTeamResponse>
            {
                [293] = CreateTeam(293, "CAP", "atletico-pr", "Athletico-PR"),
            },
            AthletesRound = new GatoMestreAthletesRoundResponse
            {
                Result =
                [
                    new GatoMestreAthleteRoundSummaryResponse
                    {
                        AthleteId = 130843,
                        Games = JsonDocument.Parse("""{"geral":3,"mandante":1,"visitante":2}""").RootElement.Clone(),
                        Averages = JsonDocument.Parse("""{"geral":{"movel":{"num":4.33},"total":{"num":4.33}},"mandante":{"total":{"num":4.2}},"visitante":{"total":{"num":4.4}}}""").RootElement.Clone(),
                    }
                ],
            },
            AthleteDetails = new Dictionary<int, GatoMestreAthleteDetailResponse>
            {
                [130843] = new()
                {
                    Athlete = new GatoMestreAthleteProfileResponse
                    {
                        AthleteId = 130843,
                        TeamId = 293,
                        Name = "Arthur dos Santos Barbosa Dias",
                        PopularName = "Arthur Dias",
                        Nickname = "Arthur Dias",
                        Position = new GatoMestrePositionDescriptorResponse
                        {
                            Macro = "ZAG",
                            Sigla = "ZAE",
                            Description = "Zagueiro esquerdo",
                        },
                    },
                    Team = CreateTeam(293, "CAP", "atletico-pr", "Athletico-PR"),
                    Rounds =
                    [
                        new GatoMestreAthleteRoundDetailResponse
                        {
                            Round = 4,
                            WasInGame = true,
                            GamesCount = 3,
                            StatusName = "Provável",
                            Price = new GatoMestrePriceSnapshotResponse { Num = 5.3m, Variation = -0.36m },
                            Points = new GatoMestreNumericSnapshotResponse { Num = 0.8m, Variation = -3.4m },
                            Scouts = new Dictionary<string, decimal> { ["DS"] = 1m, ["CA"] = 1m },
                            Averages = new GatoMestreDetailedAveragesResponse
                            {
                                Overall = new GatoMestreAveragesBucketResponse
                                {
                                    Moving = new GatoMestreNumericSnapshotResponse { Num = 4.33m },
                                    Total = new GatoMestreNumericSnapshotResponse { Num = 4.33m },
                                },
                                Home = new GatoMestreAveragesBucketResponse
                                {
                                    Total = new GatoMestreNumericSnapshotResponse { Num = 4.2m },
                                },
                                Away = new GatoMestreAveragesBucketResponse
                                {
                                    Total = new GatoMestreNumericSnapshotResponse { Num = 4.4m },
                                },
                            },
                        }
                    ],
                },
            },
            ClubConfrontations = new Dictionary<int, GatoMestreClubConfrontationResponse>
            {
                [293] = new()
                {
                    Round = 7,
                    Home = CreateClubBlock(293, "mandante", 40m, 30m, 60m, 50m, 48m, []),
                    Away = CreateClubBlock(262, "visitante", 40m, 30m, 40m, 52m, 49m, []),
                },
            },
        }, failureLogService, syncExecutionLogService);

        var result = await service.SyncRoundAsync(2026, 4);

        var athlete = await dbContext.Athletes.SingleAsync();
        var athleteRound = await dbContext.AthleteRounds.Include(round => round.Scouts).SingleAsync();

        Assert.False(result.ClubRoundDataAvailable);
        Assert.Equal(1, result.AthleteRoundsUpserted);
        Assert.Equal(1, result.AthleteDetailsFetched);
        Assert.Equal("Arthur dos Santos Barbosa Dias", athlete.Name);
        Assert.Equal(4, athleteRound.RoundId);
        Assert.Equal(3, athleteRound.GamesNum);
        Assert.Equal(1, athleteRound.HomeGamesNum);
        Assert.Equal(2, athleteRound.AwayGamesNum);
        Assert.Equal(4.33m, athleteRound.MovingAverage);
        Assert.Equal(4.2m, athleteRound.HomeAverageScore);
        Assert.Equal(4.4m, athleteRound.AwayAverageScore);
        Assert.Equal(0.8m, athleteRound.ScoreNum);
        Assert.Equal(5.3m, athleteRound.PriceNum);
        Assert.Equal(2, athleteRound.Scouts.Count);
    }

    [Fact]
    public async Task SyncRoundAsync_LogsWarningAndContinues_WhenAthleteDetailRequestFailsButPreviousRoundCanBeUsedAsFallback()
    {
        await using var dbContext = CreateDbContext();
        var failureLogService = new FakeIntegrationFailureLogService();
        var syncExecutionLogService = new FakeSyncExecutionLogService();

        var club = new Club(0, 293, "CAP", "atletico-pr", "Athletico-PR", "https://example.com/293.png");
        var position = new Position(0, 3, "Zagueiro", "zag");
        var status = new AthleteStatus(0, 7, "Provavel");
        var athlete = new Athlete(0, 37457, "Atleta 37457", "Atleta 37457");

        dbContext.Clubs.Add(club);
        dbContext.Positions.Add(position);
        dbContext.AthleteStatuses.Add(status);
        dbContext.Athletes.Add(athlete);
        await dbContext.SaveChangesAsync();

        dbContext.AthleteRounds.Add(new AthleteRound(
            0,
            5,
            athlete.Id,
            club.Id,
            position.Id,
            status.Id,
            2,
            1,
            1,
            1.9m,
            2.63m,
            2.63m,
            2.96m,
            2.3m,
            -0.10m,
            5.10m,
            true)
        {
            Scouts = [],
        });
        await dbContext.SaveChangesAsync();

        var service = new GatoMestreSyncService(dbContext, new FakeGatoMestreApiClient
        {
            Teams = new Dictionary<int, GatoMestreTeamResponse>
            {
                [293] = CreateTeam(293, "CAP", "atletico-pr", "Athletico-PR"),
            },
            AthletesRound = new GatoMestreAthletesRoundResponse
            {
                Result =
                [
                    new GatoMestreAthleteRoundSummaryResponse
                    {
                        AthleteId = 37457,
                        Points = JsonDocument.Parse("""{"num":2.3,"variacao":0}""").RootElement.Clone(),
                        Scouts = JsonDocument.Parse("""{"rodada":{"DS":0,"CA":0}}""").RootElement.Clone(),
                        Games = JsonDocument.Parse("""{"geral":3,"mandante":1,"visitante":2}""").RootElement.Clone(),
                        Averages = JsonDocument.Parse("""{"geral":{"movel":{"num":4.33},"total":{"num":4.33}}}""").RootElement.Clone(),
                    }
                ],
            },
            ClubConfrontations = new Dictionary<int, GatoMestreClubConfrontationResponse>
            {
                [293] = new()
                {
                    Round = 6,
                    Home = CreateClubBlock(293, "mandante", 40m, 30m, 60m, 50m, 48m, []),
                    Away = CreateClubBlock(262, "visitante", 40m, 30m, 40m, 52m, 49m, []),
                },
            },
            ThrowAthleteDetail = new ExternalApiException(
                sourceSystem: "GatoMestre",
                errorLocation: "GatoMestreApiClient.GetRequiredAsync",
                requestMethod: "GET",
                requestUrl: "https://api.gatomestre.globo.com/api/v2/atletas/37457?a=true",
                requestHeadersJson: """{"User-Agent":"CartolaOptimization/1.0"}""",
                requestPayload: string.Empty,
                responseStatusCode: 200,
                responseReasonPhrase: "OK",
                responseHeadersJson: """{"Content-Type":"application/json"}""",
                responsePayload: """{"error":true,"message":"Atleta não encontrado"}""",
                message: "The Gato Mestre API returned an application error for the athlete detail endpoint."),
        }, failureLogService, syncExecutionLogService);

        var result = await service.SyncRoundAsync(2026, 6);
        var athleteRound = await dbContext.AthleteRounds.SingleAsync(item => item.RoundId == 6 && item.AthleteId == athlete.Id);

        Assert.Equal(1, result.AthleteRoundsUpserted);
        Assert.Equal(0, result.AthleteRoundsSkipped);
        Assert.Equal(1, result.AthleteFailuresLogged);
        Assert.True(result.IsPartialSync);
        Assert.Single(failureLogService.Entries);
        Assert.Contains("37457", failureLogService.Entries[0].RequestUrl);
        Assert.Contains("Atleta não encontrado", failureLogService.Entries[0].ResponsePayload);
        Assert.Contains("athleteCartolaId", failureLogService.Entries[0].ContextJson);
        Assert.Equal(position.Id, athleteRound.PositionId);
        Assert.Equal(status.Id, athleteRound.StatusId);
        Assert.Equal(club.Id, athleteRound.ClubId);
        Assert.Equal(2.3m, athleteRound.ScoreNum);
    }

    [Fact]
    public async Task SyncRoundAsync_UsesZeroValues_WhenDetailNumericSnapshotsAreNull()
    {
        await using var dbContext = CreateDbContext();
        var failureLogService = new FakeIntegrationFailureLogService();
        var syncExecutionLogService = new FakeSyncExecutionLogService();
        var service = new GatoMestreSyncService(dbContext, new FakeGatoMestreApiClient
        {
            Teams = new Dictionary<int, GatoMestreTeamResponse>
            {
                [293] = CreateTeam(293, "CAP", "atletico-pr", "Athletico-PR"),
            },
            AthletesRound = new GatoMestreAthletesRoundResponse
            {
                Result =
                [
                    new GatoMestreAthleteRoundSummaryResponse
                    {
                        AthleteId = 130843,
                        Games = JsonDocument.Parse("""{}""").RootElement.Clone(),
                        Averages = JsonDocument.Parse("""{}""").RootElement.Clone(),
                        Points = JsonDocument.Parse("""{}""").RootElement.Clone(),
                        Price = JsonDocument.Parse("""{}""").RootElement.Clone(),
                        Scouts = JsonDocument.Parse("""{}""").RootElement.Clone(),
                    }
                ],
            },
            AthleteDetails = new Dictionary<int, GatoMestreAthleteDetailResponse>
            {
                [130843] = new()
                {
                    Athlete = new GatoMestreAthleteProfileResponse
                    {
                        AthleteId = 130843,
                        TeamId = 293,
                        Name = "Arthur dos Santos Barbosa Dias",
                        PopularName = "Arthur Dias",
                        Nickname = "Arthur Dias",
                        Position = new GatoMestrePositionDescriptorResponse
                        {
                            Macro = "ZAG",
                            Sigla = "ZAG",
                            Description = "Zagueiro",
                        },
                    },
                    Team = CreateTeam(293, "CAP", "atletico-pr", "Athletico-PR"),
                    Rounds =
                    [
                        new GatoMestreAthleteRoundDetailResponse
                        {
                            Round = 4,
                            WasInGame = false,
                            GamesCount = 0,
                            StatusName = "Provavel",
                            Price = new GatoMestrePriceSnapshotResponse { Num = null, Variation = null },
                            Points = new GatoMestreNumericSnapshotResponse { Num = null, Variation = null },
                            Averages = new GatoMestreDetailedAveragesResponse
                            {
                                Overall = new GatoMestreAveragesBucketResponse
                                {
                                    Moving = new GatoMestreNumericSnapshotResponse { Num = null },
                                    Total = new GatoMestreNumericSnapshotResponse { Num = null },
                                },
                                Home = new GatoMestreAveragesBucketResponse
                                {
                                    Total = new GatoMestreNumericSnapshotResponse { Num = null },
                                },
                                Away = new GatoMestreAveragesBucketResponse
                                {
                                    Total = new GatoMestreNumericSnapshotResponse { Num = null },
                                },
                            },
                        }
                    ],
                },
            },
            ClubConfrontations = new Dictionary<int, GatoMestreClubConfrontationResponse>
            {
                [293] = new()
                {
                    Round = 7,
                    Home = CreateClubBlock(293, "mandante", 40m, 30m, 60m, 50m, 48m, []),
                    Away = CreateClubBlock(262, "visitante", 40m, 30m, 40m, 52m, 49m, []),
                },
            },
        }, failureLogService, syncExecutionLogService);

        var result = await service.SyncRoundAsync(2026, 4);
        var athleteRound = await dbContext.AthleteRounds.SingleAsync();

        Assert.Equal(1, result.AthleteRoundsUpserted);
        Assert.Equal(1, result.AthleteDetailsFetched);
        Assert.Equal(0m, athleteRound.ScoreNum);
        Assert.Equal(0m, athleteRound.MovingAverage);
        Assert.Equal(0m, athleteRound.AverageScoreNum);
        Assert.Equal(0m, athleteRound.HomeAverageScore);
        Assert.Equal(0m, athleteRound.AwayAverageScore);
        Assert.Equal(0m, athleteRound.PriceNum);
        Assert.Equal(0m, athleteRound.VariationNum);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new AppDbContext(options);
    }

    private static GatoMestreTeamResponse CreateTeam(int teamId, string acronym, string slug, string popularName)
    {
        return new GatoMestreTeamResponse
        {
            TeamId = teamId,
            Acronym = acronym,
            Slug = slug,
            Name = popularName,
            PopularName = popularName,
            Nickname = popularName,
            Shields = new GatoMestreTeamShieldResponse { Size60 = $"https://example.com/{teamId}.png" },
        };
    }

    private static GatoMestreClubRoundBlockResponse CreateClubBlock(
        int teamId,
        string homeAwayFlag,
        decimal homeWinProbability,
        decimal awayWinProbability,
        decimal expectedPoints,
        decimal overallAverage,
        decimal movingAverage,
        IReadOnlyCollection<(int PositionId, decimal Ceded, decimal Conquered)> stats)
    {
        var mappedStats = stats.Select(item => new GatoMestreClubPositionStatsResponse
        {
            PositionId = item.PositionId,
            PointsConcededAverage = item.Ceded,
            PointsConqueredAverage = item.Conquered,
        }).ToList();

        return new GatoMestreClubRoundBlockResponse
        {
            TeamId = teamId,
            OverallAverage = overallAverage,
            MovingAverage = movingAverage,
            NextConfrontation = new GatoMestreNextConfrontationResponse
            {
                HomeAwayFlag = homeAwayFlag,
                DrawProbability = 26.56m,
                HomeTeamId = teamId,
                AwayTeamId = teamId == 284 ? 287 : 284,
                HomeWinProbability = homeWinProbability,
                AwayWinProbability = awayWinProbability,
                HomeExpectedPoints = homeAwayFlag == "mandante" ? expectedPoints : 45.18m,
                AwayExpectedPoints = homeAwayFlag == "visitante" ? expectedPoints : 84.81m,
            },
            PositionStats = new GatoMestrePointsByPositionBlockResponse
            {
                Home = mappedStats,
                Away = mappedStats,
            },
        };
    }

    private sealed class FakeGatoMestreApiClient : IGatoMestreApiClient
    {
        public Dictionary<int, GatoMestreTeamResponse> Teams { get; init; } = [];

        public GatoMestreAthletesRoundResponse AthletesRound { get; init; } = new();

        public Dictionary<int, GatoMestreAthleteDetailResponse> AthleteDetails { get; init; } = [];

        public Dictionary<int, GatoMestreClubConfrontationResponse> ClubConfrontations { get; init; } = [];

        public ExternalApiException? ThrowAthleteDetail { get; init; }

        public Task<Dictionary<int, GatoMestreTeamResponse>> GetTeamsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Teams);

        public Task<GatoMestreAthletesRoundResponse> GetAthletesRoundAsync(int season, int round, CancellationToken cancellationToken = default)
            => Task.FromResult(AthletesRound);

        public Task<GatoMestreAthleteDetailResponse> GetAthleteDetailAsync(int athleteId, CancellationToken cancellationToken = default)
        {
            if (ThrowAthleteDetail is not null)
            {
                throw ThrowAthleteDetail;
            }

            return Task.FromResult(AthleteDetails[athleteId]);
        }

        public Task<GatoMestreClubConfrontationResponse> GetClubConfrontationAsync(int clubId, CancellationToken cancellationToken = default)
            => Task.FromResult(ClubConfrontations.TryGetValue(clubId, out var confrontation)
                ? confrontation
                : new GatoMestreClubConfrontationResponse());
    }

    private sealed class FakeIntegrationFailureLogService : IIntegrationFailureLogService
    {
        public List<IntegrationFailureLogEntry> Entries { get; } = [];

        public Task<int?> TryLogAsync(IntegrationFailureLogEntry entry, CancellationToken cancellationToken = default)
        {
            Entries.Add(entry);
            return Task.FromResult<int?>(9001);
        }
    }

    private sealed class FakeSyncExecutionLogService : ISyncExecutionLogService
    {
        public List<SyncExecutionLogEntry> StartedEntries { get; } = [];

        public List<(int ExecutionId, SyncExecutionLogEntry Entry)> UpdatedEntries { get; } = [];

        public Task<int?> StartAsync(SyncExecutionLogEntry entry, CancellationToken cancellationToken = default)
        {
            StartedEntries.Add(entry);
            return Task.FromResult<int?>(7001);
        }

        public Task UpdateAsync(int executionId, SyncExecutionLogEntry entry, CancellationToken cancellationToken = default)
        {
            UpdatedEntries.Add((executionId, entry));
            return Task.CompletedTask;
        }
    }
}
