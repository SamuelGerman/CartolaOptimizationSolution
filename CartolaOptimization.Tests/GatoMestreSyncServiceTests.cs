using System.Text.Json;
using CartolaOptimization.Domain.Entities;
using CartolaOptimization.Infrastructure.Context;
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
        });

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
        });

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

        public Task<Dictionary<int, GatoMestreTeamResponse>> GetTeamsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Teams);

        public Task<GatoMestreAthletesRoundResponse> GetAthletesRoundAsync(int season, int round, CancellationToken cancellationToken = default)
            => Task.FromResult(AthletesRound);

        public Task<GatoMestreAthleteDetailResponse> GetAthleteDetailAsync(int athleteId, CancellationToken cancellationToken = default)
            => Task.FromResult(AthleteDetails[athleteId]);

        public Task<GatoMestreClubConfrontationResponse> GetClubConfrontationAsync(int clubId, CancellationToken cancellationToken = default)
            => Task.FromResult(ClubConfrontations[clubId]);
    }
}
