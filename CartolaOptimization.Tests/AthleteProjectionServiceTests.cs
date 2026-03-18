using CartolaOptimization.Infrastructure.Context;
using CartolaOptimization.Infrastructure.Services;
using CartolaOptimization.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CartolaOptimization.Tests;

public sealed class AthleteProjectionServiceTests
{
    [Fact]
    public async Task GetRoundProjectionAsync_ReturnsRankedAthletesWithMatchAndOpponentContext()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var (clubA, clubB, defenderPosition, forwardPosition, probableStatus, defender, forward) = await SeedProjectionBaseAsync(dbContext);

        dbContext.Matches.Add(new Match(
            0,
            5001,
            7,
            DateTime.UtcNow,
            "Arena",
            true,
            clubA.Id,
            3,
            "VVE",
            null,
            clubB.Id,
            12,
            "DED",
            null));

        var clubRoundA = new ClubRound(0, 7, clubA.Id, 60m, 20m, 20m, 52m, 50m, 68m)
        {
            PositionStats = [],
        };
        var clubRoundB = new ClubRound(0, 7, clubB.Id, 20m, 20m, 60m, 40m, 38m, 35m)
        {
            PositionStats = [],
        };

        dbContext.ClubRounds.AddRange(clubRoundA, clubRoundB);
        await dbContext.SaveChangesAsync();

        dbContext.ClubRoundPositions.Add(new ClubRoundPosition(0, clubRoundB.Id, defenderPosition.Id, 8.5m, 2.2m));
        dbContext.ClubRoundPositions.Add(new ClubRoundPosition(0, clubRoundB.Id, forwardPosition.Id, 1.5m, 3.0m));

        dbContext.AthleteRounds.AddRange(
            new AthleteRound(0, 6, defender.Id, clubA.Id, defenderPosition.Id, probableStatus.Id, 6, 3, 3, 8m, 7m, 6.5m, 7.8m, 5.2m, 1.2m, 13m, true)
            {
                Scouts = [],
            },
            new AthleteRound(0, 6, forward.Id, clubA.Id, forwardPosition.Id, probableStatus.Id, 6, 3, 3, 5m, 4m, 4.2m, 4.1m, 4.3m, 0.2m, 10m, true)
            {
                Scouts = [],
            });
        await dbContext.SaveChangesAsync();

        var service = new AthleteProjectionService(dbContext);

        var result = await service.GetRoundProjectionAsync(7);

        Assert.Equal(2, result.TotalAthletesConsidered);
        Assert.Equal(2, result.TotalAthletesProjected);
        Assert.Equal("baseline-v1", result.ModelVersion);
        Assert.Equal(2, result.Athletes.Count);
        Assert.True(result.Athletes.First().ExpectedScore >= result.Athletes.Last().ExpectedScore);

        var topProjection = result.Athletes.First();
        Assert.Equal(defender.CartolaId, topProjection.AthleteCartolaId);
        Assert.Equal(clubB.CartolaId, topProjection.OpponentClubCartolaId);
        Assert.Equal(clubB.Nickname, topProjection.OpponentClubName);
        Assert.True(topProjection.IsHomeMatch);
        Assert.True(topProjection.ExpectedScore > 0m);
        Assert.True(topProjection.ConfidenceScore >= 70m);
        Assert.True(topProjection.OpponentPositionAdjustment > 0m);
    }

    [Fact]
    public async Task GetRoundProjectionAsync_AppliesFiltersAndAvailabilityPenalty()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var (clubA, clubB, defenderPosition, _, probableStatus, defender, _) = await SeedProjectionBaseAsync(dbContext);
        var suspendedStatus = new AthleteStatus(0, 3, "Suspenso");
        var suspendedAthlete = new Athlete(0, 8003, "Suspenso", "Suspenso");

        dbContext.AthleteStatuses.Add(suspendedStatus);
        dbContext.Athletes.Add(suspendedAthlete);
        await dbContext.SaveChangesAsync();

        dbContext.Matches.Add(new Match(
            0,
            5002,
            7,
            DateTime.UtcNow,
            "Arena",
            true,
            clubA.Id,
            3,
            "VVE",
            null,
            clubB.Id,
            12,
            "DED",
            null));

        var clubRoundA = new ClubRound(0, 7, clubA.Id, 55m, 25m, 20m, 51m, 49m, 63m)
        {
            PositionStats = [],
        };
        var clubRoundB = new ClubRound(0, 7, clubB.Id, 20m, 25m, 55m, 39m, 37m, 36m)
        {
            PositionStats = [],
        };

        dbContext.ClubRounds.AddRange(clubRoundA, clubRoundB);
        await dbContext.SaveChangesAsync();
        dbContext.ClubRoundPositions.Add(new ClubRoundPosition(0, clubRoundB.Id, defenderPosition.Id, 6m, 2m));

        dbContext.AthleteRounds.AddRange(
            new AthleteRound(0, 6, defender.Id, clubA.Id, defenderPosition.Id, probableStatus.Id, 6, 3, 3, 6m, 6.5m, 6m, 7m, 5m, 0.5m, 12m, true)
            {
                Scouts = [],
            },
            new AthleteRound(0, 6, suspendedAthlete.Id, clubA.Id, defenderPosition.Id, suspendedStatus.Id, 6, 3, 3, 6m, 6.5m, 6m, 7m, 5m, 0.5m, 12m, true)
            {
                Scouts = [],
            });
        await dbContext.SaveChangesAsync();

        var service = new AthleteProjectionService(dbContext);

        var filtered = await service.GetRoundProjectionAsync(7, positionCartolaId: defenderPosition.CartolaId, clubCartolaId: clubA.CartolaId, take: 10);

        Assert.Equal(2, filtered.TotalAthletesProjected);
        Assert.All(filtered.Athletes, item => Assert.Equal(defenderPosition.CartolaId, item.PositionCartolaId));
        Assert.All(filtered.Athletes, item => Assert.Equal(clubA.CartolaId, item.ClubCartolaId));

        var probableProjection = filtered.Athletes.Single(item => item.AthleteCartolaId == defender.CartolaId);
        var suspendedProjection = filtered.Athletes.Single(item => item.AthleteCartolaId == suspendedAthlete.CartolaId);

        Assert.True(probableProjection.ExpectedScore > suspendedProjection.ExpectedScore);
        Assert.True(probableProjection.ConfidenceScore > suspendedProjection.ConfidenceScore);
        Assert.Equal(0.15m, suspendedProjection.StatusMultiplier);
        Assert.Contains("availability status reduced", suspendedProjection.ProjectionNotes, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<(Club ClubA, Club ClubB, Position DefenderPosition, Position ForwardPosition, AthleteStatus ProbableStatus, Athlete Defender, Athlete Forward)> SeedProjectionBaseAsync(AppDbContext dbContext)
    {
        var clubA = new Club(0, 293, "CAP", "athletico-pr", "Athletico-PR", "https://example.com/cap.png");
        var clubB = new Club(0, 262, "FLA", "flamengo", "Flamengo", "https://example.com/fla.png");
        var defenderPosition = new Position(0, 3, "Zagueiro", "zag");
        var forwardPosition = new Position(0, 5, "Atacante", "ata");
        var probableStatus = new AthleteStatus(0, 7, "Provavel");
        var defender = new Athlete(0, 8001, "Defensor", "Defensor");
        var forward = new Athlete(0, 8002, "Atacante", "Atacante");

        dbContext.Clubs.AddRange(clubA, clubB);
        dbContext.Positions.AddRange(defenderPosition, forwardPosition);
        dbContext.AthleteStatuses.Add(probableStatus);
        dbContext.Athletes.AddRange(defender, forward);
        await dbContext.SaveChangesAsync();

        return (clubA, clubB, defenderPosition, forwardPosition, probableStatus, defender, forward);
    }
}
