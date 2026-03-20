using CartolaOptimization.Domain.Constants;
using CartolaOptimization.Domain.Entities;
using CartolaOptimization.Infrastructure.Diagnostics;
using CartolaOptimization.Infrastructure.Integrations.Cartola.Models;
using CartolaOptimization.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace CartolaOptimization.Tests;

public sealed class CartolaSyncServiceTests
{
    [Fact]
    public async Task SyncClosedRoundAsync_FinalizesExistingRoundAndMarksItHistoricalClosed()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var failureLogService = new RecordingIntegrationFailureLogService();
        var syncExecutionLogService = new RecordingSyncExecutionLogService();

        var club = new Club(0, 293, "CAP", "athletico-pr", "Athletico-PR", "https://example.com/cap.png");
        var position = new Position(0, 3, "Zagueiro", "zag");
        var status = new AthleteStatus(0, 7, "Provavel");
        var athlete = new Athlete(0, 37457, "Atleta 37457", "Atleta 37457");

        dbContext.Clubs.Add(club);
        dbContext.Positions.Add(position);
        dbContext.AthleteStatuses.Add(status);
        dbContext.Athletes.Add(athlete);
        await dbContext.SaveChangesAsync();

        var clubRound = new ClubRound(0, 5, club.Id, 40m, 30m, 30m, 48m, 46m, 51m);
        dbContext.ClubRounds.Add(clubRound);
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
            1.5m,
            3.1m,
            3.1m,
            3.2m,
            3.0m,
            0.1m,
            7.5m,
            false)
        {
            DataPhase = RoundDataPhaseValues.Unknown,
            Scouts = [],
        });
        await dbContext.SaveChangesAsync();

        var service = new CartolaSyncService(
            dbContext,
            new FakeCartolaApiClient
            {
                MarketStatus = new CartolaMarketStatusResponse
                {
                    Season = 2026,
                    CurrentRound = 7,
                },
                AthleteScores = new CartolaAthleteScoresResponse
                {
                    Round = 5,
                    TotalAthletes = 1,
                    Clubs = new Dictionary<int, CartolaClubResponse>
                    {
                        [293] = CreateClub(293, "CAP", "athletico-pr", "Athletico-PR"),
                    },
                    Positions = new Dictionary<int, CartolaPositionResponse>
                    {
                        [3] = new() { Id = 3, Name = "Zagueiro", Abbreviation = "zag" },
                    },
                    Athletes = new Dictionary<int, CartolaAthleteScoreResponse>
                    {
                        [37457] = new()
                        {
                            Nickname = "Atleta 37457",
                            Score = 12.3m,
                            PositionId = 3,
                            ClubId = 293,
                            WasInGame = true,
                            Scout = new Dictionary<string, decimal> { ["DS"] = 2m, ["SG"] = 1m },
                        },
                    },
                },
            },
            failureLogService,
            syncExecutionLogService);

        var result = await service.SyncClosedRoundAsync(5);
        var athleteRound = await dbContext.AthleteRounds.Include(item => item.Scouts).SingleAsync();

        Assert.Equal(SyncExecutionStatus.Success, result.Status);
        Assert.True(result.QualityCriteriaSatisfied);
        Assert.Equal(1, result.AthleteRoundsUpserted);
        Assert.Equal(0, result.ScorePayloadRowsWithoutLocalRound);
        Assert.Equal(0, result.AthleteRoundsMissingScorePayload);
        Assert.Equal(RoundDataPhaseValues.HistoricalClosed, athleteRound.DataPhase);
        Assert.Equal(12.3m, athleteRound.ScoreNum);
        Assert.True(athleteRound.WasInGame);
        Assert.True(athleteRound.IsTrainingEligible);
        Assert.Equal("Complete", athleteRound.TrainingReadinessStatus);
        Assert.Equal(2, athleteRound.Scouts.Count);
        Assert.Empty(failureLogService.Entries);
        Assert.Contains(syncExecutionLogService.UpdatedEntries, entry => entry.Entry.Status == SyncExecutionStatus.Success);
    }

    [Fact]
    public async Task SyncClosedRoundAsync_ReturnsPartial_WhenScorePayloadContainsAthleteWithoutLocalBaseline()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var failureLogService = new RecordingIntegrationFailureLogService();
        var syncExecutionLogService = new RecordingSyncExecutionLogService();

        var club = new Club(0, 293, "CAP", "athletico-pr", "Athletico-PR", "https://example.com/cap.png");
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
            1,
            1,
            0,
            0m,
            2m,
            2m,
            2m,
            0m,
            0m,
            5m,
            false)
        {
            DataPhase = RoundDataPhaseValues.Unknown,
            Scouts = [],
        });
        await dbContext.SaveChangesAsync();

        var service = new CartolaSyncService(
            dbContext,
            new FakeCartolaApiClient
            {
                MarketStatus = new CartolaMarketStatusResponse
                {
                    Season = 2026,
                    CurrentRound = 8,
                },
                AthleteScores = new CartolaAthleteScoresResponse
                {
                    Round = 5,
                    TotalAthletes = 2,
                    Clubs = new Dictionary<int, CartolaClubResponse>
                    {
                        [293] = CreateClub(293, "CAP", "athletico-pr", "Athletico-PR"),
                    },
                    Positions = new Dictionary<int, CartolaPositionResponse>
                    {
                        [3] = new() { Id = 3, Name = "Zagueiro", Abbreviation = "zag" },
                    },
                    Athletes = new Dictionary<int, CartolaAthleteScoreResponse>
                    {
                        [37457] = new()
                        {
                            Nickname = "Atleta 37457",
                            Score = 8.8m,
                            PositionId = 3,
                            ClubId = 293,
                            WasInGame = true,
                        },
                        [99999] = new()
                        {
                            Nickname = "Sem baseline local",
                            Score = 4.4m,
                            PositionId = 3,
                            ClubId = 293,
                            WasInGame = true,
                        },
                    },
                },
            },
            failureLogService,
            syncExecutionLogService);

        var result = await service.SyncClosedRoundAsync(5);

        Assert.Equal(SyncExecutionStatus.Partial, result.Status);
        Assert.False(result.QualityCriteriaSatisfied);
        Assert.Equal(1, result.ScorePayloadRowsWithoutLocalRound);
        Assert.Contains("could not be mapped to a local baseline", result.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(failureLogService.Entries);
        Assert.Contains(syncExecutionLogService.UpdatedEntries, entry =>
            entry.Entry.Status == SyncExecutionStatus.Partial &&
            entry.Entry.ErrorCode == IntegrationErrorCode.IncompleteHistoricalBaseline);
    }

    [Fact]
    public async Task SyncClosedRoundAsync_TreatsNullScoutAsEmptyCollection()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var failureLogService = new RecordingIntegrationFailureLogService();
        var syncExecutionLogService = new RecordingSyncExecutionLogService();

        var club = new Club(0, 293, "CAP", "athletico-pr", "Athletico-PR", "https://example.com/cap.png");
        var position = new Position(0, 6, "Tecnico", "tec");
        var status = new AthleteStatus(0, 7, "Provavel");
        var athlete = new Athlete(0, 92273, "Odair Hellmann", "Odair Hellmann");

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
            1,
            1,
            0,
            0m,
            1.5m,
            1.5m,
            1.5m,
            0m,
            0m,
            9m,
            false)
        {
            DataPhase = RoundDataPhaseValues.Unknown,
            Scouts = [],
        });
        await dbContext.SaveChangesAsync();

        var service = new CartolaSyncService(
            dbContext,
            new FakeCartolaApiClient
            {
                MarketStatus = new CartolaMarketStatusResponse
                {
                    Season = 2026,
                    CurrentRound = 8,
                },
                AthleteScores = new CartolaAthleteScoresResponse
                {
                    Round = 5,
                    TotalAthletes = 1,
                    Clubs = new Dictionary<int, CartolaClubResponse>
                    {
                        [293] = CreateClub(293, "CAP", "athletico-pr", "Athletico-PR"),
                    },
                    Positions = new Dictionary<int, CartolaPositionResponse>
                    {
                        [6] = new() { Id = 6, Name = "Tecnico", Abbreviation = "tec" },
                    },
                    Athletes = new Dictionary<int, CartolaAthleteScoreResponse>
                    {
                        [92273] = new()
                        {
                            Nickname = "Odair Hellmann",
                            Score = 0m,
                            PositionId = 6,
                            ClubId = 293,
                            WasInGame = false,
                            Scout = null,
                        },
                    },
                },
            },
            failureLogService,
            syncExecutionLogService);

        var result = await service.SyncClosedRoundAsync(5);
        var athleteRound = await dbContext.AthleteRounds.Include(item => item.Scouts).SingleAsync();

        Assert.Equal(SyncExecutionStatus.Success, result.Status);
        Assert.Equal(1, result.AthleteRoundsUpserted);
        Assert.Empty(athleteRound.Scouts);
        Assert.Equal(0m, athleteRound.ScoreNum);
        Assert.False(athleteRound.WasInGame);
        Assert.Empty(failureLogService.Entries);
        Assert.Contains(syncExecutionLogService.UpdatedEntries, entry => entry.Entry.Status == SyncExecutionStatus.Success);
    }

    [Fact]
    public async Task SyncClosedRoundAsync_LogsFailure_WhenRequestedRoundIsNotHistorical()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var failureLogService = new RecordingIntegrationFailureLogService();
        var syncExecutionLogService = new RecordingSyncExecutionLogService();

        var service = new CartolaSyncService(
            dbContext,
            new FakeCartolaApiClient
            {
                MarketStatus = new CartolaMarketStatusResponse
                {
                    Season = 2026,
                    CurrentRound = 5,
                },
            },
            failureLogService,
            syncExecutionLogService);

        var exception = await Assert.ThrowsAsync<DataCollectionProcessException>(() => service.SyncClosedRoundAsync(5));

        Assert.Equal("Cartola", exception.SourceSystem);
        Assert.Single(failureLogService.Entries);
        Assert.Equal(IntegrationErrorCode.IncompleteHistoricalBaseline, failureLogService.Entries[0].ErrorCode);
        Assert.Contains(syncExecutionLogService.UpdatedEntries, entry =>
            entry.Entry.Status == SyncExecutionStatus.Failed &&
            entry.Entry.ErrorCode == IntegrationErrorCode.IncompleteHistoricalBaseline);
    }

    private static CartolaClubResponse CreateClub(int id, string abbreviation, string slug, string nickname)
    {
        return new CartolaClubResponse
        {
            Id = id,
            Abbreviation = abbreviation,
            Slug = slug,
            Name = nickname,
            Nickname = nickname,
            FantasyName = nickname,
            Shields = new CartolaShieldResponse
            {
                Size60 = $"https://example.com/{slug}.png",
            },
        };
    }
}
