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
    public async Task SyncRoundAsync_UsesCurrentPayloadClubs_WhenDatabaseContainsDuplicateAbbreviations()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Clubs.Add(new Club(0, 9999, "BOT", "botafogo-antigo", "Botafogo Antigo", "https://example.com/old-bot.png"));
        await dbContext.SaveChangesAsync();

        var failureLogService = new FakeIntegrationFailureLogService();
        var syncExecutionLogService = new FakeSyncExecutionLogService();
        var service = new GatoMestreSyncService(dbContext, new FakeGatoMestreApiClient
        {
            Teams = new Dictionary<int, GatoMestreTeamResponse>
            {
                [263] = CreateTeam(263, "BOT", "botafogo", "Botafogo"),
                [275] = CreateTeam(275, "PAL", "palmeiras", "Palmeiras"),
            },
            AthletesRound = new GatoMestreAthletesRoundResponse(),
            ClubConfrontations = new Dictionary<int, GatoMestreClubConfrontationResponse>
            {
                [263] = new()
                {
                    Round = 6,
                    Home = CreateClubBlock(275, "mandante", 51m, 29m, 46m, 54m, 55m, [(2, 4m, 8m), (5, 9m, 14m)]),
                    Away = CreateClubBlock(263, "visitante", 51m, 29m, 38m, 41m, 40m, [(2, 7m, 5m), (5, 13m, 7m)]),
                },
                [275] = new()
                {
                    Round = 6,
                    Home = CreateClubBlock(275, "mandante", 51m, 29m, 46m, 54m, 55m, [(2, 4m, 8m), (5, 9m, 14m)]),
                    Away = CreateClubBlock(263, "visitante", 51m, 29m, 38m, 41m, 40m, [(2, 7m, 5m), (5, 13m, 7m)]),
                },
            },
            FavoriteMatches = new()
            {
                Result =
                [
                    new GatoMestreFavoriteMatchResponse
                    {
                        HomeTeam = new GatoMestreSimpleTeamReferenceResponse { Abbreviation = "PAL", Name = "Palmeiras" },
                        AwayTeam = new GatoMestreSimpleTeamReferenceResponse { Abbreviation = "BOT", Name = "Botafogo" },
                        Odds = new GatoMestreFavoriteMatchOddsResponse { WinProbability = 51m, DrawProbability = 29m, LoseProbability = 20m },
                    },
                ],
            },
        }, failureLogService, syncExecutionLogService);

        var result = await service.SyncRoundAsync(2026, 6);

        var syncedClubCartolaIds = await dbContext.ClubRounds
            .Include(item => item.Club)
            .OrderBy(item => item.Club.CartolaId)
            .Select(item => item.Club.CartolaId)
            .ToListAsync();

        Assert.Equal(SyncExecutionStatus.Success, result.Status);
        Assert.Equal([263, 275], syncedClubCartolaIds);
        Assert.Equal(2, result.ClubsUpserted);
        Assert.Empty(failureLogService.Entries);
    }

    [Fact]
    public async Task SyncRoundAsync_PersistsExtendedAthleteAndClubContexts_WhenOptionalPayloadsAreAvailable()
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
            AthletesRound = new GatoMestreAthletesRoundResponse
            {
                Result =
                [
                    new GatoMestreAthleteRoundSummaryResponse
                    {
                        AthleteId = 72951,
                        Games = JsonDocument.Parse("""{"geral":1,"mandante":1,"visitante":0}""").RootElement.Clone(),
                        Averages = JsonDocument.Parse("""{"geral":{"movel":{"num":2.4},"total":{"num":2.4}},"mandante":{"total":{"num":2.4}},"visitante":{"total":{"num":0}}}""").RootElement.Clone(),
                        Points = JsonDocument.Parse("""{"num":2.4,"variacao":0.3}""").RootElement.Clone(),
                        Price = JsonDocument.Parse("""{"valor":5.1,"valorizacao":0.3}""").RootElement.Clone(),
                    }
                ],
            },
            AthleteDetails = new Dictionary<int, GatoMestreAthleteDetailResponse>
            {
                [72951] = new()
                {
                    Athlete = new GatoMestreAthleteProfileResponse
                    {
                        AthleteId = 72951,
                        TeamId = 284,
                        Name = "Mercado",
                        PopularName = "Mercado",
                        Nickname = "Mercado",
                        Position = new GatoMestrePositionDescriptorResponse
                        {
                            Macro = "ZAG",
                            Sigla = "ZAG",
                            Description = "Zagueiro",
                        },
                    },
                    Team = CreateTeam(284, "GRE", "gremio", "Grêmio"),
                    Rounds =
                    [
                        new GatoMestreAthleteRoundDetailResponse
                        {
                            Round = 1,
                            WasInGame = true,
                            GamesCount = 1,
                            StatusName = "Provável",
                            Price = new GatoMestrePriceSnapshotResponse { Num = 5.1m, Variation = 0.3m },
                            Points = new GatoMestreNumericSnapshotResponse { Num = 2.4m, Variation = 0.3m },
                            Averages = new GatoMestreDetailedAveragesResponse
                            {
                                Overall = new GatoMestreAveragesBucketResponse
                                {
                                    Moving = new GatoMestreNumericSnapshotResponse { Num = 2.4m },
                                    Total = new GatoMestreNumericSnapshotResponse { Num = 2.4m },
                                },
                                Home = new GatoMestreAveragesBucketResponse
                                {
                                    Total = new GatoMestreNumericSnapshotResponse { Num = 2.4m },
                                },
                                Away = new GatoMestreAveragesBucketResponse
                                {
                                    Total = new GatoMestreNumericSnapshotResponse { Num = 0m },
                                },
                            },
                        }
                    ],
                },
            },
            AthleteRoundContents = new Dictionary<int, GatoMestreAthleteRoundContentResponse>
            {
                [72951] = new()
                {
                    AthleteId = 72951,
                    TeamId = 284,
                    TeamAverageScore = 27.16m,
                    TeamHomeAverageCurrent = 30m,
                    TeamHomeAveragePrevious = 28m,
                    TeamAwayAverageCurrent = 24m,
                    TeamAwayAveragePrevious = 20m,
                    PointsPerCartoletaCurrent = 1.25m,
                    PointsPerCartoletaPrevious = 0.95m,
                    GrossOverallAverageCurrent = 5.3m,
                    GrossOverallMovingAverageCurrent = 5.8m,
                    GrossOverallAveragePrevious = 4.8m,
                    GrossOverallMovingAveragePrevious = 4.2m,
                    NetOverallAverageCurrent = 4.9m,
                    NetOverallMovingAverageCurrent = 5.2m,
                    NetOverallAveragePrevious = 4.1m,
                    NetOverallMovingAveragePrevious = 3.9m,
                    ValuationCurrent = 0.42m,
                    MinimumToAppreciateCurrent = 4.11m,
                    ValuationPrevious = -0.25m,
                    MinimumToAppreciatePrevious = 3.74m,
                    Indices =
                    [
                        new GatoMestreNamedMetricResponse { Key = "ipc", Value = 1.25m },
                        new GatoMestreNamedMetricResponse { Key = "ieo", Value = 4.75m },
                        new GatoMestreNamedMetricResponse { Key = "ied", Value = 7.80m },
                        new GatoMestreNamedMetricResponse { Key = "fj", Value = 0.60m },
                        new GatoMestreNamedMetricResponse { Key = "fg", Value = 0.30m },
                    ],
                },
            },
            ClubConfrontations = new Dictionary<int, GatoMestreClubConfrontationResponse>
            {
                [284] = new()
                {
                    Round = 1,
                    Home = CreateClubBlock(284, "mandante", 53m, 21m, 49m, 45m, 47m, [(3, 8m, 12m), (5, 14m, 22m)]),
                    Away = CreateClubBlock(287, "visitante", 53m, 21m, 35m, 40m, 38m, [(3, 9m, 8m), (5, 16m, 10m)]),
                },
                [287] = new()
                {
                    Round = 1,
                    Home = CreateClubBlock(284, "mandante", 53m, 21m, 49m, 45m, 47m, [(3, 8m, 12m), (5, 14m, 22m)]),
                    Away = CreateClubBlock(287, "visitante", 53m, 21m, 35m, 40m, 38m, [(3, 9m, 8m), (5, 16m, 10m)]),
                },
            },
            FavoriteMatches = new()
            {
                Result =
                [
                    new GatoMestreFavoriteMatchResponse
                    {
                        HomeTeam = new GatoMestreSimpleTeamReferenceResponse { Abbreviation = "GRE", Name = "Grêmio" },
                        AwayTeam = new GatoMestreSimpleTeamReferenceResponse { Abbreviation = "VIT", Name = "Vitória" },
                        Odds = new GatoMestreFavoriteMatchOddsResponse { WinProbability = 53m, DrawProbability = 31m, LoseProbability = 16m },
                    },
                ],
            },
            MatchForecasts = new()
            {
                Result =
                [
                    new GatoMestreForecastMatchResponse
                    {
                        HomeTeam = new GatoMestreSimpleTeamReferenceResponse { Abbreviation = "GRE", Name = "Grêmio" },
                        AwayTeam = new GatoMestreSimpleTeamReferenceResponse { Abbreviation = "VIT", Name = "Vitória" },
                        ResultCode = 1,
                        RiskLevel = "baixo",
                    },
                ],
            },
            ClubAverageRankings = new()
            {
                Result =
                [
                    new GatoMestreClubAverageRankingResponse
                    {
                        ClubId = "284",
                        Averages = new GatoMestreClubAverageRankingBucketsResponse
                        {
                            Overall = new GatoMestreAveragesBucketResponse
                            {
                                Total = new GatoMestreNumericSnapshotResponse { Num = 55m },
                                Moving = new GatoMestreNumericSnapshotResponse { Num = 57m },
                            },
                            Home = new GatoMestreAveragesBucketResponse
                            {
                                Total = new GatoMestreNumericSnapshotResponse { Num = 61m },
                                Moving = new GatoMestreNumericSnapshotResponse { Num = 62m },
                            },
                            Away = new GatoMestreAveragesBucketResponse
                            {
                                Total = new GatoMestreNumericSnapshotResponse { Num = 49m },
                                Moving = new GatoMestreNumericSnapshotResponse { Num = 50m },
                            },
                        },
                    },
                ],
            },
            RoundTips = new()
            {
                Result =
                [
                    new GatoMestreTipItemResponse
                    {
                        Hot = true,
                        Type = "team",
                        Info = JsonDocument.Parse("""{"team_id":284}""").RootElement.Clone(),
                    },
                    new GatoMestreTipItemResponse
                    {
                        Hot = false,
                        Type = "match",
                        Info = JsonDocument.Parse("""{"homeTeam":{"abbr":"GRE"},"awayTeam":{"abbr":"VIT"}}""").RootElement.Clone(),
                    },
                    new GatoMestreTipItemResponse
                    {
                        Hot = true,
                        Type = "insight",
                        Info = JsonDocument.Parse("""{"homeTeam":{"abbr":"GRE"},"awayTeam":{"abbr":"VIT"},"confidence":0.8,"verdict":"home_win"}""").RootElement.Clone(),
                    },
                ],
            },
        }, failureLogService, syncExecutionLogService);

        var result = await service.SyncRoundAsync(2026, 1);

        var athleteRound = await dbContext.AthleteRounds.Include(item => item.Context).SingleAsync();
        var homeClubRound = await dbContext.ClubRounds
            .Include(item => item.Context)
            .Include(item => item.PositionStats)
            .SingleAsync(item => item.Club.CartolaId == 284);

        Assert.Equal(SyncExecutionStatus.Success, result.Status);
        Assert.NotNull(athleteRound.Context);
        Assert.Equal(27.16m, athleteRound.Context!.TeamAverageScore);
        Assert.Equal(1.25m, athleteRound.Context.IndexPointsPerCartoleta);
        Assert.Equal(4.75m, athleteRound.Context.IndexOffensiveEfficiency);
        Assert.NotNull(homeClubRound.Context);
        Assert.Equal(44m, homeClubRound.Context!.PreviousOverallAverage);
        Assert.Equal(61m, homeClubRound.Context.RankingsHomeAverage);
        Assert.Equal(53m, homeClubRound.Context.FavoriteWinProbability);
        Assert.Equal(1, homeClubRound.Context.ForecastExpectedResult);
        Assert.Equal("baixo", homeClubRound.Context.ForecastRiskLevel);
        Assert.Equal(1, homeClubRound.Context.TeamTipsCount);
        Assert.Equal(1, homeClubRound.Context.MatchTipsCount);
        Assert.Equal(1, homeClubRound.Context.InsightCount);
        Assert.Equal(1, homeClubRound.Context.PositiveInsightCount);
        Assert.Equal(0, homeClubRound.Context.NegativeInsightCount);
        Assert.Contains(homeClubRound.PositionStats, item => item.PositionId == 3 && item.DistributionValue > 0);
    }

    [Fact]
    public async Task SyncRoundAsync_LogsOptionalEndpointWarningsAndKeepsCoreClubData_WhenOptionalPayloadsFail()
    {
        await using var dbContext = CreateDbContext();
        var failureLogService = new FakeIntegrationFailureLogService();
        var syncExecutionLogService = new FakeSyncExecutionLogService();
        var service = new GatoMestreSyncService(dbContext, new FakeGatoMestreApiClient
        {
            Teams = new Dictionary<int, GatoMestreTeamResponse>
            {
                [284] = CreateTeam(284, "GRE", "gremio", "GrÃªmio"),
                [287] = CreateTeam(287, "VIT", "vitoria", "VitÃ³ria"),
            },
            AthletesRound = new GatoMestreAthletesRoundResponse(),
            ClubConfrontations = new Dictionary<int, GatoMestreClubConfrontationResponse>
            {
                [284] = new()
                {
                    Round = 1,
                    Home = CreateClubBlock(284, "mandante", 53m, 21m, 49m, 45m, 47m, [(3, 8m, 12m), (5, 14m, 22m)]),
                    Away = CreateClubBlock(287, "visitante", 53m, 21m, 35m, 40m, 38m, [(3, 9m, 8m), (5, 16m, 10m)]),
                },
                [287] = new()
                {
                    Round = 1,
                    Home = CreateClubBlock(284, "mandante", 53m, 21m, 49m, 45m, 47m, [(3, 8m, 12m), (5, 14m, 22m)]),
                    Away = CreateClubBlock(287, "visitante", 53m, 21m, 35m, 40m, 38m, [(3, 9m, 8m), (5, 16m, 10m)]),
                },
            },
            ThrowRoundTips = CreateOptionalPayloadException("https://api.gatomestre.globo.com/api/v2/dicas/2026/campeonato-brasileiro/rodadas/1?a=true"),
            ThrowFavoriteMatches = CreateOptionalPayloadException("https://api.gatomestre.globo.com/api/v2/equipes/2026/campeonato-brasileiro/favoritos/rodadas/1?a=true"),
            ThrowMatchForecasts = CreateOptionalPayloadException("https://api.gatomestre.globo.com/api/v2/equipes/2026/campeonato-brasileiro/previsoes/rodadas/1?a=true"),
            ThrowClubAverageRankings = CreateOptionalPayloadException("https://api.gatomestre.globo.com/api/v2/rankings/2026/campeonato-brasileiro/clubes/medias/rodadas/1?a=true"),
        }, failureLogService, syncExecutionLogService);

        var result = await service.SyncRoundAsync(2026, 1);

        var clubRounds = await dbContext.ClubRounds
            .Include(item => item.Context)
            .Include(item => item.PositionStats)
            .OrderBy(item => item.ClubId)
            .ToListAsync();

        Assert.Equal(SyncExecutionStatus.Partial, result.Status);
        Assert.True(result.IsPartialSync);
        Assert.True(result.ClubRoundDataAvailable);
        Assert.Equal(2, result.ClubRoundsUpserted);
        Assert.Equal(4, result.ClubRoundPositionsUpserted);
        Assert.Equal(4, failureLogService.Entries.Count);
        Assert.Equal(4, syncExecutionLogService.UpdatedEntries.Single().Entry.WarningLogsCount);
        Assert.Contains("tip-based club enrichment was skipped", result.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.All(clubRounds, round => Assert.NotNull(round.Context));
        Assert.All(clubRounds, round => Assert.True(round.Context!.PreviousOverallAverage > 0m));
        Assert.All(clubRounds, round => Assert.Equal(0m, round.Context!.FavoriteWinProbability));
        Assert.All(clubRounds, round => Assert.Equal(0, round.Context!.TeamTipsCount));
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

    [Fact]
    public async Task SyncRoundAsync_ReturnsPartial_WhenRequestedRoundIsNotPublishedYet()
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
            ThrowAthletesRound = new ExternalApiException(
                sourceSystem: "GatoMestre",
                errorLocation: "GatoMestreApiClient.GetRequiredAsync",
                requestMethod: "GET",
                requestUrl: "https://api.gatomestre.globo.com/api/v2/atletas/2026/campeonato-brasileiro/home/atletas/rodadas/7?a=true",
                requestHeadersJson: "{}",
                requestPayload: string.Empty,
                responseStatusCode: 404,
                responseReasonPhrase: "Not Found",
                responseHeadersJson: "{}",
                responsePayload: """{"error":true,"message":"Conteúdo não encontrado"}""",
                message: "The Gato Mestre API returned status code 404 for the round endpoint."),
        }, failureLogService, syncExecutionLogService);

        var result = await service.SyncRoundAsync(2026, 7);

        Assert.Equal(SyncExecutionStatus.Partial, result.Status);
        Assert.True(result.IsPartialSync);
        Assert.False(result.QualityCriteriaSatisfied);
        Assert.Equal(0, result.AthleteRoundsUpserted);
        Assert.Contains("not published yet", result.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(failureLogService.Entries);
        Assert.Contains(syncExecutionLogService.UpdatedEntries, entry =>
            entry.Entry.Status == SyncExecutionStatus.Partial &&
            entry.Entry.ErrorCode == IntegrationErrorCode.RoundNotPublished);
    }

    [Fact]
    public async Task SyncRoundAsync_LogsClubWarningAndContinues_WhenConfrontationPayloadFails()
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
                        Averages = JsonDocument.Parse("""{"geral":{"movel":{"num":4.33},"total":{"num":4.33}}}""").RootElement.Clone(),
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
                            Round = 6,
                            WasInGame = true,
                            GamesCount = 3,
                            StatusName = "Provavel",
                            Price = new GatoMestrePriceSnapshotResponse { Num = 5.3m, Variation = -0.36m },
                            Points = new GatoMestreNumericSnapshotResponse { Num = 0.8m, Variation = -3.4m },
                            Averages = new GatoMestreDetailedAveragesResponse
                            {
                                Overall = new GatoMestreAveragesBucketResponse
                                {
                                    Moving = new GatoMestreNumericSnapshotResponse { Num = 4.33m },
                                    Total = new GatoMestreNumericSnapshotResponse { Num = 4.33m },
                                },
                            },
                        }
                    ],
                },
            },
            ThrowClubConfrontation = new ExternalApiException(
                sourceSystem: "GatoMestre",
                errorLocation: "GatoMestreApiClient.GetRequiredAsync",
                requestMethod: "GET",
                requestUrl: "https://api.gatomestre.globo.com/api/v1/conteudos/confrontos/equipe/293?a=true",
                requestHeadersJson: "{}",
                requestPayload: string.Empty,
                responseStatusCode: 200,
                responseReasonPhrase: "OK",
                responseHeadersJson: "{}",
                responsePayload: "<html>unexpected payload</html>",
                message: "The Gato Mestre API payload for the confrontation endpoint could not be deserialized.",
                innerException: new JsonException("Unexpected token while reading confrontation payload.")),
        }, failureLogService, syncExecutionLogService);

        var result = await service.SyncRoundAsync(2026, 6);
        var athleteRound = await dbContext.AthleteRounds.SingleAsync();

        Assert.Equal(SyncExecutionStatus.Partial, result.Status);
        Assert.True(result.IsPartialSync);
        Assert.False(result.ClubRoundDataAvailable);
        Assert.Equal(1, result.AthleteRoundsUpserted);
        Assert.Equal(1, result.AthleteDetailsFetched);
        Assert.Single(failureLogService.Entries);
        Assert.Equal(IntegrationLogSeverity.Warning, failureLogService.Entries[0].Severity);
        Assert.Equal(IntegrationErrorCode.ExternalJsonSchemaChanged, failureLogService.Entries[0].ErrorCode);
        Assert.Equal(6, athleteRound.RoundId);
        Assert.Contains(syncExecutionLogService.UpdatedEntries, entry =>
            entry.Entry.Status == SyncExecutionStatus.Partial &&
            entry.Entry.WarningLogsCount == 1);
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
            PreviousOverallAverage = overallAverage - 1m,
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
            PointsBalance = new GatoMestreClubPointsBalanceResponse
            {
                OverallPointsConcededAverage = stats.Sum(item => item.Ceded),
                OverallPointsConqueredAverage = stats.Sum(item => item.Conquered),
                HomePointsConcededAverage = stats.Sum(item => item.Ceded) / Math.Max(1, stats.Count),
                HomePointsConqueredAverage = stats.Sum(item => item.Conquered) / Math.Max(1, stats.Count),
                AwayPointsConcededAverage = stats.Sum(item => item.Ceded) / Math.Max(1, stats.Count),
                AwayPointsConqueredAverage = stats.Sum(item => item.Conquered) / Math.Max(1, stats.Count),
            },
            PositionDistribution = stats.Select(item => new GatoMestrePositionDistributionResponse
            {
                PositionId = item.PositionId,
                Value = item.PositionId * 0.1m,
            }).ToList(),
            Performance = new GatoMestreClubPerformanceResponse
            {
                Wins = new GatoMestreHistoricalResultResponse
                {
                    Rate = new GatoMestreHistoricalRateBucketResponse { Overall = 48m, Home = 60m, Away = 36m },
                },
                Draws = new GatoMestreHistoricalResultResponse
                {
                    Rate = new GatoMestreHistoricalRateBucketResponse { Overall = 26m, Home = 20m, Away = 32m },
                },
                Losses = new GatoMestreHistoricalResultResponse
                {
                    Rate = new GatoMestreHistoricalRateBucketResponse { Overall = 26m, Home = 20m, Away = 32m },
                },
            },
        };
    }

    private static ExternalApiException CreateOptionalPayloadException(string requestUrl)
        => new(
            sourceSystem: "GatoMestre",
            errorLocation: "GatoMestreApiClient.GetRequiredAsync",
            requestMethod: "GET",
            requestUrl: requestUrl,
            requestHeadersJson: "{}",
            requestPayload: string.Empty,
            responseStatusCode: 500,
            responseReasonPhrase: "Internal Server Error",
            responseHeadersJson: "{}",
            responsePayload: """{"error":true,"message":"Synthetic optional payload failure"}""",
            message: "The Gato Mestre API returned an application error for an optional round endpoint.");

    private sealed class FakeGatoMestreApiClient : IGatoMestreApiClient
    {
        public Dictionary<int, GatoMestreTeamResponse> Teams { get; init; } = [];

        public GatoMestreAthletesRoundResponse AthletesRound { get; init; } = new();

        public Dictionary<int, GatoMestreAthleteDetailResponse> AthleteDetails { get; init; } = [];

        public Dictionary<int, GatoMestreAthleteRoundContentResponse> AthleteRoundContents { get; init; } = [];

        public Dictionary<int, GatoMestreClubConfrontationResponse> ClubConfrontations { get; init; } = [];

        public GatoMestreRoundCollectionResponse<GatoMestreTipItemResponse> RoundTips { get; init; } = new();

        public GatoMestreRoundCollectionResponse<GatoMestreFavoriteMatchResponse> FavoriteMatches { get; init; } = new();

        public GatoMestreRoundCollectionResponse<GatoMestreForecastMatchResponse> MatchForecasts { get; init; } = new();

        public GatoMestreRoundCollectionResponse<GatoMestreClubAverageRankingResponse> ClubAverageRankings { get; init; } = new();

        public ExternalApiException? ThrowAthletesRound { get; init; }

        public ExternalApiException? ThrowAthleteDetail { get; init; }

        public ExternalApiException? ThrowAthleteRoundContent { get; init; }

        public ExternalApiException? ThrowClubConfrontation { get; init; }

        public ExternalApiException? ThrowRoundTips { get; init; }

        public ExternalApiException? ThrowFavoriteMatches { get; init; }

        public ExternalApiException? ThrowMatchForecasts { get; init; }

        public ExternalApiException? ThrowClubAverageRankings { get; init; }

        public Task<Dictionary<int, GatoMestreTeamResponse>> GetTeamsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Teams);

        public Task<GatoMestreAthletesRoundResponse> GetAthletesRoundAsync(int season, int round, CancellationToken cancellationToken = default)
        {
            if (ThrowAthletesRound is not null)
            {
                throw ThrowAthletesRound;
            }

            return Task.FromResult(AthletesRound);
        }

        public Task<GatoMestreAthleteDetailResponse> GetAthleteDetailAsync(int athleteId, CancellationToken cancellationToken = default)
        {
            if (ThrowAthleteDetail is not null)
            {
                throw ThrowAthleteDetail;
            }

            return Task.FromResult(AthleteDetails[athleteId]);
        }

        public Task<GatoMestreAthleteRoundContentResponse> GetAthleteRoundContentAsync(int athleteId, int round, CancellationToken cancellationToken = default)
        {
            if (ThrowAthleteRoundContent is not null)
            {
                throw ThrowAthleteRoundContent;
            }

            return Task.FromResult(AthleteRoundContents.TryGetValue(athleteId, out var content)
                ? content
                : new GatoMestreAthleteRoundContentResponse());
        }

        public Task<GatoMestreClubConfrontationResponse> GetClubConfrontationAsync(int clubId, CancellationToken cancellationToken = default)
        {
            if (ThrowClubConfrontation is not null)
            {
                throw ThrowClubConfrontation;
            }

            return Task.FromResult(ClubConfrontations.TryGetValue(clubId, out var confrontation)
                ? confrontation
                : new GatoMestreClubConfrontationResponse());
        }

        public Task<GatoMestreRoundCollectionResponse<GatoMestreTipItemResponse>> GetRoundTipsAsync(int season, int round, CancellationToken cancellationToken = default)
        {
            if (ThrowRoundTips is not null)
            {
                throw ThrowRoundTips;
            }

            return Task.FromResult(RoundTips);
        }

        public Task<GatoMestreRoundCollectionResponse<GatoMestreFavoriteMatchResponse>> GetFavoriteMatchesAsync(int season, int round, CancellationToken cancellationToken = default)
        {
            if (ThrowFavoriteMatches is not null)
            {
                throw ThrowFavoriteMatches;
            }

            return Task.FromResult(FavoriteMatches);
        }

        public Task<GatoMestreRoundCollectionResponse<GatoMestreForecastMatchResponse>> GetMatchForecastsAsync(int season, int round, CancellationToken cancellationToken = default)
        {
            if (ThrowMatchForecasts is not null)
            {
                throw ThrowMatchForecasts;
            }

            return Task.FromResult(MatchForecasts);
        }

        public Task<GatoMestreRoundCollectionResponse<GatoMestreClubAverageRankingResponse>> GetClubAverageRankingsAsync(int season, int round, CancellationToken cancellationToken = default)
        {
            if (ThrowClubAverageRankings is not null)
            {
                throw ThrowClubAverageRankings;
            }

            return Task.FromResult(ClubAverageRankings);
        }
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
