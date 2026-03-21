using System.Globalization;
using System.Text;
using System.Text.Json;
using CartolaOptimization.Application.Interfaces;
using CartolaOptimization.Application.Models;
using CartolaOptimization.Domain.Entities;
using CartolaOptimization.Infrastructure.Context;
using CartolaOptimization.Infrastructure.Diagnostics;
using CartolaOptimization.Infrastructure.Integrations.GatoMestre;
using CartolaOptimization.Infrastructure.Integrations.GatoMestre.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CartolaOptimization.Infrastructure.Services;

/// <summary>
/// Synchronizes team and athlete enrichment data published by Gato Mestre.
/// </summary>
public sealed class GatoMestreSyncService(
    AppDbContext dbContext,
    IGatoMestreApiClient gatoMestreApiClient,
    IIntegrationFailureLogService integrationFailureLogService,
    ISyncExecutionLogService syncExecutionLogService) : IGatoMestreSyncService
{
    private const decimal MinimumHealthyCoverageRate = 95m;
    private const decimal MaximumHealthySkippedRate = 5m;
    private const string QualityCriteriaDescription = "Healthy Gato Mestre synchronization requires athlete coverage >= 95% and skipped athletes <= 5% of processed athletes.";

    private static readonly IReadOnlyDictionary<string, int> PositionMacroToCartolaId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["GOL"] = 1,
        ["LAT"] = 2,
        ["ZAG"] = 3,
        ["MEI"] = 4,
        ["ATA"] = 5,
        ["TEC"] = 6,
    };

    private static readonly IReadOnlyDictionary<string, int> KnownStatuses = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["DUVIDA"] = 2,
        ["SUSPENSO"] = 3,
        ["CONTUNDIDO"] = 5,
        ["NULO"] = 6,
        ["PROVAVEL"] = 7,
    };

    public async Task<GatoMestreSyncResult> SyncRoundAsync(int season, int round, CancellationToken cancellationToken = default)
    {
        var executionContext = new SyncExecutionContext("GatoMestre", "GatoMestreSyncService.SyncRoundAsync");
        executionContext.SetContext("season", season);
        executionContext.SetContext("round", round);
        var startedAtUtc = DateTime.UtcNow;
        var syncExecutionId = await syncExecutionLogService.StartAsync(
            new SyncExecutionLogEntry
            {
                CorrelationId = executionContext.CorrelationId,
                SourceSystem = executionContext.SourceSystem,
                ProcessName = executionContext.ProcessName,
                StartedAtUtc = startedAtUtc,
                Status = SyncExecutionStatus.Running,
                LastStepName = executionContext.CurrentStep,
                Season = season,
                Round = round,
                ContextJson = executionContext.ToContextJson(),
            },
            cancellationToken);

        IDbContextTransaction? transaction = null;

        try
        {
            executionContext.MarkStep("Load teams payload");
            var teamsPayload = await gatoMestreApiClient.GetTeamsAsync(cancellationToken);

            executionContext.MarkStep("Load athletes round payload");
            GatoMestreAthletesRoundResponse athletesRoundPayload;
            try
            {
                athletesRoundPayload = await gatoMestreApiClient.GetAthletesRoundAsync(season, round, cancellationToken);
            }
            catch (ExternalApiException exception) when (IntegrationErrorClassifier.Classify(exception) == IntegrationErrorCode.RoundNotPublished)
            {
                return await CompleteRoundNotPublishedAsync(
                    season,
                    round,
                    executionContext,
                    syncExecutionId,
                    startedAtUtc,
                    exception,
                    cancellationToken);
            }

            var roundTipsPayload = await TryLoadOptionalRoundPayloadAsync(
                () => gatoMestreApiClient.GetRoundTipsAsync(season, round, cancellationToken),
                executionContext,
                $"The tips endpoint could not be loaded for round {round}, so tip-based club enrichment was skipped.",
                "Load round tips payload",
                cancellationToken);

            var favoriteMatchesPayload = await TryLoadOptionalRoundPayloadAsync(
                () => gatoMestreApiClient.GetFavoriteMatchesAsync(season, round, cancellationToken),
                executionContext,
                $"The favorites endpoint could not be loaded for round {round}, so favorite-odds club enrichment was skipped.",
                "Load favorite matches payload",
                cancellationToken);

            var matchForecastsPayload = await TryLoadOptionalRoundPayloadAsync(
                () => gatoMestreApiClient.GetMatchForecastsAsync(season, round, cancellationToken),
                executionContext,
                $"The forecasts endpoint could not be loaded for round {round}, so forecast-risk club enrichment was skipped.",
                "Load match forecasts payload",
                cancellationToken);

            var clubAverageRankingsPayload = await TryLoadOptionalRoundPayloadAsync(
                () => gatoMestreApiClient.GetClubAverageRankingsAsync(season, round, cancellationToken),
                executionContext,
                $"The club rankings endpoint could not be loaded for round {round}, so ranking-based club enrichment was skipped.",
                "Load club average rankings payload",
                cancellationToken);

            executionContext.MarkStep("Begin database transaction");
            transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            executionContext.MarkStep("Ensure known positions");
            await EnsureKnownPositionsAsync(cancellationToken);

            executionContext.MarkStep("Ensure known statuses");
            await EnsureKnownStatusesAsync(cancellationToken);

            executionContext.MarkStep("Upsert clubs");
            var clubs = await UpsertClubsAsync(teamsPayload, cancellationToken);

            executionContext.MarkStep("Persist catalog changes");
            await dbContext.SaveChangesAsync(cancellationToken);

            executionContext.MarkStep("Upsert club rounds");
            var clubRoundResult = await UpsertClubRoundsAsync(
                round,
                clubs,
                executionContext,
                favoriteMatchesPayload.Payload,
                matchForecastsPayload.Payload,
                clubAverageRankingsPayload.Payload,
                roundTipsPayload.Payload,
                cancellationToken);

            executionContext.MarkStep("Upsert athlete rounds");
            var athleteRoundResult = await UpsertAthleteRoundsAsync(round, athletesRoundPayload.Result, clubs, clubRoundResult.AvailableClubIds, executionContext, cancellationToken);

            executionContext.MarkStep("Persist enriched round changes");
            await dbContext.SaveChangesAsync(cancellationToken);

            executionContext.MarkStep("Commit database transaction");
            await transaction.CommitAsync(cancellationToken);

            var finishedAtUtc = DateTime.UtcNow;
            var durationMs = (long)(finishedAtUtc - startedAtUtc).TotalMilliseconds;
            var athleteSkippedRate = CalculateSkippedRate(
                athleteRoundResult.AthleteRoundsUpserted,
                athleteRoundResult.AthleteRoundsSkipped);
            var qualityCriteriaSatisfied =
                athleteRoundResult.AthleteCoverageRate >= MinimumHealthyCoverageRate &&
                athleteSkippedRate <= MaximumHealthySkippedRate;
            var optionalClubWarningLogsCount =
                roundTipsPayload.WarningLogsCount +
                favoriteMatchesPayload.WarningLogsCount +
                matchForecastsPayload.WarningLogsCount +
                clubAverageRankingsPayload.WarningLogsCount;
            var optionalClubNotes = string.Join(
                " ",
                new[]
                {
                    roundTipsPayload.Note,
                    favoriteMatchesPayload.Note,
                    matchForecastsPayload.Note,
                    clubAverageRankingsPayload.Note,
                }.Where(note => !string.IsNullOrWhiteSpace(note)));
            var isPartialSync =
                athleteRoundResult.AthleteRoundsSkipped > 0 ||
                !clubRoundResult.DataAvailable ||
                clubRoundResult.WarningLogsCount > 0 ||
                optionalClubWarningLogsCount > 0 ||
                !string.IsNullOrWhiteSpace(optionalClubNotes) ||
                athleteRoundResult.AthleteFailuresLogged > 0;
            var executionStatus = isPartialSync ? SyncExecutionStatus.Partial : SyncExecutionStatus.Success;
            var notes = BuildNotes(
                string.Join(" ", new[] { clubRoundResult.Note, optionalClubNotes }.Where(note => !string.IsNullOrWhiteSpace(note))),
                clubRoundResult.WarningLogsCount + optionalClubWarningLogsCount,
                athleteRoundResult);
            var metricsJson = JsonSerializer.Serialize(new
            {
                ClubsUpserted = clubs.Count,
                ClubRoundsUpserted = clubRoundResult.ClubRoundsUpserted,
                ClubRoundPositionsUpserted = clubRoundResult.PositionStatsUpserted,
                AthleteRoundsUpserted = athleteRoundResult.AthleteRoundsUpserted,
                AthleteRoundsSkipped = athleteRoundResult.AthleteRoundsSkipped,
                AthleteDetailsFetched = athleteRoundResult.AthleteDetailsFetched,
                AthleteFailuresLogged = athleteRoundResult.AthleteFailuresLogged,
                AthleteCoverageRate = athleteRoundResult.AthleteCoverageRate,
                AthleteSkippedRate = athleteSkippedRate,
                ClubRoundDataAvailable = clubRoundResult.DataAvailable,
                TipsDataAvailable = roundTipsPayload.Payload is not null,
                FavoriteMatchesDataAvailable = favoriteMatchesPayload.Payload is not null,
                MatchForecastsDataAvailable = matchForecastsPayload.Payload is not null,
                ClubAverageRankingsDataAvailable = clubAverageRankingsPayload.Payload is not null,
            });

            if (syncExecutionId.HasValue)
            {
                await syncExecutionLogService.UpdateAsync(
                    syncExecutionId.Value,
                    new SyncExecutionLogEntry
                    {
                        CorrelationId = executionContext.CorrelationId,
                        SourceSystem = executionContext.SourceSystem,
                        ProcessName = executionContext.ProcessName,
                        StartedAtUtc = startedAtUtc,
                        FinishedAtUtc = finishedAtUtc,
                        DurationMs = durationMs,
                        Status = executionStatus,
                        LastStepName = executionContext.CurrentStep,
                        Season = season,
                        Round = round,
                        Notes = notes,
                        MetricsJson = metricsJson,
                        ContextJson = executionContext.ToContextJson(),
                        QualityCriteriaJson = JsonSerializer.Serialize(new
                        {
                            MinimumCoverageRate = MinimumHealthyCoverageRate,
                            MaximumSkippedRate = MaximumHealthySkippedRate,
                            QualityCriteriaDescription,
                            QualityCriteriaSatisfied = qualityCriteriaSatisfied,
                        }),
                        CoverageRate = athleteRoundResult.AthleteCoverageRate,
                        SkippedRate = athleteSkippedRate,
                        QualityCriteriaSatisfied = qualityCriteriaSatisfied,
                        WarningLogsCount = clubRoundResult.WarningLogsCount + optionalClubWarningLogsCount + athleteRoundResult.AthleteFailuresLogged,
                    },
                    cancellationToken);
            }

            return new GatoMestreSyncResult
            {
                SyncExecutionId = syncExecutionId,
                CorrelationId = executionContext.CorrelationId,
                Status = executionStatus,
                StartedAtUtc = startedAtUtc,
                FinishedAtUtc = finishedAtUtc,
                DurationMs = durationMs,
                Season = season,
                Round = round,
                ClubsUpserted = clubs.Count,
                ClubRoundsUpserted = clubRoundResult.ClubRoundsUpserted,
                ClubRoundPositionsUpserted = clubRoundResult.PositionStatsUpserted,
                AthleteRoundsUpserted = athleteRoundResult.AthleteRoundsUpserted,
                AthleteRoundsSkipped = athleteRoundResult.AthleteRoundsSkipped,
                AthleteDetailsFetched = athleteRoundResult.AthleteDetailsFetched,
                AthleteFailuresLogged = athleteRoundResult.AthleteFailuresLogged,
                AthleteCoverageRate = athleteRoundResult.AthleteCoverageRate,
                AthleteSkippedRate = athleteSkippedRate,
                MinimumCoverageRate = MinimumHealthyCoverageRate,
                MaximumSkippedRate = MaximumHealthySkippedRate,
                QualityCriteriaSatisfied = qualityCriteriaSatisfied,
                QualityCriteriaDescription = QualityCriteriaDescription,
                ClubRoundDataAvailable = clubRoundResult.DataAvailable,
                IsPartialSync = isPartialSync,
                Notes = notes,
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var transactionRolledBack = await RollbackTransactionAsync(transaction, cancellationToken);
            var errorLogId = await integrationFailureLogService.TryLogAsync(
                BuildFailureLogEntry(executionContext, exception, transactionRolledBack, IntegrationLogSeverity.Error),
                cancellationToken);

            if (syncExecutionId.HasValue)
            {
                var finishedAtUtc = DateTime.UtcNow;
                var durationMs = (long)(finishedAtUtc - startedAtUtc).TotalMilliseconds;

                await syncExecutionLogService.UpdateAsync(
                    syncExecutionId.Value,
                    new SyncExecutionLogEntry
                    {
                        CorrelationId = executionContext.CorrelationId,
                        SourceSystem = executionContext.SourceSystem,
                        ProcessName = executionContext.ProcessName,
                        StartedAtUtc = startedAtUtc,
                        FinishedAtUtc = finishedAtUtc,
                        DurationMs = durationMs,
                        Status = SyncExecutionStatus.Failed,
                        LastStepName = executionContext.CurrentStep,
                        Season = season,
                        Round = round,
                        ErrorCode = IntegrationErrorClassifier.Classify(exception),
                        Notes = exception.Message,
                        ContextJson = executionContext.ToContextJson(),
                        ErrorLogsCount = errorLogId.HasValue ? 1 : 0,
                    },
                    cancellationToken);
            }

            throw new DataCollectionProcessException(
                sourceSystem: executionContext.SourceSystem,
                processName: executionContext.ProcessName,
                stepName: executionContext.CurrentStep,
                correlationId: executionContext.CorrelationId,
                errorLogId: errorLogId,
                message: $"Gato Mestre synchronization failed at step '{executionContext.CurrentStep}'. Review IntegrationFailureLogs using correlation '{executionContext.CorrelationId}'.",
                innerException: exception);
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    private async Task EnsureKnownPositionsAsync(CancellationToken cancellationToken)
    {
        var existingPositions = await dbContext.Positions.ToDictionaryAsync(position => position.CartolaId, cancellationToken);
        var defaults = new[]
        {
            new Position(0, 1, "Goleiro", "gol"),
            new Position(0, 2, "Lateral", "lat"),
            new Position(0, 3, "Zagueiro", "zag"),
            new Position(0, 4, "Meia", "mei"),
            new Position(0, 5, "Atacante", "ata"),
            new Position(0, 6, "Tecnico", "tec"),
        };

        foreach (var position in defaults)
        {
            if (!existingPositions.ContainsKey(position.CartolaId))
            {
                dbContext.Positions.Add(position);
                existingPositions.Add(position.CartolaId, position);
            }
        }
    }

    private async Task EnsureKnownStatusesAsync(CancellationToken cancellationToken)
    {
        var existingStatuses = await dbContext.AthleteStatuses.ToDictionaryAsync(status => status.CartolaId, cancellationToken);
        var defaults = new[]
        {
            new AthleteStatus(0, 2, "Duvida"),
            new AthleteStatus(0, 3, "Suspenso"),
            new AthleteStatus(0, 5, "Contundido"),
            new AthleteStatus(0, 6, "Nulo"),
            new AthleteStatus(0, 7, "Provavel"),
        };

        foreach (var status in defaults)
        {
            if (!existingStatuses.ContainsKey(status.CartolaId))
            {
                dbContext.AthleteStatuses.Add(status);
                existingStatuses.Add(status.CartolaId, status);
            }
        }
    }

    private async Task<Dictionary<int, Club>> UpsertClubsAsync(
        Dictionary<int, GatoMestreTeamResponse> teamsPayload,
        CancellationToken cancellationToken)
    {
        var existingClubs = await dbContext.Clubs.ToDictionaryAsync(club => club.CartolaId, cancellationToken);

        foreach (var team in teamsPayload.Values.OrderBy(team => team.TeamId))
        {
            if (!existingClubs.TryGetValue(team.TeamId, out var club))
            {
                club = new Club(
                    0,
                    team.TeamId,
                    ToRequiredValue(team.Acronym, team.Name, 10),
                    ToOptionalValue(team.Slug, 100),
                    ToRequiredValue(string.IsNullOrWhiteSpace(team.PopularName) ? team.Nickname : team.PopularName, team.Name, 100),
                    ToOptionalValue(team.Shields?.Size60, 255));

                dbContext.Clubs.Add(club);
                existingClubs.Add(team.TeamId, club);
            }

            club.CartolaId = team.TeamId;
            club.Abreviation = ToRequiredValue(team.Acronym, team.Name, 10);
            club.Slug = ToOptionalValue(team.Slug, 100);
            club.Nickname = ToRequiredValue(string.IsNullOrWhiteSpace(team.PopularName) ? team.Nickname : team.PopularName, team.Name, 100);
            club.LogoUrl = ToOptionalValue(team.Shields?.Size60, 255);
        }

        return existingClubs;
    }

    private async Task<(int ClubRoundsUpserted, int PositionStatsUpserted, bool DataAvailable, string Note, int WarningLogsCount, IReadOnlyCollection<int> AvailableClubIds)> UpsertClubRoundsAsync(
        int requestedRound,
        IReadOnlyDictionary<int, Club> clubs,
        SyncExecutionContext executionContext,
        GatoMestreRoundCollectionResponse<GatoMestreFavoriteMatchResponse>? favoriteMatchesPayload,
        GatoMestreRoundCollectionResponse<GatoMestreForecastMatchResponse>? matchForecastsPayload,
        GatoMestreRoundCollectionResponse<GatoMestreClubAverageRankingResponse>? clubAverageRankingsPayload,
        GatoMestreRoundCollectionResponse<GatoMestreTipItemResponse>? roundTipsPayload,
        CancellationToken cancellationToken)
    {
        var positions = await dbContext.Positions.ToDictionaryAsync(position => position.CartolaId, cancellationToken);
        var clubIdByAbbreviation = clubs.Values
            .Where(club => !string.IsNullOrWhiteSpace(club.Abreviation))
            .ToDictionary(club => Normalize(club.Abreviation), club => club.CartolaId);
        var favoriteByClubCartolaId = BuildFavoriteContextByClub(favoriteMatchesPayload, clubIdByAbbreviation);
        var forecastByClubCartolaId = BuildForecastContextByClub(matchForecastsPayload, clubIdByAbbreviation);
        var rankingsByClubCartolaId = BuildAverageRankingByClub(clubAverageRankingsPayload);
        var tipsSummaryByClubCartolaId = BuildTipSummaryByClub(roundTipsPayload, clubIdByAbbreviation);
        var hasFavoritePayload = favoriteMatchesPayload is not null;
        var hasForecastPayload = matchForecastsPayload is not null;
        var hasRankingsPayload = clubAverageRankingsPayload is not null;
        var hasTipsPayload = roundTipsPayload is not null;
        var processedClubRounds = 0;
        var processedPositionStats = 0;
        var dataAvailable = true;
        var note = string.Empty;
        var warningLogsCount = 0;
        var mismatchWarningLogged = false;
        var availableClubIds = new HashSet<int>();

        foreach (var club in clubs.Values.OrderBy(club => club.CartolaId))
        {
            executionContext.SetContext("clubCartolaId", club.CartolaId);
            executionContext.MarkStep($"Load confrontation payload for club {club.CartolaId}");

            GatoMestreClubConfrontationResponse confrontation;
            try
            {
                confrontation = await gatoMestreApiClient.GetClubConfrontationAsync(club.CartolaId, cancellationToken);
            }
            catch (ExternalApiException exception)
            {
                dataAvailable = false;

                if (string.IsNullOrWhiteSpace(note))
                {
                    note = $"One or more confrontation payloads could not be read for requested round {requestedRound}, so club round data was only partially synchronized.";
                }

                warningLogsCount += await TryLogNonFatalWarningAsync(executionContext, exception, cancellationToken);
                await RemoveExistingClubRoundAsync(requestedRound, club.Id, cancellationToken);
                continue;
            }

            if (confrontation.Round != requestedRound)
            {
                dataAvailable = false;
                note = $"The confrontation endpoint currently exposes round {confrontation.Round}, so club round data was not synchronized for requested round {requestedRound}.";
                await RemoveExistingClubRoundAsync(requestedRound, club.Id, cancellationToken);

                if (!mismatchWarningLogged)
                {
                    var mismatchException = new DataCollectionIntegrityException(
                        sourceSystem: "GatoMestre",
                        stepName: "Upsert club rounds",
                        errorLocation: "GatoMestreSyncService.UpsertClubRoundsAsync",
                        message: $"The confrontation payload returned round '{confrontation.Round}' while the synchronization requested round '{requestedRound}'.",
                        contextData: new Dictionary<string, object?>
                        {
                            ["clubCartolaId"] = club.CartolaId,
                            ["requestedRound"] = requestedRound,
                            ["payloadRound"] = confrontation.Round,
                        });

                    warningLogsCount += await TryLogNonFatalWarningAsync(executionContext, mismatchException, cancellationToken);
                    mismatchWarningLogged = true;
                }

                continue;
            }

            var clubBlock = ResolveClubBlock(confrontation, club.CartolaId);
            if (clubBlock is null)
            {
                throw new DataCollectionIntegrityException(
                    sourceSystem: "GatoMestre",
                    stepName: "Upsert club rounds",
                    errorLocation: "GatoMestreSyncService.UpsertClubRoundsAsync",
                    message: $"The confrontation payload for club '{club.CartolaId}' does not contain a block for the requested club.",
                    contextData: new Dictionary<string, object?>
                    {
                        ["clubCartolaId"] = club.CartolaId,
                        ["requestedRound"] = requestedRound,
                        ["payloadRound"] = confrontation.Round,
                    });
            }

            var existingClubRound = await dbContext.ClubRounds
                .Include(round => round.PositionStats)
                .Include(round => round.Context)
                .FirstOrDefaultAsync(round => round.Round == requestedRound && round.ClubId == club.Id, cancellationToken);

            if (existingClubRound is null)
            {
                existingClubRound = new ClubRound(
                    0,
                    requestedRound,
                    club.Id,
                    ResolveWinProbability(clubBlock),
                    clubBlock.NextConfrontation.DrawProbability,
                    ResolveLoseProbability(clubBlock),
                    clubBlock.OverallAverage,
                    clubBlock.MovingAverage,
                    ResolveExpectedPoints(clubBlock))
                {
                    PositionStats = [],
                };

                dbContext.ClubRounds.Add(existingClubRound);
            }

            existingClubRound.Round = requestedRound;
            existingClubRound.ClubId = club.Id;
            existingClubRound.WinProbability = ResolveWinProbability(clubBlock);
            existingClubRound.DrawProbability = clubBlock.NextConfrontation.DrawProbability;
            existingClubRound.LoseProbability = ResolveLoseProbability(clubBlock);
            existingClubRound.OverallAverage = clubBlock.OverallAverage;
            existingClubRound.MovingAverage = clubBlock.MovingAverage;
            existingClubRound.ExpectedPoints = ResolveExpectedPoints(clubBlock);

            existingClubRound.PositionStats ??= [];
            dbContext.ClubRoundPositions.RemoveRange(existingClubRound.PositionStats);
            existingClubRound.PositionStats.Clear();
            var positionDistribution = clubBlock.PositionDistribution
                .GroupBy(item => item.PositionId)
                .ToDictionary(group => group.Key, group => group.Last().Value);

            foreach (var positionStat in ResolvePositionStats(clubBlock).OrderBy(item => item.PositionId))
            {
                if (!positions.TryGetValue(positionStat.PositionId, out var position))
                {
                    throw new DataCollectionIntegrityException(
                        sourceSystem: "GatoMestre",
                        stepName: "Upsert club round positions",
                        errorLocation: "GatoMestreSyncService.UpsertClubRoundsAsync",
                        message: $"Club round position data references position '{positionStat.PositionId}', but the local catalog does not contain it.",
                        contextData: new Dictionary<string, object?>
                        {
                            ["clubCartolaId"] = club.CartolaId,
                            ["requestedRound"] = requestedRound,
                            ["positionCartolaId"] = positionStat.PositionId,
                        });
                }

                existingClubRound.PositionStats.Add(new ClubRoundPosition(
                    0,
                    existingClubRound.Id,
                    position.Id,
                    positionStat.PointsConcededAverage,
                    positionStat.PointsConqueredAverage,
                    positionDistribution.GetValueOrDefault(positionStat.PositionId))
                {
                    ClubRound = existingClubRound,
                    Position = position,
                });

                processedPositionStats++;
            }

            existingClubRound.Context ??= new ClubRoundContext
            {
                ClubRound = existingClubRound,
                ForecastRiskLevel = string.Empty,
            };

            var ranking = rankingsByClubCartolaId.GetValueOrDefault(club.CartolaId);
            var favoriteContext = favoriteByClubCartolaId.GetValueOrDefault(club.CartolaId);
            var forecastContext = forecastByClubCartolaId.GetValueOrDefault(club.CartolaId);
            var tipSummary = tipsSummaryByClubCartolaId.GetValueOrDefault(club.CartolaId) ?? ClubTipSummary.Empty;

            existingClubRound.Context.PreviousOverallAverage = clubBlock.PreviousOverallAverage;
            existingClubRound.Context.RankingsOverallAverage = ranking is not null ? GetSnapshotValue(ranking.Averages.Overall.Total) : hasRankingsPayload ? 0m : existingClubRound.Context.RankingsOverallAverage;
            existingClubRound.Context.RankingsMovingAverage = ranking is not null ? GetSnapshotValue(ranking.Averages.Overall.Moving) : hasRankingsPayload ? 0m : existingClubRound.Context.RankingsMovingAverage;
            existingClubRound.Context.RankingsHomeAverage = ranking is not null ? GetSnapshotValue(ranking.Averages.Home.Total) : hasRankingsPayload ? 0m : existingClubRound.Context.RankingsHomeAverage;
            existingClubRound.Context.RankingsHomeMovingAverage = ranking is not null ? GetSnapshotValue(ranking.Averages.Home.Moving) : hasRankingsPayload ? 0m : existingClubRound.Context.RankingsHomeMovingAverage;
            existingClubRound.Context.RankingsAwayAverage = ranking is not null ? GetSnapshotValue(ranking.Averages.Away.Total) : hasRankingsPayload ? 0m : existingClubRound.Context.RankingsAwayAverage;
            existingClubRound.Context.RankingsAwayMovingAverage = ranking is not null ? GetSnapshotValue(ranking.Averages.Away.Moving) : hasRankingsPayload ? 0m : existingClubRound.Context.RankingsAwayMovingAverage;
            existingClubRound.Context.GeneralPointsConcededAverage = clubBlock.PointsBalance.OverallPointsConcededAverage;
            existingClubRound.Context.GeneralPointsConqueredAverage = clubBlock.PointsBalance.OverallPointsConqueredAverage;
            existingClubRound.Context.HomePointsConcededAverage = clubBlock.PointsBalance.HomePointsConcededAverage;
            existingClubRound.Context.HomePointsConqueredAverage = clubBlock.PointsBalance.HomePointsConqueredAverage;
            existingClubRound.Context.AwayPointsConcededAverage = clubBlock.PointsBalance.AwayPointsConcededAverage;
            existingClubRound.Context.AwayPointsConqueredAverage = clubBlock.PointsBalance.AwayPointsConqueredAverage;
            existingClubRound.Context.HistoricalWinRate = clubBlock.Performance.Wins.Rate.Overall;
            existingClubRound.Context.HistoricalDrawRate = clubBlock.Performance.Draws.Rate.Overall;
            existingClubRound.Context.HistoricalLossRate = clubBlock.Performance.Losses.Rate.Overall;
            existingClubRound.Context.FavoriteWinProbability = favoriteContext is not null ? favoriteContext.WinProbability : hasFavoritePayload ? 0m : existingClubRound.Context.FavoriteWinProbability;
            existingClubRound.Context.FavoriteDrawProbability = favoriteContext is not null ? favoriteContext.DrawProbability : hasFavoritePayload ? 0m : existingClubRound.Context.FavoriteDrawProbability;
            existingClubRound.Context.FavoriteLoseProbability = favoriteContext is not null ? favoriteContext.LoseProbability : hasFavoritePayload ? 0m : existingClubRound.Context.FavoriteLoseProbability;
            existingClubRound.Context.ForecastExpectedResult = forecastContext is not null ? forecastContext.ExpectedResult : hasForecastPayload ? 0 : existingClubRound.Context.ForecastExpectedResult;
            existingClubRound.Context.ForecastRiskLevel = forecastContext is not null ? forecastContext.RiskLevel : hasForecastPayload ? string.Empty : existingClubRound.Context.ForecastRiskLevel;
            existingClubRound.Context.TeamTipsCount = hasTipsPayload ? tipSummary.TeamTipsCount : existingClubRound.Context.TeamTipsCount;
            existingClubRound.Context.HotTeamTipsCount = hasTipsPayload ? tipSummary.HotTeamTipsCount : existingClubRound.Context.HotTeamTipsCount;
            existingClubRound.Context.MatchTipsCount = hasTipsPayload ? tipSummary.MatchTipsCount : existingClubRound.Context.MatchTipsCount;
            existingClubRound.Context.HotMatchTipsCount = hasTipsPayload ? tipSummary.HotMatchTipsCount : existingClubRound.Context.HotMatchTipsCount;
            existingClubRound.Context.InsightCount = hasTipsPayload ? tipSummary.InsightCount : existingClubRound.Context.InsightCount;
            existingClubRound.Context.HotInsightCount = hasTipsPayload ? tipSummary.HotInsightCount : existingClubRound.Context.HotInsightCount;
            existingClubRound.Context.AverageInsightConfidence = hasTipsPayload ? tipSummary.AverageInsightConfidence : existingClubRound.Context.AverageInsightConfidence;
            existingClubRound.Context.PositiveInsightCount = hasTipsPayload ? tipSummary.PositiveInsightCount : existingClubRound.Context.PositiveInsightCount;
            existingClubRound.Context.NegativeInsightCount = hasTipsPayload ? tipSummary.NegativeInsightCount : existingClubRound.Context.NegativeInsightCount;

            TrainingReadinessEvaluator.ApplyClubRoundReadiness(existingClubRound);

            if (existingClubRound.IsTrainingEligible)
            {
                availableClubIds.Add(existingClubRound.ClubId);
            }

            processedClubRounds++;
        }

        return (processedClubRounds, processedPositionStats, dataAvailable, note, warningLogsCount, availableClubIds.ToArray());
    }

    private async Task<(int AthleteRoundsUpserted, int AthleteRoundsSkipped, int AthleteDetailsFetched, int AthleteFailuresLogged, decimal AthleteCoverageRate)> UpsertAthleteRoundsAsync(
        int round,
        IReadOnlyCollection<GatoMestreAthleteRoundSummaryResponse> summaries,
        IReadOnlyDictionary<int, Club> clubs,
        IReadOnlyCollection<int> availableClubIds,
        SyncExecutionContext executionContext,
        CancellationToken cancellationToken)
    {
        var athletesByCartolaId = await dbContext.Athletes.ToDictionaryAsync(athlete => athlete.CartolaId, cancellationToken);
        var positionsByCartolaId = await dbContext.Positions.ToDictionaryAsync(position => position.CartolaId, cancellationToken);
        var statusesByName = await dbContext.AthleteStatuses.ToDictionaryAsync(status => Normalize(status.Name), cancellationToken);

        var athleteIds = athletesByCartolaId.Values.Select(athlete => athlete.Id).ToArray();
        var existingRounds = await dbContext.AthleteRounds
            .Include(roundEntity => roundEntity.Scouts)
            .Include(roundEntity => roundEntity.Context)
            .Where(roundEntity => roundEntity.RoundId == round && athleteIds.Contains(roundEntity.AthleteId))
            .ToDictionaryAsync(roundEntity => roundEntity.AthleteId, cancellationToken);

        var latestKnownRounds = await dbContext.AthleteRounds
            .Where(roundEntity => athleteIds.Contains(roundEntity.AthleteId))
            .OrderByDescending(roundEntity => roundEntity.RoundId)
            .ToListAsync(cancellationToken);

        var latestKnownRoundByAthleteId = latestKnownRounds
            .GroupBy(roundEntity => roundEntity.AthleteId)
            .ToDictionary(
                group => group.Key,
                group => group.FirstOrDefault(roundEntity => roundEntity.ClubId != 0 && roundEntity.PositionId != 0 && roundEntity.StatusId != 0) ?? group.First());

        var upserted = 0;
        var skipped = 0;
        var athleteDetailsFetched = 0;
        var athleteFailuresLogged = 0;
        var availableClubIdSet = availableClubIds.ToHashSet();

        foreach (var summary in summaries.OrderBy(item => item.AthleteId))
        {
            executionContext.SetContext("athleteCartolaId", summary.AthleteId);

            athletesByCartolaId.TryGetValue(summary.AthleteId, out var athlete);
            existingRounds.TryGetValue(athlete?.Id ?? 0, out var athleteRound);
            latestKnownRoundByAthleteId.TryGetValue(athlete?.Id ?? 0, out var latestKnownRound);

            GatoMestreAthleteDetailResponse? athleteDetail = null;
            GatoMestreAthleteRoundDetailResponse? detailRound = null;
            ExternalApiException? athleteDetailFailure = null;

            if (athlete is null || athleteRound is null || athleteRound.StatusId == 0 || athleteRound.PositionId == 0 || athleteRound.ClubId == 0)
            {
                try
                {
                    executionContext.MarkStep($"Load athlete detail payload for athlete {summary.AthleteId}");
                    athleteDetail = await gatoMestreApiClient.GetAthleteDetailAsync(summary.AthleteId, cancellationToken);
                    detailRound = athleteDetail.Rounds.FirstOrDefault(item => item.Round == round);
                    athleteDetailsFetched++;

                    if (athleteDetail.Athlete.AthleteId != summary.AthleteId)
                    {
                        throw new DataCollectionIntegrityException(
                            sourceSystem: "GatoMestre",
                            stepName: "Load athlete detail payload",
                            errorLocation: "GatoMestreSyncService.UpsertAthleteRoundsAsync",
                            message: $"The athlete detail payload returned inconsistent identifiers for athlete '{summary.AthleteId}'.",
                            contextData: new Dictionary<string, object?>
                            {
                                ["athleteCartolaId"] = summary.AthleteId,
                                ["payloadAthleteId"] = athleteDetail.Athlete.AthleteId,
                            });
                    }

                    executionContext.MarkStep($"Upsert athlete catalog fallback for athlete {summary.AthleteId}");
                    athlete = await EnsureAthleteAsync(athletesByCartolaId, athleteDetail);
                    existingRounds.TryGetValue(athlete.Id, out athleteRound);
                    latestKnownRoundByAthleteId.TryGetValue(athlete.Id, out latestKnownRound);
                }
                catch (ExternalApiException exception)
                {
                    athleteDetailFailure = exception;
                    athleteFailuresLogged += await TryLogNonFatalWarningAsync(executionContext, exception, cancellationToken);
                }
                catch (DataCollectionIntegrityException exception)
                {
                    athleteFailuresLogged += await TryLogNonFatalWarningAsync(executionContext, exception, cancellationToken);
                }
            }

            var club = ResolveClub(clubs, athleteDetail?.Team.TeamId ?? 0, athleteRound?.ClubId, latestKnownRound?.ClubId);
            var position = ResolvePosition(positionsByCartolaId, athleteDetail?.Athlete.Position, athleteRound?.PositionId, latestKnownRound?.PositionId);
            var status = ResolveStatus(statusesByName, detailRound?.StatusName, athleteRound?.StatusId, latestKnownRound?.StatusId);

            if (athlete is null || club is null || position is null || status is null)
            {
                var mappingException = new DataCollectionIntegrityException(
                    sourceSystem: "GatoMestre",
                    stepName: "Upsert athlete rounds",
                    errorLocation: "GatoMestreSyncService.UpsertAthleteRoundsAsync",
                    message: $"Athlete '{summary.AthleteId}' could not be mapped to all required local references.",
                    contextData: new Dictionary<string, object?>
                    {
                        ["athleteCartolaId"] = summary.AthleteId,
                        ["round"] = round,
                        ["athleteResolved"] = athlete is not null,
                        ["clubResolved"] = club is not null,
                        ["positionResolved"] = position is not null,
                        ["statusResolved"] = status is not null,
                        ["detailRoundFound"] = detailRound is not null,
                        ["detailRequestFailed"] = athleteDetailFailure is not null,
                    });

                athleteFailuresLogged += await TryLogNonFatalWarningAsync(executionContext, mappingException, cancellationToken);
                skipped++;
                continue;
            }

            if (athleteRound is null)
            {
                athleteRound = new AthleteRound(
                    0,
                    round,
                    athlete.Id,
                    club.Id,
                    position.Id,
                    status.Id,
                    ReadGames(summary.Games, "geral") ?? detailRound?.GamesCount ?? 0,
                    ReadGames(summary.Games, "mandante") ?? 0,
                    ReadGames(summary.Games, "visitante") ?? 0,
                    ReadSnapshotDecimal(summary.Points, "num") ?? detailRound?.Points?.Num ?? 0,
                    ReadAverage(summary.Averages, "geral", "movel") ?? detailRound?.Averages.Overall.Moving?.Num ?? 0,
                    ReadAverage(summary.Averages, "geral", "total") ?? detailRound?.Averages.Overall.Total?.Num ?? 0,
                    ReadAverage(summary.Averages, "mandante", "total") ?? detailRound?.Averages.Home.Total?.Num ?? 0,
                    ReadAverage(summary.Averages, "visitante", "total") ?? detailRound?.Averages.Away.Total?.Num ?? 0,
                    ReadSummaryPriceVariation(summary.Price) ?? detailRound?.Price?.Variation ?? 0,
                    ReadSummaryPriceValue(summary.Price) ?? detailRound?.Price?.Num ?? 0,
                    detailRound?.WasInGame ?? false)
                {
                    Scouts = [],
                };

                dbContext.AthleteRounds.Add(athleteRound);
                existingRounds[athlete.Id] = athleteRound;
            }

            athleteRound.ClubId = club.Id;
            athleteRound.PositionId = position.Id;
            athleteRound.StatusId = status.Id;
            athleteRound.RoundId = round;
            athleteRound.AthleteId = athlete.Id;
            athleteRound.GamesNum = ReadGames(summary.Games, "geral") ?? detailRound?.GamesCount ?? athleteRound.GamesNum;
            athleteRound.HomeGamesNum = ReadGames(summary.Games, "mandante") ?? athleteRound.HomeGamesNum;
            athleteRound.AwayGamesNum = ReadGames(summary.Games, "visitante") ?? athleteRound.AwayGamesNum;
            athleteRound.MovingAverage = ReadAverage(summary.Averages, "geral", "movel") ?? detailRound?.Averages.Overall.Moving?.Num ?? athleteRound.MovingAverage;
            athleteRound.AverageScoreNum = ReadAverage(summary.Averages, "geral", "total") ?? detailRound?.Averages.Overall.Total?.Num ?? athleteRound.AverageScoreNum;
            athleteRound.HomeAverageScore = ReadAverage(summary.Averages, "mandante", "total") ?? detailRound?.Averages.Home.Total?.Num ?? athleteRound.HomeAverageScore;
            athleteRound.AwayAverageScore = ReadAverage(summary.Averages, "visitante", "total") ?? detailRound?.Averages.Away.Total?.Num ?? athleteRound.AwayAverageScore;
            athleteRound.ScoreNum = ReadSnapshotDecimal(summary.Points, "num") ?? detailRound?.Points?.Num ?? athleteRound.ScoreNum;
            athleteRound.VariationNum = ReadSummaryPriceVariation(summary.Price) ?? detailRound?.Price?.Variation ?? athleteRound.VariationNum;
            athleteRound.PriceNum = ReadSummaryPriceValue(summary.Price) ?? detailRound?.Price?.Num ?? athleteRound.PriceNum;
            athleteRound.WasInGame = detailRound?.WasInGame ?? athleteRound.WasInGame;

            executionContext.MarkStep($"Load athlete round content payload for athlete {summary.AthleteId}");
            try
            {
                var athleteRoundContent = await gatoMestreApiClient.GetAthleteRoundContentAsync(summary.AthleteId, round, cancellationToken);
                UpsertAthleteRoundContext(athleteRound, athleteRoundContent);
            }
            catch (ExternalApiException exception)
            {
                athleteFailuresLogged += await TryLogNonFatalWarningAsync(executionContext, exception, cancellationToken);
            }

            TrainingReadinessEvaluator.ApplyAthleteEnrichment(
                athleteRound,
                availableClubIdSet.Contains(athleteRound.ClubId));

            if (detailRound is not null && detailRound.Scouts.Count > 0)
            {
                athleteRound.Scouts ??= [];
                dbContext.AthleteRoundScouts.RemoveRange(athleteRound.Scouts);
                athleteRound.Scouts.Clear();

                foreach (var scout in detailRound.Scouts.OrderBy(item => item.Key))
                {
                    athleteRound.Scouts.Add(new AthleteRoundScouts(0, athleteRound.Id, scout.Key, scout.Value)
                    {
                        AthleteRound = athleteRound,
                    });
                }
            }
            else
            {
                var roundScouts = ReadRoundScouts(summary.Scouts);
                if (roundScouts.Count > 0)
                {
                    athleteRound.Scouts ??= [];
                    dbContext.AthleteRoundScouts.RemoveRange(athleteRound.Scouts);
                    athleteRound.Scouts.Clear();

                    foreach (var scout in roundScouts.OrderBy(item => item.Key))
                    {
                        athleteRound.Scouts.Add(new AthleteRoundScouts(0, athleteRound.Id, scout.Key, scout.Value)
                        {
                            AthleteRound = athleteRound,
                        });
                    }
                }
            }

            upserted++;
        }

        var athleteCoverageRate = summaries.Count == 0
            ? 100m
            : Math.Round((decimal)upserted / summaries.Count * 100m, 2);

        return (upserted, skipped, athleteDetailsFetched, athleteFailuresLogged, athleteCoverageRate);
    }

    private Task<Athlete> EnsureAthleteAsync(
        IDictionary<int, Athlete> athletesByCartolaId,
        GatoMestreAthleteDetailResponse athleteDetail)
    {
        if (athletesByCartolaId.TryGetValue(athleteDetail.Athlete.AthleteId, out var athlete))
        {
            athlete.Name = ToRequiredValue(athleteDetail.Athlete.Name, athleteDetail.Athlete.PopularName, 150);
            athlete.Nickname = ToRequiredValue(athleteDetail.Athlete.PopularName, athleteDetail.Athlete.Nickname, 100);
            return Task.FromResult(athlete);
        }

        athlete = new Athlete(
            0,
            athleteDetail.Athlete.AthleteId,
            ToRequiredValue(athleteDetail.Athlete.Name, athleteDetail.Athlete.PopularName, 150),
            ToRequiredValue(athleteDetail.Athlete.PopularName, athleteDetail.Athlete.Nickname, 100));

        dbContext.Athletes.Add(athlete);
        athletesByCartolaId.Add(athlete.CartolaId, athlete);
        return Task.FromResult(athlete);
    }

    private static async Task<bool> RollbackTransactionAsync(IDbContextTransaction? transaction, CancellationToken cancellationToken)
    {
        if (transaction is null)
        {
            return false;
        }

        await transaction.RollbackAsync(cancellationToken);
        return true;
    }

    private async Task<GatoMestreSyncResult> CompleteRoundNotPublishedAsync(
        int season,
        int round,
        SyncExecutionContext executionContext,
        int? syncExecutionId,
        DateTime startedAtUtc,
        ExternalApiException exception,
        CancellationToken cancellationToken)
    {
        var finishedAtUtc = DateTime.UtcNow;
        var durationMs = (long)(finishedAtUtc - startedAtUtc).TotalMilliseconds;
        var notes = $"The Gato Mestre round payload is not published yet for season {season}, round {round}.";
        var metricsJson = JsonSerializer.Serialize(new
        {
            ClubsUpserted = 0,
            ClubRoundsUpserted = 0,
            ClubRoundPositionsUpserted = 0,
            AthleteRoundsUpserted = 0,
            AthleteRoundsSkipped = 0,
            AthleteDetailsFetched = 0,
            AthleteFailuresLogged = 0,
            AthleteCoverageRate = 0m,
            AthleteSkippedRate = 0m,
            ClubRoundDataAvailable = false,
        });

        if (syncExecutionId.HasValue)
        {
            await syncExecutionLogService.UpdateAsync(
                syncExecutionId.Value,
                new SyncExecutionLogEntry
                {
                    CorrelationId = executionContext.CorrelationId,
                    SourceSystem = executionContext.SourceSystem,
                    ProcessName = executionContext.ProcessName,
                    StartedAtUtc = startedAtUtc,
                    FinishedAtUtc = finishedAtUtc,
                    DurationMs = durationMs,
                    Status = SyncExecutionStatus.Partial,
                    LastStepName = executionContext.CurrentStep,
                    Season = season,
                    Round = round,
                    ErrorCode = IntegrationErrorCode.RoundNotPublished,
                    Notes = notes,
                    MetricsJson = metricsJson,
                    ContextJson = executionContext.ToContextJson(new Dictionary<string, object?>
                    {
                        ["responseStatusCode"] = exception.ResponseStatusCode,
                        ["requestUrl"] = exception.RequestUrl,
                    }),
                    QualityCriteriaJson = JsonSerializer.Serialize(new
                    {
                        MinimumCoverageRate = MinimumHealthyCoverageRate,
                        MaximumSkippedRate = MaximumHealthySkippedRate,
                        QualityCriteriaDescription,
                        QualityCriteriaSatisfied = false,
                    }),
                    CoverageRate = 0m,
                    SkippedRate = 0m,
                    QualityCriteriaSatisfied = false,
                },
                cancellationToken);
        }

        return new GatoMestreSyncResult
        {
            SyncExecutionId = syncExecutionId,
            CorrelationId = executionContext.CorrelationId,
            Status = SyncExecutionStatus.Partial,
            StartedAtUtc = startedAtUtc,
            FinishedAtUtc = finishedAtUtc,
            DurationMs = durationMs,
            Season = season,
            Round = round,
            ClubsUpserted = 0,
            ClubRoundsUpserted = 0,
            ClubRoundPositionsUpserted = 0,
            AthleteRoundsUpserted = 0,
            AthleteRoundsSkipped = 0,
            AthleteDetailsFetched = 0,
            AthleteFailuresLogged = 0,
            AthleteCoverageRate = 0m,
            AthleteSkippedRate = 0m,
            MinimumCoverageRate = MinimumHealthyCoverageRate,
            MaximumSkippedRate = MaximumHealthySkippedRate,
            QualityCriteriaSatisfied = false,
            QualityCriteriaDescription = QualityCriteriaDescription,
            ClubRoundDataAvailable = false,
            IsPartialSync = true,
            Notes = notes,
        };
    }

    private async Task RemoveExistingClubRoundAsync(int requestedRound, int clubId, CancellationToken cancellationToken)
    {
        var existingClubRound = await dbContext.ClubRounds
            .Include(round => round.PositionStats)
            .FirstOrDefaultAsync(round => round.Round == requestedRound && round.ClubId == clubId, cancellationToken);

        if (existingClubRound is null)
        {
            return;
        }

        if (existingClubRound.PositionStats.Count > 0)
        {
            dbContext.ClubRoundPositions.RemoveRange(existingClubRound.PositionStats);
        }

        dbContext.ClubRounds.Remove(existingClubRound);
    }

    private static IntegrationFailureLogEntry BuildFailureLogEntry(
        SyncExecutionContext executionContext,
        Exception exception,
        bool transactionRolledBack,
        string severity)
    {
        var sourceSystem = executionContext.SourceSystem;
        var stepName = executionContext.CurrentStep;
        var errorLocation = executionContext.ProcessName;
        var requestMethod = string.Empty;
        var requestUrl = string.Empty;
        var requestHeadersJson = string.Empty;
        var requestPayload = string.Empty;
        int? responseStatusCode = null;
        var responseReasonPhrase = string.Empty;
        var responseHeadersJson = string.Empty;
        var responsePayload = string.Empty;
        IReadOnlyDictionary<string, object?>? extraContext = null;

        if (exception is ExternalApiException externalApiException)
        {
            sourceSystem = externalApiException.SourceSystem;
            errorLocation = externalApiException.ErrorLocation;
            requestMethod = externalApiException.RequestMethod;
            requestUrl = externalApiException.RequestUrl;
            requestHeadersJson = externalApiException.RequestHeadersJson;
            requestPayload = externalApiException.RequestPayload;
            responseStatusCode = externalApiException.ResponseStatusCode;
            responseReasonPhrase = externalApiException.ResponseReasonPhrase;
            responseHeadersJson = externalApiException.ResponseHeadersJson;
            responsePayload = externalApiException.ResponsePayload;
        }
        else if (exception is DataCollectionIntegrityException integrityException)
        {
            sourceSystem = integrityException.SourceSystem;
            stepName = integrityException.StepName;
            errorLocation = integrityException.ErrorLocation;
            extraContext = integrityException.ContextData;
        }

        return new IntegrationFailureLogEntry
        {
            OccurredAtUtc = DateTime.UtcNow,
            CorrelationId = executionContext.CorrelationId,
            SourceSystem = sourceSystem,
            ProcessName = executionContext.ProcessName,
            StepName = stepName,
            ErrorLocation = errorLocation,
            RequestMethod = requestMethod,
            RequestUrl = requestUrl,
            RequestHeadersJson = requestHeadersJson,
            RequestPayload = requestPayload,
            ResponseStatusCode = responseStatusCode,
            ResponseReasonPhrase = responseReasonPhrase,
            ResponseHeadersJson = responseHeadersJson,
            ResponsePayload = responsePayload,
            ExecutedStepsJson = executionContext.ToExecutedStepsJson(),
            ContextJson = executionContext.ToContextJson(extraContext),
            ErrorCode = IntegrationErrorClassifier.Classify(exception),
            Severity = severity,
            ExceptionType = Truncate(exception.GetType().FullName ?? exception.GetType().Name, 300),
            ExceptionMessage = Truncate(exception.Message, 4000),
            InnerExceptionMessage = Truncate(exception.InnerException?.Message, 4000),
            StackTrace = exception.ToString(),
            TransactionRolledBack = transactionRolledBack,
        };
    }

    private async Task<int> TryLogNonFatalWarningAsync(
        SyncExecutionContext executionContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var errorLogId = await integrationFailureLogService.TryLogAsync(
            BuildFailureLogEntry(executionContext, exception, transactionRolledBack: false, IntegrationLogSeverity.Warning),
            cancellationToken);

        return errorLogId.HasValue ? 1 : 0;
    }

    private static string BuildNotes(
        string clubRoundNote,
        int clubRoundWarningLogsCount,
        (int AthleteRoundsUpserted, int AthleteRoundsSkipped, int AthleteDetailsFetched, int AthleteFailuresLogged, decimal AthleteCoverageRate) athleteRoundResult)
    {
        var notes = new List<string>();

        if (!string.IsNullOrWhiteSpace(clubRoundNote))
        {
            notes.Add(clubRoundNote);
        }

        if (clubRoundWarningLogsCount > 0)
        {
            notes.Add($"Persisted {clubRoundWarningLogsCount} non-fatal club warning logs.");
        }

        if (athleteRoundResult.AthleteRoundsSkipped > 0)
        {
            notes.Add($"Skipped {athleteRoundResult.AthleteRoundsSkipped} athletes due to incomplete or inconsistent enrichment payloads.");
        }

        if (athleteRoundResult.AthleteFailuresLogged > 0)
        {
            notes.Add($"Persisted {athleteRoundResult.AthleteFailuresLogged} non-fatal athlete warning logs.");
        }

        return string.Join(" ", notes);
    }

    private static decimal CalculateSkippedRate(int upserted, int skipped)
    {
        var total = upserted + skipped;
        return total == 0 ? 0m : Math.Round((decimal)skipped / total * 100m, 2);
    }

    private async Task<OptionalRoundPayloadResult<TPayload>> TryLoadOptionalRoundPayloadAsync<TPayload>(
        Func<Task<TPayload>> loader,
        SyncExecutionContext executionContext,
        string warningNote,
        string stepName,
        CancellationToken cancellationToken)
        where TPayload : class
    {
        executionContext.MarkStep(stepName);

        try
        {
            return new OptionalRoundPayloadResult<TPayload>(await loader(), 0, string.Empty);
        }
        catch (ExternalApiException exception)
        {
            var warningLogsCount = await TryLogNonFatalWarningAsync(executionContext, exception, cancellationToken);
            return new OptionalRoundPayloadResult<TPayload>(null, warningLogsCount, warningNote);
        }
    }

    private static void UpsertAthleteRoundContext(
        AthleteRound athleteRound,
        GatoMestreAthleteRoundContentResponse athleteRoundContent)
    {
        athleteRound.Context ??= new AthleteRoundContext
        {
            AthleteRound = athleteRound,
        };

        athleteRound.Context.AthleteRoundId = athleteRound.Id;
        athleteRound.Context.TeamAverageScore = athleteRoundContent.TeamAverageScore;
        athleteRound.Context.TeamHomeAverageCurrent = athleteRoundContent.TeamHomeAverageCurrent;
        athleteRound.Context.TeamHomeAveragePrevious = athleteRoundContent.TeamHomeAveragePrevious;
        athleteRound.Context.TeamAwayAverageCurrent = athleteRoundContent.TeamAwayAverageCurrent;
        athleteRound.Context.TeamAwayAveragePrevious = athleteRoundContent.TeamAwayAveragePrevious;
        athleteRound.Context.PointsPerCartoletaCurrent = athleteRoundContent.PointsPerCartoletaCurrent;
        athleteRound.Context.PointsPerCartoletaPrevious = athleteRoundContent.PointsPerCartoletaPrevious;
        athleteRound.Context.GrossOverallAverageCurrent = athleteRoundContent.GrossOverallAverageCurrent;
        athleteRound.Context.GrossOverallMovingAverageCurrent = athleteRoundContent.GrossOverallMovingAverageCurrent;
        athleteRound.Context.GrossOverallAveragePrevious = athleteRoundContent.GrossOverallAveragePrevious;
        athleteRound.Context.GrossOverallMovingAveragePrevious = athleteRoundContent.GrossOverallMovingAveragePrevious;
        athleteRound.Context.NetOverallAverageCurrent = athleteRoundContent.NetOverallAverageCurrent;
        athleteRound.Context.NetOverallMovingAverageCurrent = athleteRoundContent.NetOverallMovingAverageCurrent;
        athleteRound.Context.NetOverallAveragePrevious = athleteRoundContent.NetOverallAveragePrevious;
        athleteRound.Context.NetOverallMovingAveragePrevious = athleteRoundContent.NetOverallMovingAveragePrevious;
        athleteRound.Context.ValuationCurrent = athleteRoundContent.ValuationCurrent;
        athleteRound.Context.MinimumToAppreciateCurrent = athleteRoundContent.MinimumToAppreciateCurrent;
        athleteRound.Context.ValuationPrevious = athleteRoundContent.ValuationPrevious;
        athleteRound.Context.MinimumToAppreciatePrevious = athleteRoundContent.MinimumToAppreciatePrevious;
        athleteRound.Context.IndexPointsPerCartoleta = ReadNamedMetric(athleteRoundContent.Indices, "ipc");
        athleteRound.Context.IndexOffensiveUsage = ReadNamedMetric(athleteRoundContent.Indices, "iao");
        athleteRound.Context.IndexOffensiveRepresentation = ReadNamedMetric(athleteRoundContent.Indices, "iro");
        athleteRound.Context.IndexPointsRepresentation = ReadNamedMetric(athleteRoundContent.Indices, "irp");
        athleteRound.Context.IndexOffensiveEfficiency = ReadNamedMetric(athleteRoundContent.Indices, "ieo");
        athleteRound.Context.IndexDefensiveEfficiency = ReadNamedMetric(athleteRoundContent.Indices, "ied");
        athleteRound.Context.IndexTackleDuelPercent = ReadNamedMetric(athleteRoundContent.Indices, "pdsc");
        athleteRound.Context.IndexShotsPerGame = ReadNamedMetric(athleteRoundContent.Indices, "fj");
        athleteRound.Context.IndexShotsPerGoal = ReadNamedMetric(athleteRoundContent.Indices, "fg");
    }

    private static Dictionary<int, ClubFavoriteContext> BuildFavoriteContextByClub(
        GatoMestreRoundCollectionResponse<GatoMestreFavoriteMatchResponse>? payload,
        IReadOnlyDictionary<string, int> clubIdByAbbreviation)
    {
        var contexts = new Dictionary<int, ClubFavoriteContext>();
        if (payload is null)
        {
            return contexts;
        }

        foreach (var match in payload.Result)
        {
            if (!TryResolveClubCartolaId(match.HomeTeam, clubIdByAbbreviation, out var homeClubId) ||
                !TryResolveClubCartolaId(match.AwayTeam, clubIdByAbbreviation, out var awayClubId))
            {
                continue;
            }

            contexts[homeClubId] = new ClubFavoriteContext(
                match.Odds.WinProbability,
                match.Odds.DrawProbability,
                match.Odds.LoseProbability);
            contexts[awayClubId] = new ClubFavoriteContext(
                match.Odds.LoseProbability,
                match.Odds.DrawProbability,
                match.Odds.WinProbability);
        }

        return contexts;
    }

    private static Dictionary<int, ClubForecastContext> BuildForecastContextByClub(
        GatoMestreRoundCollectionResponse<GatoMestreForecastMatchResponse>? payload,
        IReadOnlyDictionary<string, int> clubIdByAbbreviation)
    {
        var contexts = new Dictionary<int, ClubForecastContext>();
        if (payload is null)
        {
            return contexts;
        }

        foreach (var match in payload.Result)
        {
            if (!TryResolveClubCartolaId(match.HomeTeam, clubIdByAbbreviation, out var homeClubId) ||
                !TryResolveClubCartolaId(match.AwayTeam, clubIdByAbbreviation, out var awayClubId))
            {
                continue;
            }

            contexts[homeClubId] = new ClubForecastContext(
                match.ResultCode switch
                {
                    1 => 1,
                    2 => -1,
                    _ => 0,
                },
                NormalizeRiskLevel(match.RiskLevel));
            contexts[awayClubId] = new ClubForecastContext(
                match.ResultCode switch
                {
                    2 => 1,
                    1 => -1,
                    _ => 0,
                },
                NormalizeRiskLevel(match.RiskLevel));
        }

        return contexts;
    }

    private static Dictionary<int, GatoMestreClubAverageRankingResponse> BuildAverageRankingByClub(
        GatoMestreRoundCollectionResponse<GatoMestreClubAverageRankingResponse>? payload)
    {
        var rankings = new Dictionary<int, GatoMestreClubAverageRankingResponse>();
        if (payload is null)
        {
            return rankings;
        }

        foreach (var item in payload.Result)
        {
            if (int.TryParse(item.ClubId, out var clubCartolaId))
            {
                rankings[clubCartolaId] = item;
            }
        }

        return rankings;
    }

    private static Dictionary<int, ClubTipSummary> BuildTipSummaryByClub(
        GatoMestreRoundCollectionResponse<GatoMestreTipItemResponse>? payload,
        IReadOnlyDictionary<string, int> clubIdByAbbreviation)
    {
        var mutableSummaries = new Dictionary<int, MutableClubTipSummary>();
        if (payload is null)
        {
            return [];
        }

        foreach (var tip in payload.Result)
        {
            var normalizedType = Normalize(tip.Type);
            if (normalizedType == "TEAM")
            {
                if (TryGetIntProperty(tip.Info, "team_id", out var teamId))
                {
                    var summary = GetOrAddSummary(mutableSummaries, teamId);
                    summary.TeamTipsCount++;
                    if (tip.Hot)
                    {
                        summary.HotTeamTipsCount++;
                    }
                }

                continue;
            }

            if (!TryResolveClubCartolaId(tip.Info, "homeTeam", clubIdByAbbreviation, out var homeClubId) ||
                !TryResolveClubCartolaId(tip.Info, "awayTeam", clubIdByAbbreviation, out var awayClubId))
            {
                continue;
            }

            if (normalizedType == "MATCH")
            {
                IncrementMatchTip(GetOrAddSummary(mutableSummaries, homeClubId), tip.Hot);
                IncrementMatchTip(GetOrAddSummary(mutableSummaries, awayClubId), tip.Hot);
                continue;
            }

            if (normalizedType != "INSIGHT")
            {
                continue;
            }

            var homeSummary = GetOrAddSummary(mutableSummaries, homeClubId);
            var awaySummary = GetOrAddSummary(mutableSummaries, awayClubId);
            IncrementInsight(homeSummary, tip.Hot);
            IncrementInsight(awaySummary, tip.Hot);

            if (TryGetDecimalProperty(tip.Info, "confidence", out var confidence))
            {
                homeSummary.InsightConfidenceTotal += confidence;
                homeSummary.InsightConfidenceSamples++;
                awaySummary.InsightConfidenceTotal += confidence;
                awaySummary.InsightConfidenceSamples++;
            }

            if (!TryGetStringProperty(tip.Info, "verdict", out var verdict))
            {
                continue;
            }

            switch (Normalize(verdict))
            {
                case "HOME_WIN":
                    homeSummary.PositiveInsightCount++;
                    awaySummary.NegativeInsightCount++;
                    break;
                case "AWAY_WIN":
                    awaySummary.PositiveInsightCount++;
                    homeSummary.NegativeInsightCount++;
                    break;
            }
        }

        return mutableSummaries.ToDictionary(
            item => item.Key,
            item => new ClubTipSummary(
                item.Value.TeamTipsCount,
                item.Value.HotTeamTipsCount,
                item.Value.MatchTipsCount,
                item.Value.HotMatchTipsCount,
                item.Value.InsightCount,
                item.Value.HotInsightCount,
                item.Value.InsightConfidenceSamples == 0
                    ? 0m
                    : Math.Round(item.Value.InsightConfidenceTotal / item.Value.InsightConfidenceSamples, 4),
                item.Value.PositiveInsightCount,
                item.Value.NegativeInsightCount));
    }

    private static MutableClubTipSummary GetOrAddSummary(
        IDictionary<int, MutableClubTipSummary> summaries,
        int clubCartolaId)
    {
        if (summaries.TryGetValue(clubCartolaId, out var summary))
        {
            return summary;
        }

        summary = new MutableClubTipSummary();
        summaries[clubCartolaId] = summary;
        return summary;
    }

    private static void IncrementMatchTip(MutableClubTipSummary summary, bool hot)
    {
        summary.MatchTipsCount++;
        if (hot)
        {
            summary.HotMatchTipsCount++;
        }
    }

    private static void IncrementInsight(MutableClubTipSummary summary, bool hot)
    {
        summary.InsightCount++;
        if (hot)
        {
            summary.HotInsightCount++;
        }
    }

    private static bool TryResolveClubCartolaId(
        GatoMestreSimpleTeamReferenceResponse reference,
        IReadOnlyDictionary<string, int> clubIdByAbbreviation,
        out int clubCartolaId)
    {
        return clubIdByAbbreviation.TryGetValue(Normalize(reference.Abbreviation), out clubCartolaId);
    }

    private static bool TryResolveClubCartolaId(
        JsonElement container,
        string propertyName,
        IReadOnlyDictionary<string, int> clubIdByAbbreviation,
        out int clubCartolaId)
    {
        clubCartolaId = 0;

        if (container.ValueKind != JsonValueKind.Object ||
            !container.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!TryGetStringProperty(property, "abbr", out var abbreviation))
        {
            return false;
        }

        return clubIdByAbbreviation.TryGetValue(Normalize(abbreviation), out clubCartolaId);
    }

    private static bool TryGetIntProperty(JsonElement container, string propertyName, out int value)
    {
        value = 0;

        if (container.ValueKind != JsonValueKind.Object || !container.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        if (property.ValueKind == JsonValueKind.Number)
        {
            return property.TryGetInt32(out value);
        }

        return property.ValueKind == JsonValueKind.String && int.TryParse(property.GetString(), out value);
    }

    private static bool TryGetDecimalProperty(JsonElement container, string propertyName, out decimal value)
    {
        value = 0m;

        if (container.ValueKind != JsonValueKind.Object || !container.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        if (property.ValueKind == JsonValueKind.Number)
        {
            return property.TryGetDecimal(out value);
        }

        return property.ValueKind == JsonValueKind.String && decimal.TryParse(property.GetString(), out value);
    }

    private static bool TryGetStringProperty(JsonElement container, string propertyName, out string value)
    {
        value = string.Empty;

        if (container.ValueKind != JsonValueKind.Object ||
            !container.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static decimal GetSnapshotValue(GatoMestreNumericSnapshotResponse? snapshot)
        => snapshot?.Num ?? 0m;

    private static decimal ReadNamedMetric(IReadOnlyCollection<GatoMestreNamedMetricResponse> metrics, string key)
        => metrics.FirstOrDefault(metric => string.Equals(metric.Key, key, StringComparison.OrdinalIgnoreCase))?.Value ?? 0m;

    private static string NormalizeRiskLevel(string? riskLevel)
        => string.IsNullOrWhiteSpace(riskLevel) ? string.Empty : Normalize(riskLevel).ToLowerInvariant();

    private static Club? ResolveClub(
        IReadOnlyDictionary<int, Club> clubs,
        int cartolaClubId,
        int? existingClubId,
        int? latestKnownClubId)
    {
        if (cartolaClubId != 0 && clubs.TryGetValue(cartolaClubId, out var club))
        {
            return club;
        }

        if (existingClubId.HasValue)
        {
            return clubs.Values.FirstOrDefault(clubEntity => clubEntity.Id == existingClubId.Value);
        }

        return latestKnownClubId.HasValue
            ? clubs.Values.FirstOrDefault(clubEntity => clubEntity.Id == latestKnownClubId.Value)
            : null;
    }

    private static Position? ResolvePosition(
        IReadOnlyDictionary<int, Position> positionsByCartolaId,
        GatoMestrePositionDescriptorResponse? descriptor,
        int? existingPositionId,
        int? latestKnownPositionId)
    {
        if (descriptor is not null &&
            PositionMacroToCartolaId.TryGetValue(descriptor.Macro, out var positionId) &&
            positionsByCartolaId.TryGetValue(positionId, out var position))
        {
            return position;
        }

        if (existingPositionId.HasValue)
        {
            return positionsByCartolaId.Values.FirstOrDefault(positionEntity => positionEntity.Id == existingPositionId.Value);
        }

        return latestKnownPositionId.HasValue
            ? positionsByCartolaId.Values.FirstOrDefault(positionEntity => positionEntity.Id == latestKnownPositionId.Value)
            : null;
    }

    private static AthleteStatus? ResolveStatus(
        IReadOnlyDictionary<string, AthleteStatus> statusesByName,
        string? statusName,
        int? existingStatusId,
        int? latestKnownStatusId)
    {
        if (string.IsNullOrWhiteSpace(statusName))
        {
            if (existingStatusId.HasValue)
            {
                return statusesByName.Values.FirstOrDefault(item => item.Id == existingStatusId.Value);
            }

            if (latestKnownStatusId.HasValue)
            {
                return statusesByName.Values.FirstOrDefault(item => item.Id == latestKnownStatusId.Value);
            }

            return statusesByName.GetValueOrDefault(Normalize("Nulo"));
        }

        if (statusesByName.TryGetValue(Normalize(statusName), out var status))
        {
            return status;
        }

        return KnownStatuses.TryGetValue(Normalize(statusName), out var knownId)
            ? statusesByName.Values.FirstOrDefault(item => item.CartolaId == knownId)
            : statusesByName.GetValueOrDefault(Normalize("Nulo"));
    }

    private static GatoMestreClubRoundBlockResponse? ResolveClubBlock(GatoMestreClubConfrontationResponse confrontation, int clubCartolaId)
    {
        if (confrontation.Home.TeamId == clubCartolaId)
        {
            return confrontation.Home;
        }

        if (confrontation.Away.TeamId == clubCartolaId)
        {
            return confrontation.Away;
        }

        return null;
    }

    private static IEnumerable<GatoMestreClubPositionStatsResponse> ResolvePositionStats(GatoMestreClubRoundBlockResponse block)
    {
        return string.Equals(block.NextConfrontation.HomeAwayFlag, "mandante", StringComparison.OrdinalIgnoreCase)
            ? block.PositionStats.Home
            : block.PositionStats.Away;
    }

    private static decimal ResolveWinProbability(GatoMestreClubRoundBlockResponse block)
    {
        return string.Equals(block.NextConfrontation.HomeAwayFlag, "mandante", StringComparison.OrdinalIgnoreCase)
            ? block.NextConfrontation.HomeWinProbability
            : block.NextConfrontation.AwayWinProbability;
    }

    private static decimal ResolveLoseProbability(GatoMestreClubRoundBlockResponse block)
    {
        return string.Equals(block.NextConfrontation.HomeAwayFlag, "mandante", StringComparison.OrdinalIgnoreCase)
            ? block.NextConfrontation.AwayWinProbability
            : block.NextConfrontation.HomeWinProbability;
    }

    private static decimal ResolveExpectedPoints(GatoMestreClubRoundBlockResponse block)
    {
        return string.Equals(block.NextConfrontation.HomeAwayFlag, "mandante", StringComparison.OrdinalIgnoreCase)
            ? block.NextConfrontation.HomeExpectedPoints
            : block.NextConfrontation.AwayExpectedPoints;
    }

    private static int? ReadGames(JsonElement gamesElement, string propertyName)
    {
        if (gamesElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!gamesElement.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Number when property.TryGetInt32(out var value) => value,
            JsonValueKind.Null => 0,
            _ => null,
        };
    }

    private static decimal? ReadAverage(JsonElement averagesElement, string bucketName, string metricName)
    {
        if (averagesElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!averagesElement.TryGetProperty(bucketName, out var bucket))
        {
            return null;
        }

        if (!bucket.TryGetProperty(metricName, out var metric))
        {
            return null;
        }

        return metric.ValueKind switch
        {
            JsonValueKind.Object when metric.TryGetProperty("num", out var num) && num.ValueKind == JsonValueKind.Number && num.TryGetDecimal(out var value) => value,
            JsonValueKind.Object when metric.TryGetProperty("num", out var nestedNum) && nestedNum.ValueKind == JsonValueKind.Null => 0,
            JsonValueKind.Number when metric.TryGetDecimal(out var scalar) => scalar,
            JsonValueKind.Null => 0,
            _ => null,
        };
    }

    private static decimal? ReadSnapshotDecimal(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Number when property.TryGetDecimal(out var value) => value,
            JsonValueKind.Null => 0,
            _ => null,
        };
    }

    private static decimal? ReadSummaryPriceVariation(JsonElement priceElement)
    {
        return ReadSnapshotDecimal(priceElement, "variacao") ??
               ReadSnapshotDecimal(priceElement, "valorizacao");
    }

    private static decimal? ReadSummaryPriceValue(JsonElement priceElement)
    {
        return ReadSnapshotDecimal(priceElement, "num") ??
               ReadSnapshotDecimal(priceElement, "valor");
    }

    private static Dictionary<string, decimal> ReadRoundScouts(JsonElement scoutsElement)
    {
        var scouts = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        if (scoutsElement.ValueKind != JsonValueKind.Object ||
            !scoutsElement.TryGetProperty("rodada", out var roundScouts) ||
            roundScouts.ValueKind != JsonValueKind.Object)
        {
            return scouts;
        }

        foreach (var scout in roundScouts.EnumerateObject())
        {
            if (scout.Value.ValueKind == JsonValueKind.Number && scout.Value.TryGetDecimal(out var value) && value != 0)
            {
                scouts[scout.Name] = value;
            }
        }

        return scouts;
    }

    private static string Normalize(string value)
    {
        var normalized = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC).ToUpperInvariant();
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static string ToRequiredValue(string? value, string? fallback, int maxLength)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? fallback : value;
        return ToOptionalValue(normalized, maxLength, string.Empty);
    }

    private static string ToOptionalValue(string? value, int maxLength, string defaultValue = "")
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        var normalized = value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private sealed record OptionalRoundPayloadResult<TPayload>(TPayload? Payload, int WarningLogsCount, string Note)
        where TPayload : class;

    private sealed record ClubFavoriteContext(decimal WinProbability, decimal DrawProbability, decimal LoseProbability);

    private sealed record ClubForecastContext(int ExpectedResult, string RiskLevel);

    private sealed record ClubTipSummary(
        int TeamTipsCount,
        int HotTeamTipsCount,
        int MatchTipsCount,
        int HotMatchTipsCount,
        int InsightCount,
        int HotInsightCount,
        decimal AverageInsightConfidence,
        int PositiveInsightCount,
        int NegativeInsightCount)
    {
        public static ClubTipSummary Empty { get; } = new(0, 0, 0, 0, 0, 0, 0m, 0, 0);
    }

    private sealed class MutableClubTipSummary
    {
        public int TeamTipsCount { get; set; }

        public int HotTeamTipsCount { get; set; }

        public int MatchTipsCount { get; set; }

        public int HotMatchTipsCount { get; set; }

        public int InsightCount { get; set; }

        public int HotInsightCount { get; set; }

        public decimal InsightConfidenceTotal { get; set; }

        public int InsightConfidenceSamples { get; set; }

        public int PositiveInsightCount { get; set; }

        public int NegativeInsightCount { get; set; }
    }
}
