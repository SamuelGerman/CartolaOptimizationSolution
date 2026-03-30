using CartolaOptimization.Application.Models;
using CartolaOptimization.Domain.Constants;
using CartolaOptimization.Domain.Entities;
using CartolaOptimization.Infrastructure.Context;
using CartolaOptimization.Infrastructure.Diagnostics;
using CartolaOptimization.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace CartolaOptimization.Tests;

public sealed class TrainingDatasetServicesTests
{
    [Fact]
    public async Task RecomputeReadinessAsync_ClassifiesRowsAcrossCompletePartialAndUnfit()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var (club, position, status, athletes) = await SeedTrainingBaseAsync(dbContext);

        var completeClubRound = new ClubRound(0, 3, club.Id, 40m, 30m, 30m, 48m, 45m, 50m)
        {
            PositionStats = [],
        };
        dbContext.ClubRounds.Add(completeClubRound);
        await dbContext.SaveChangesAsync();

        dbContext.ClubRoundPositions.Add(new ClubRoundPosition(0, completeClubRound.Id, position.Id, 1.4m, 6.2m));
        dbContext.ClubRounds.Add(new ClubRound(0, 4, club.Id, 35m, 30m, 35m, 42m, 41m, 40m)
        {
            PositionStats = [],
        });

        dbContext.AthleteRounds.AddRange(
            new AthleteRound(0, 3, athletes[0].Id, club.Id, position.Id, status.Id, 3, 2, 1, 5m, 5m, 5m, 5m, 5m, 0m, 10m, true)
            {
                DataPhase = RoundDataPhaseValues.HistoricalClosed,
                Scouts = [],
            },
            new AthleteRound(0, 4, athletes[1].Id, club.Id, position.Id, status.Id, 2, 1, 1, 2m, 2m, 2m, 2m, 2m, 0m, 8m, true)
            {
                DataPhase = RoundDataPhaseValues.CurrentOpen,
                Scouts = [],
            },
            new AthleteRound(0, 5, athletes[2].Id, club.Id, position.Id, status.Id, 1, 1, 0, 0m, 0m, 0m, 0m, 0m, 0m, 6m, false)
            {
                DataPhase = RoundDataPhaseValues.Unknown,
                Scouts = [],
            });
        await dbContext.SaveChangesAsync();

        var service = new TrainingDatasetPreparationService(
            dbContext,
            new FakeCartolaSyncService(),
            new FakeGatoMestreSyncService());

        var result = await service.RecomputeReadinessAsync(3, 5);
        var athleteRounds = await dbContext.AthleteRounds.OrderBy(item => item.RoundId).ToListAsync();
        var clubRounds = await dbContext.ClubRounds.OrderBy(item => item.Round).ToListAsync();

        Assert.Equal(3, result.AthleteRoundsEvaluated);
        Assert.Equal(1, result.AthleteRoundsComplete);
        Assert.Equal(1, result.AthleteRoundsPartial);
        Assert.Equal(1, result.AthleteRoundsUnfit);
        Assert.Equal(2, result.ClubRoundsEvaluated);
        Assert.Equal(1, result.ClubRoundsComplete);
        Assert.Equal(1, result.ClubRoundsPartial);
        Assert.Equal("Complete", athleteRounds[0].TrainingReadinessStatus);
        Assert.True(athleteRounds[0].IsTrainingEligible);
        Assert.Equal("UnfitForTraining", athleteRounds[1].TrainingReadinessStatus);
        Assert.Equal("Partial", athleteRounds[2].TrainingReadinessStatus);
        Assert.Equal("Complete", clubRounds[0].TrainingReadinessStatus);
        Assert.Equal("Partial", clubRounds[1].TrainingReadinessStatus);
    }

    [Fact]
    public async Task RecomputeReadinessAsync_RequiresCompleteClubRoundForAthleteEligibility()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var (club, position, status, athletes) = await SeedTrainingBaseAsync(dbContext);

        dbContext.ClubRounds.Add(new ClubRound(0, 3, club.Id, 40m, 30m, 30m, 48m, 45m, 50m)
        {
            PositionStats = [],
        });

        dbContext.AthleteRounds.Add(new AthleteRound(0, 3, athletes[0].Id, club.Id, position.Id, status.Id, 3, 2, 1, 5m, 5m, 5m, 5m, 5m, 0m, 10m, true)
        {
            DataPhase = RoundDataPhaseValues.HistoricalClosed,
            Scouts = [],
        });
        await dbContext.SaveChangesAsync();

        var service = new TrainingDatasetPreparationService(
            dbContext,
            new FakeCartolaSyncService(),
            new FakeGatoMestreSyncService());

        await service.RecomputeReadinessAsync(3, 3);
        var athleteRound = await dbContext.AthleteRounds.SingleAsync();

        Assert.False(athleteRound.IsTrainingEligible);
        Assert.Equal("Partial", athleteRound.TrainingReadinessStatus);
        Assert.Contains("missing or incomplete", athleteRound.TrainingReadinessReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BackfillCartolaResultsAsync_AggregatesSuccessfulPartialAndFailedRounds()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var service = new TrainingDatasetPreparationService(
            dbContext,
            new FakeCartolaSyncService
            {
                OnSyncClosedRoundAsync = (round, _) => round switch
                {
                    1 => Task.FromResult(new CartolaSyncResult
                    {
                        Status = SyncExecutionStatus.Success,
                        SyncExecutionId = 101,
                        CorrelationId = "cartola-success",
                    }),
                    2 => Task.FromResult(new CartolaSyncResult
                    {
                        Status = SyncExecutionStatus.Partial,
                        SyncExecutionId = 102,
                        CorrelationId = "cartola-partial",
                        Notes = "Partial baseline reconciliation.",
                    }),
                    _ => throw new DataCollectionProcessException(
                        "Cartola",
                        "CartolaSyncService.SyncClosedRoundAsync",
                        "Load closed round score payload",
                        "cartola-failed",
                        77,
                        "Cartola closed-round synchronization failed.",
                        new InvalidOperationException("boom")),
                },
            },
            new FakeGatoMestreSyncService());

        var result = await service.BackfillCartolaResultsAsync(1, 3);

        Assert.Equal(3, result.RequestedRounds);
        Assert.Equal(1, result.SuccessfulRounds);
        Assert.Equal(1, result.PartialRounds);
        Assert.Equal(1, result.FailedRounds);
        Assert.Collection(
            result.Rounds.OrderBy(item => item.Round),
            round1 => Assert.Equal(SyncExecutionStatus.Success, round1.Status),
            round2 =>
            {
                Assert.Equal(SyncExecutionStatus.Partial, round2.Status);
                Assert.Contains("Partial", round2.Notes, StringComparison.OrdinalIgnoreCase);
            },
            round3 =>
            {
                Assert.Equal(SyncExecutionStatus.Failed, round3.Status);
                Assert.Equal(77, round3.ErrorLogId);
            });
    }

    [Fact]
    public async Task TrainingDatasetDiagnosticsService_ReturnsCoverageSummaryAndFilteredLists()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var (club, position, status, athletes) = await SeedTrainingBaseAsync(dbContext);

        dbContext.ClubRounds.AddRange(
            new ClubRound(0, 3, club.Id, 40m, 30m, 30m, 48m, 46m, 50m)
            {
                TrainingReadinessStatus = "Complete",
                TrainingReadinessReason = string.Empty,
                IsTrainingEligible = true,
            },
            new ClubRound(0, 4, club.Id, 35m, 30m, 35m, 42m, 40m, 41m)
            {
                TrainingReadinessStatus = "Partial",
                TrainingReadinessReason = "Club round was synchronized without position statistics, so the enrichment is incomplete for model training.",
                IsTrainingEligible = false,
            });

        dbContext.AthleteRounds.AddRange(
            new AthleteRound(0, 3, athletes[0].Id, club.Id, position.Id, status.Id, 3, 2, 1, 7m, 6m, 6m, 6m, 6m, 0m, 11m, true)
            {
                DataPhase = RoundDataPhaseValues.HistoricalClosed,
                TrainingReadinessStatus = "Complete",
                TrainingReadinessReason = string.Empty,
                IsTrainingEligible = true,
                Scouts = [],
            },
            new AthleteRound(0, 4, athletes[1].Id, club.Id, position.Id, status.Id, 2, 1, 1, 2m, 2m, 2m, 2m, 2m, 0m, 8m, true)
            {
                DataPhase = RoundDataPhaseValues.Unknown,
                TrainingReadinessStatus = "Partial",
                TrainingReadinessReason = "The athlete row is available, but club-level Gato Mestre features for the same round are still missing.",
                IsTrainingEligible = false,
                Scouts = [],
            },
            new AthleteRound(0, 5, athletes[2].Id, club.Id, position.Id, status.Id, 1, 1, 0, 0m, 0m, 0m, 0m, 0m, 0m, 6m, false)
            {
                DataPhase = RoundDataPhaseValues.CurrentOpen,
                TrainingReadinessStatus = "UnfitForTraining",
                TrainingReadinessReason = "The round is still open or current, so its final target value is not considered stable for training.",
                IsTrainingEligible = false,
                Scouts = [],
            });
        await dbContext.SaveChangesAsync();

        var service = new TrainingDatasetDiagnosticsService(dbContext);

        var summary = await service.GetCoverageSummaryAsync(3, 5);
        var partialAthletes = await service.GetAthleteRoundsAsync(3, 5, "Partial", RoundDataPhaseValues.Unknown, false, 10);
        var completeClubs = await service.GetClubRoundsAsync(3, 5, "Complete", true, 10);

        Assert.Equal(3, summary.AthleteRoundsTotal);
        Assert.Equal(1, summary.AthleteRoundsEligible);
        Assert.Equal(1, summary.AthleteRoundsComplete);
        Assert.Equal(1, summary.AthleteRoundsPartial);
        Assert.Equal(1, summary.AthleteRoundsUnfit);
        Assert.Equal(1, summary.AthleteRoundsHistoricalClosed);
        Assert.Equal(1, summary.AthleteRoundsCurrentOpen);
        Assert.Equal(1, summary.AthleteRoundsUnknownPhase);
        Assert.Equal(33.33m, summary.AthleteEligibilityRate);
        Assert.Equal(2, summary.ClubRoundsTotal);
        Assert.Equal(1, summary.ClubRoundsEligible);
        Assert.Equal(50m, summary.ClubEligibilityRate);
        Assert.Equal(2, summary.AthleteTopBlockingReasons.Count);
        Assert.Contains(summary.AthleteTopBlockingReasons, item => item.Reason.Contains("club-level Gato Mestre features", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(summary.AthleteTopBlockingReasons, item => item.Reason.Contains("still open or current", StringComparison.OrdinalIgnoreCase));
        Assert.Single(partialAthletes);
        Assert.Equal(athletes[1].Name, partialAthletes.Single().AthleteName);
        Assert.Single(completeClubs);
        Assert.Equal(club.CartolaId, completeClubs.Single().ClubCartolaId);
    }

    [Fact]
    public async Task TrainingDatasetDiagnosticsService_ReturnsAthleteOnlyAndFullContextDatasets()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var clubA = new Club(0, 293, "CAP", "athletico-pr", "Athletico-PR", "https://example.com/cap.png");
        var clubB = new Club(0, 262, "FLA", "flamengo", "Flamengo", "https://example.com/fla.png");
        var position = new Position(0, 3, "Zagueiro", "zag");
        var status = new AthleteStatus(0, 7, "Provavel");
        var athleteA = new Athlete(0, 1001, "Atleta A", "A");
        var athleteB = new Athlete(0, 1002, "Atleta B", "B");

        dbContext.Clubs.AddRange(clubA, clubB);
        dbContext.Positions.Add(position);
        dbContext.AthleteStatuses.Add(status);
        dbContext.Athletes.AddRange(athleteA, athleteB);
        await dbContext.SaveChangesAsync();

        dbContext.Matches.Add(new Match(
            0,
            9001,
            3,
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

        dbContext.AthleteRounds.AddRange(
            new AthleteRound(0, 3, athleteA.Id, clubA.Id, position.Id, status.Id, 6, 3, 3, 7m, 6m, 5.8m, 6.1m, 5.4m, 0.4m, 12m, true)
            {
                DataPhase = RoundDataPhaseValues.HistoricalClosed,
                Scouts = [],
            },
            new AthleteRound(0, 4, athleteB.Id, clubA.Id, position.Id, status.Id, 5, 3, 2, 4m, 4.5m, 4.3m, 4.7m, 3.9m, -0.2m, 10m, true)
            {
                DataPhase = RoundDataPhaseValues.HistoricalClosed,
                Scouts = [],
            },
            new AthleteRound(0, 5, athleteB.Id, clubA.Id, position.Id, status.Id, 5, 3, 2, 0m, 4.5m, 4.3m, 4.7m, 3.9m, -0.2m, 10m, false)
            {
                DataPhase = RoundDataPhaseValues.CurrentOpen,
                Scouts = [],
            });
        await dbContext.SaveChangesAsync();

        var athleteRounds = await dbContext.AthleteRounds
            .OrderBy(item => item.RoundId)
            .ThenBy(item => item.AthleteId)
            .ToListAsync();

        dbContext.AthleteRoundContexts.Add(
            new AthleteRoundContext(
                0,
                athleteRounds[0].Id,
                64m,
                66m,
                62m,
                60m,
                58m,
                0.44m,
                0.41m,
                6.2m,
                6.0m,
                5.8m,
                5.6m,
                5.7m,
                5.4m,
                5.2m,
                5.0m,
                1.10m,
                0.50m,
                0.90m,
                0.20m,
                0.47m,
                0.39m,
                0.35m,
                0.51m,
                0.28m,
                0.24m,
                0.56m,
                1.7m,
                3.1m));

        dbContext.AthleteRoundScouts.AddRange(
            new AthleteRoundScouts(0, athleteRounds[0].Id, "DS", 2m),
            new AthleteRoundScouts(0, athleteRounds[0].Id, "SG", 1m),
            new AthleteRoundScouts(0, athleteRounds[1].Id, "DS", 1m),
            new AthleteRoundScouts(0, athleteRounds[1].Id, "CA", 1m));
        await dbContext.SaveChangesAsync();

        var service = new TrainingDatasetDiagnosticsService(dbContext);

        var athleteOnlyDataset = await service.GetAthleteOnlyDatasetAsync(3, 5, take: 10);
        var fullContextDataset = await service.GetFullContextDatasetAsync(3, 5, take: 10);

        Assert.Equal(2, athleteOnlyDataset.TotalExportableRows);
        Assert.Equal(2, athleteOnlyDataset.ReturnedRows);
        Assert.Equal(2, athleteOnlyDataset.DistinctAthletes);
        Assert.Equal(1, fullContextDataset.TotalExportableRows);
        Assert.Equal(1, fullContextDataset.ReturnedRows);
        Assert.Equal(1, fullContextDataset.ExcludedRows);
        Assert.Single(fullContextDataset.Rows);
        Assert.Equal(athleteA.CartolaId, fullContextDataset.Rows.Single().AthleteCartolaId);
        Assert.Equal(clubB.CartolaId, fullContextDataset.Rows.Single().OpponentClubCartolaId);
        Assert.True(fullContextDataset.Rows.Single().IsHomeMatch);
        Assert.True(fullContextDataset.Rows.Single().HasAthleteContext);
        Assert.Equal(64m, fullContextDataset.Rows.Single().TeamAverageScore);
    }

    [Fact]
    public async Task TrainingDatasetDiagnosticsService_FullContextDataset_ComputesHistoricalWindowsAndScoutAggregates()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var clubA = new Club(0, 293, "CAP", "athletico-pr", "Athletico-PR", "https://example.com/cap.png");
        var clubB = new Club(0, 262, "FLA", "flamengo", "Flamengo", "https://example.com/fla.png");
        var position = new Position(0, 3, "Zagueiro", "zag");
        var status = new AthleteStatus(0, 7, "Provavel");
        var athlete = new Athlete(0, 1001, "Atleta A", "A");

        dbContext.Clubs.AddRange(clubA, clubB);
        dbContext.Positions.Add(position);
        dbContext.AthleteStatuses.Add(status);
        dbContext.Athletes.Add(athlete);
        await dbContext.SaveChangesAsync();

        dbContext.Matches.AddRange(
            new Match(0, 9101, 1, DateTime.UtcNow.AddDays(-28), "Arena", true, clubA.Id, 8, "VEV", 1, clubB.Id, 14, "DED", 0),
            new Match(0, 9102, 2, DateTime.UtcNow.AddDays(-21), "Maracana", true, clubB.Id, 13, "DED", 0, clubA.Id, 7, "VEV", 1),
            new Match(0, 9103, 3, DateTime.UtcNow.AddDays(-14), "Arena", true, clubA.Id, 6, "VVE", 2, clubB.Id, 15, "DDE", 1),
            new Match(0, 9104, 4, DateTime.UtcNow.AddDays(-7), "Maracana", true, clubB.Id, 12, "DED", null, clubA.Id, 5, "VVE", null));

        dbContext.AthleteRounds.AddRange(
            new AthleteRound(0, 1, athlete.Id, clubA.Id, position.Id, status.Id, 1, 1, 0, 2m, 2m, 2m, 2m, 2m, 0m, 8m, true)
            {
                DataPhase = RoundDataPhaseValues.HistoricalClosed,
                Scouts = [],
            },
            new AthleteRound(0, 2, athlete.Id, clubA.Id, position.Id, status.Id, 2, 1, 1, 4m, 3m, 3m, 3m, 3m, 0m, 9m, true)
            {
                DataPhase = RoundDataPhaseValues.HistoricalClosed,
                Scouts = [],
            },
            new AthleteRound(0, 3, athlete.Id, clubA.Id, position.Id, status.Id, 3, 2, 1, 6m, 4m, 4m, 4m, 4m, 0m, 10m, true)
            {
                DataPhase = RoundDataPhaseValues.HistoricalClosed,
                Scouts = [],
            },
            new AthleteRound(0, 4, athlete.Id, clubA.Id, position.Id, status.Id, 4, 2, 2, 8m, 5m, 5m, 5m, 5m, 0m, 11m, true)
            {
                DataPhase = RoundDataPhaseValues.HistoricalClosed,
                Scouts = [],
            });
        await dbContext.SaveChangesAsync();

        var athleteRounds = await dbContext.AthleteRounds
            .OrderBy(item => item.RoundId)
            .ToListAsync();

        dbContext.AthleteRoundScouts.AddRange(
            new AthleteRoundScouts(0, athleteRounds[0].Id, "DS", 1m),
            new AthleteRoundScouts(0, athleteRounds[1].Id, "G", 1m),
            new AthleteRoundScouts(0, athleteRounds[2].Id, "A", 1m),
            new AthleteRoundScouts(0, athleteRounds[2].Id, "FS", 2m),
            new AthleteRoundScouts(0, athleteRounds[3].Id, "DS", 2m),
            new AthleteRoundScouts(0, athleteRounds[3].Id, "SG", 1m));
        await dbContext.SaveChangesAsync();

        var service = new TrainingDatasetDiagnosticsService(dbContext);
        var dataset = await service.GetFullContextDatasetAsync(4, 4, take: 10);

        Assert.Single(dataset.Rows);

        var row = dataset.Rows.Single();
        Assert.Equal(4m, row.ScoreAverageLast3);
        Assert.Equal(4.5m, row.ScoreEwmaLast3);
        Assert.Equal(4m, row.ScoreTrendLast3VsPrev3);
        Assert.Equal(4m, row.VenueScoreAverageLast5);
        Assert.Equal(4m, row.VenueScoreEwmaLast3);
        Assert.Equal(4m, row.ScoutOffensiveSumLast3);
        Assert.Equal(4m, row.ScoutOffensiveSumLast5);
        Assert.Equal(1.3333m, Math.Round(row.ScoutOffensiveAverageLast3, 4));
        Assert.Equal(1.3333m, Math.Round(row.ScoutOffensiveAverageLast5, 4));
        Assert.Equal(1.75m, row.ScoutOffensiveEwmaLast3);
        Assert.Equal(1.75m, row.ScoutOffensiveEwmaLast5);
        Assert.Equal(1m, row.ScoutDefensiveSumLast3);
        Assert.Equal(1m, row.ScoutDefensiveSumLast5);
        Assert.Equal(0.3333m, Math.Round(row.ScoutDefensiveAverageLast3, 4));
        Assert.Equal(0.3333m, Math.Round(row.ScoutDefensiveAverageLast5, 4));
        Assert.Equal(0.25m, row.ScoutDefensiveEwmaLast3);
        Assert.Equal(0.25m, row.ScoutDefensiveEwmaLast5);
        Assert.Equal(0m, row.ScoutNegativeSumLast3);
        Assert.Equal(0m, row.ScoutNegativeSumLast5);
        Assert.Equal(0m, row.ScoutNegativeAverageLast3);
        Assert.Equal(0m, row.ScoutNegativeEwmaLast3);
        Assert.Equal(0m, row.ScoutNegativeEwmaLast5);
        Assert.Equal(5m, row.ScoutBalanceSumLast3);
        Assert.Equal(5m, row.ScoutBalanceSumLast5);
        Assert.Equal(1.6667m, Math.Round(row.ScoutBalanceAverageLast3, 4));
        Assert.Equal(1.6667m, Math.Round(row.ScoutBalanceAverageLast5, 4));
        Assert.Equal(2m, row.ScoutBalanceEwmaLast3);
        Assert.Equal(2m, row.ScoutBalanceEwmaLast5);
        Assert.Equal(0.3333m, Math.Round(row.ScoutGoalsAverageLast3, 4));
        Assert.Equal(0.3333m, Math.Round(row.ScoutAssistsAverageLast3, 4));
        Assert.Equal(0.3333m, Math.Round(row.ScoutTacklesAverageLast3, 4));
        Assert.Equal(0m, row.ScoutCleanSheetRateLast3);
        Assert.Equal(0.6667m, Math.Round(row.ScoreHitRateOver3Last5, 4));
        Assert.Equal(1m, row.PlayRateLast5);
        Assert.Equal(1m, row.RoundsSinceLastInGame);
        Assert.Equal(1m, row.StatusStabilityLast5);
        Assert.Equal(0m, row.StatusChangeFlag);
        Assert.Equal(1m, row.EmpiricalPlayRateForCurrentStatus);
        Assert.Equal(1.5m, row.PriceTrendLast3);
        Assert.Equal(0.8165m, Math.Round(row.PriceVolatilityLast5, 4));
        Assert.Equal(0.4315m, Math.Round(row.HistoricalPointsPerCartoletaLast3, 4));
        Assert.Equal(0.4315m, Math.Round(row.HistoricalPointsPerCartoletaLast5, 4));
        Assert.Equal(2m, row.PositionRelativePriceSignal);
        Assert.Equal(2m, row.PriceDeltaVsHistoricalAverage);
        Assert.Equal(3m, row.ClubPointsPerMatchLast3);
        Assert.Equal(3m, row.ClubVenuePointsPerMatchLast3);
        Assert.Equal(1.3333m, Math.Round(row.ClubGoalsForPerMatchLast3, 4));
        Assert.Equal(0.3333m, Math.Round(row.ClubGoalsAgainstPerMatchLast3, 4));
        Assert.Equal(1m, row.ClubGoalDifferencePerMatchLast3);
        Assert.Equal(0.6667m, Math.Round(row.ClubCleanSheetRateLast3, 4));
        Assert.Equal(4m, row.ClubFantasyPointsForAverageLast3);
        Assert.Equal(4m, row.ClubPositionFantasyPointsForAverageLast3);
        Assert.Equal(0m, row.ClubScoutOffensiveConcededAverageLast3);
        Assert.Equal(0m, row.ClubScoutDefensiveConcededAverageLast3);
        Assert.Equal(0m, row.ClubScoutBalanceConcededAverageLast3);
        Assert.Equal(0m, row.ClubPositionScoutOffensiveConcededAverageLast3);
        Assert.Equal(3m, row.ClubExpectedPointsLast3);
        Assert.Equal(2.1333m, Math.Round(row.ClubAttackStrengthLast3, 4));
        Assert.Equal(1m, Math.Round(row.ClubDefenseStrengthLast3, 4));
        Assert.Equal(0m, row.OpponentPointsPerMatchLast3);
        Assert.Equal(0.3333m, Math.Round(row.OpponentGoalsForPerMatchLast3, 4));
        Assert.Equal(1.3333m, Math.Round(row.OpponentGoalsAgainstPerMatchLast3, 4));
        Assert.Equal(-1m, row.OpponentGoalDifferencePerMatchLast3);
        Assert.Equal(0m, row.OpponentCleanSheetRateLast3);
        Assert.Equal(0m, row.OpponentFantasyPointsForAverageLast3);
        Assert.Equal(4m, row.OpponentFantasyPointsAgainstAverageLast3);
        Assert.Equal(4m, row.OpponentPositionFantasyPointsAgainstAverageLast3);
        Assert.Equal(1.3333m, Math.Round(row.OpponentScoutOffensiveConcededAverageLast3, 4));
        Assert.Equal(0.3333m, Math.Round(row.OpponentScoutDefensiveConcededAverageLast3, 4));
        Assert.Equal(0m, row.OpponentScoutNegativeConcededAverageLast3);
        Assert.Equal(1.6667m, Math.Round(row.OpponentScoutBalanceConcededAverageLast3, 4));
        Assert.Equal(0.3333m, Math.Round(row.OpponentScoutGoalsConcededAverageLast3, 4));
        Assert.Equal(0.3333m, Math.Round(row.OpponentScoutAssistsConcededAverageLast3, 4));
        Assert.Equal(0.3333m, Math.Round(row.OpponentScoutTacklesConcededAverageLast3, 4));
        Assert.Equal(1.3333m, Math.Round(row.OpponentPositionScoutOffensiveConcededAverageLast3, 4));
        Assert.Equal(0.3333m, Math.Round(row.OpponentPositionScoutDefensiveConcededAverageLast3, 4));
        Assert.Equal(0m, row.OpponentPositionScoutNegativeConcededAverageLast3);
        Assert.Equal(1.6667m, Math.Round(row.OpponentPositionScoutBalanceConcededAverageLast3, 4));
        Assert.Equal(0.3333m, Math.Round(row.OpponentPositionScoutGoalsConcededAverageLast3, 4));
        Assert.Equal(0.3333m, Math.Round(row.OpponentPositionScoutAssistsConcededAverageLast3, 4));
        Assert.Equal(0.3333m, Math.Round(row.OpponentPositionScoutTacklesConcededAverageLast3, 4));
        Assert.Equal(0.3333m, Math.Round(row.OpponentAttackStrengthLast3, 4));
        Assert.Equal(-2.1333m, Math.Round(row.OpponentDefenseStrengthLast3, 4));
        Assert.Equal(4.2667m, Math.Round(row.AttackVsOpponentDefenseDeltaLast3, 4));
        Assert.Equal(0m, row.PositionForVsOpponentConcededDeltaLast3);
        Assert.Equal(2m, row.GoalDifferenceDeltaLast3);
    }

    [Fact]
    public async Task TrainingDatasetDiagnosticsService_FullContextDataset_ComputesAvailabilityAndRiskSignals()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var clubA = new Club(0, 293, "CAP", "athletico-pr", "Athletico-PR", "https://example.com/cap.png");
        var clubB = new Club(0, 262, "FLA", "flamengo", "Flamengo", "https://example.com/fla.png");
        var position = new Position(0, 3, "Zagueiro", "zag");
        var probable = new AthleteStatus(0, 7, "Provavel");
        var doubtful = new AthleteStatus(0, 2, "Duvida");
        var athlete = new Athlete(0, 1001, "Atleta A", "A");

        dbContext.Clubs.AddRange(clubA, clubB);
        dbContext.Positions.Add(position);
        dbContext.AthleteStatuses.AddRange(probable, doubtful);
        dbContext.Athletes.Add(athlete);
        await dbContext.SaveChangesAsync();

        dbContext.Matches.AddRange(
            new Match(0, 9201, 1, DateTime.UtcNow.AddDays(-28), "Arena", true, clubA.Id, 8, "VEV", 1, clubB.Id, 14, "DED", 0),
            new Match(0, 9202, 2, DateTime.UtcNow.AddDays(-21), "Maracana", true, clubB.Id, 13, "DED", 0, clubA.Id, 7, "VEV", 1),
            new Match(0, 9203, 3, DateTime.UtcNow.AddDays(-14), "Arena", true, clubA.Id, 6, "VVE", 2, clubB.Id, 15, "DDE", 1),
            new Match(0, 9204, 4, DateTime.UtcNow.AddDays(-7), "Maracana", true, clubB.Id, 12, "DED", null, clubA.Id, 5, "VVE", null));

        dbContext.AthleteRounds.AddRange(
            new AthleteRound(0, 1, athlete.Id, clubA.Id, position.Id, probable.Id, 1, 1, 0, 2m, 2m, 2m, 2m, 2m, 0m, 8m, true)
            {
                DataPhase = RoundDataPhaseValues.HistoricalClosed,
                Scouts = [],
            },
            new AthleteRound(0, 2, athlete.Id, clubA.Id, position.Id, doubtful.Id, 2, 1, 1, 0m, 2m, 2m, 2m, 2m, 0m, 8m, false)
            {
                DataPhase = RoundDataPhaseValues.HistoricalClosed,
                Scouts = [],
            },
            new AthleteRound(0, 3, athlete.Id, clubA.Id, position.Id, doubtful.Id, 3, 2, 1, 0m, 2m, 2m, 2m, 2m, 0m, 8m, false)
            {
                DataPhase = RoundDataPhaseValues.HistoricalClosed,
                Scouts = [],
            },
            new AthleteRound(0, 4, athlete.Id, clubA.Id, position.Id, probable.Id, 4, 2, 2, 6m, 3m, 3m, 3m, 3m, 0m, 9m, true)
            {
                DataPhase = RoundDataPhaseValues.HistoricalClosed,
                Scouts = [],
            });
        await dbContext.SaveChangesAsync();

        var service = new TrainingDatasetDiagnosticsService(dbContext);
        var dataset = await service.GetFullContextDatasetAsync(4, 4, take: 10);

        Assert.Single(dataset.Rows);

        var row = dataset.Rows.Single();
        Assert.Equal(0.3333m, Math.Round(row.PlayRateLast5, 4));
        Assert.Equal(3m, row.RoundsSinceLastInGame);
        Assert.Equal(0.3333m, Math.Round(row.StatusStabilityLast5, 4));
        Assert.Equal(1m, row.StatusChangeFlag);
        Assert.Equal(1m, row.EmpiricalPlayRateForCurrentStatus);
    }

    private static async Task<(Club Club, Position Position, AthleteStatus Status, Athlete[] Athletes)> SeedTrainingBaseAsync(AppDbContext dbContext)
    {
        var club = new Club(0, 293, "CAP", "athletico-pr", "Athletico-PR", "https://example.com/cap.png");
        var position = new Position(0, 3, "Zagueiro", "zag");
        var status = new AthleteStatus(0, 7, "Provavel");
        var athletes = new[]
        {
            new Athlete(0, 1001, "Atleta 1", "Atleta 1"),
            new Athlete(0, 1002, "Atleta 2", "Atleta 2"),
            new Athlete(0, 1003, "Atleta 3", "Atleta 3"),
        };

        dbContext.Clubs.Add(club);
        dbContext.Positions.Add(position);
        dbContext.AthleteStatuses.Add(status);
        dbContext.Athletes.AddRange(athletes);
        await dbContext.SaveChangesAsync();

        return (club, position, status, athletes);
    }
}
