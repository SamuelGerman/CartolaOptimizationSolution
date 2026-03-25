using System.Text.Json;
using System.Globalization;
using CartolaOptimization.Application.Interfaces;
using CartolaOptimization.Application.Models;
using CartolaOptimization.Domain.Constants;
using CartolaOptimization.Domain.Entities;
using CartolaOptimization.Infrastructure.Context;
using CartolaOptimization.Infrastructure.Diagnostics;
using CartolaOptimization.Infrastructure.Integrations.Cartola;
using CartolaOptimization.Infrastructure.Integrations.Cartola.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CartolaOptimization.Infrastructure.Services;

public sealed class CartolaSyncService(
    AppDbContext dbContext,
    ICartolaApiClient cartolaApiClient,
    IIntegrationFailureLogService integrationFailureLogService,
    ISyncExecutionLogService syncExecutionLogService) : ICartolaSyncService
{
    private static readonly Dictionary<int, CartolaClubResponse> EmptyClubPayload = [];

    public async Task<CartolaSyncResult> SyncAsync(int? matchesRound = null, CancellationToken cancellationToken = default)
    {
        var executionContext = new SyncExecutionContext("Cartola", "CartolaSyncService.SyncAsync");
        executionContext.SetContext("requestedMatchesRound", matchesRound);
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
                Round = matchesRound,
                ContextJson = executionContext.ToContextJson(),
            },
            cancellationToken);

        IDbContextTransaction? transaction = null;

        try
        {
            executionContext.MarkStep("Load market status payload");
            var marketStatus = await cartolaApiClient.GetMarketStatusAsync(cancellationToken);
            executionContext.SetContext("season", marketStatus.Season);
            executionContext.SetContext("currentRound", marketStatus.CurrentRound);

            executionContext.MarkStep("Load clubs payload");
            var clubsPayload = await cartolaApiClient.GetClubsAsync(cancellationToken);

            executionContext.MarkStep("Load athlete market payload");
            var athletesMarketPayload = await cartolaApiClient.GetAthletesMarketAsync(cancellationToken);

            var resolvedMatchesRound = matchesRound ?? marketStatus.CurrentRound;
            executionContext.SetContext("matchesRound", resolvedMatchesRound);

            executionContext.MarkStep("Load matches payload");
            var matchesPayload = await cartolaApiClient.GetMatchesAsync(resolvedMatchesRound, cancellationToken);

            executionContext.MarkStep("Begin database transaction");
            transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            executionContext.MarkStep("Upsert clubs");
            var clubs = await UpsertClubsAsync(clubsPayload, athletesMarketPayload.Clubs, matchesPayload.Clubs, cancellationToken);

            executionContext.MarkStep("Upsert positions");
            var positions = await UpsertPositionsAsync(athletesMarketPayload.Positions, cancellationToken);

            executionContext.MarkStep("Upsert statuses");
            var statuses = await UpsertStatusesAsync(athletesMarketPayload.Statuses, cancellationToken);

            executionContext.MarkStep("Upsert athletes");
            var athletes = await UpsertAthletesAsync(athletesMarketPayload.Athletes, cancellationToken);

            executionContext.MarkStep("Persist catalog changes");
            await dbContext.SaveChangesAsync(cancellationToken);

            executionContext.MarkStep("Upsert matches");
            var matchesUpserted = await UpsertMatchesAsync(matchesPayload, clubs, cancellationToken);

            executionContext.MarkStep("Upsert athlete rounds");
            var athleteRoundResult = await UpsertAthleteRoundsAsync(
                athleteResponses: athletesMarketPayload.Athletes,
                athletes: athletes,
                clubs: clubs,
                positions: positions,
                statuses: statuses,
                targetRoundId: marketStatus.CurrentRound,
                currentRound: marketStatus.CurrentRound,
                cancellationToken: cancellationToken);

            executionContext.MarkStep("Persist round changes");
            await dbContext.SaveChangesAsync(cancellationToken);

            executionContext.MarkStep("Commit database transaction");
            await transaction.CommitAsync(cancellationToken);

            var finishedAtUtc = DateTime.UtcNow;
            var durationMs = (long)(finishedAtUtc - startedAtUtc).TotalMilliseconds;
            const string qualityCriteriaDescription = "Cartola synchronization is healthy when the execution completes without fatal collection or persistence errors.";
            var metricsJson = JsonSerializer.Serialize(new
            {
                marketStatus.Season,
                marketStatus.CurrentRound,
                MatchesRound = matchesPayload.Round,
                ClubsUpserted = clubs.Count,
                PositionsUpserted = positions.Count,
                StatusesUpserted = statuses.Count,
                AthletesUpserted = athletes.Count,
                MatchesUpserted = matchesUpserted,
                AthleteRoundsUpserted = athleteRoundResult.AthleteRoundsUpserted,
                ScoutsUpserted = athleteRoundResult.ScoutsUpserted,
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
                        Status = SyncExecutionStatus.Success,
                        LastStepName = executionContext.CurrentStep,
                        Season = marketStatus.Season,
                        Round = matchesPayload.Round,
                        MetricsJson = metricsJson,
                        ContextJson = executionContext.ToContextJson(),
                        QualityCriteriaJson = JsonSerializer.Serialize(new
                        {
                            Description = qualityCriteriaDescription,
                            QualityCriteriaSatisfied = true,
                        }),
                        QualityCriteriaSatisfied = true,
                    },
                    cancellationToken);
            }

            return new CartolaSyncResult
            {
                SyncExecutionId = syncExecutionId,
                CorrelationId = executionContext.CorrelationId,
                Status = SyncExecutionStatus.Success,
                StartedAtUtc = startedAtUtc,
                FinishedAtUtc = finishedAtUtc,
                DurationMs = durationMs,
                QualityCriteriaSatisfied = true,
                QualityCriteriaDescription = qualityCriteriaDescription,
                Season = marketStatus.Season,
                CurrentRound = marketStatus.CurrentRound,
                MatchesRound = matchesPayload.Round,
                AthleteRounds = athleteRoundResult.RoundIds,
                ClubsUpserted = clubs.Count,
                PositionsUpserted = positions.Count,
                StatusesUpserted = statuses.Count,
                AthletesUpserted = athletes.Count,
                MatchesUpserted = matchesUpserted,
                AthleteRoundsUpserted = athleteRoundResult.AthleteRoundsUpserted,
                ScoutsUpserted = athleteRoundResult.ScoutsUpserted,
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
                        Season = TryGetIntContext(executionContext, "season"),
                        Round = TryGetIntContext(executionContext, "matchesRound") ?? matchesRound,
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
                message: $"Cartola synchronization failed at step '{executionContext.CurrentStep}'. Review IntegrationFailureLogs using correlation '{executionContext.CorrelationId}'.",
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

    public async Task<CartolaSyncResult> SyncClosedRoundAsync(int round, CancellationToken cancellationToken = default)
    {
        if (round <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(round), "Round numbers must be greater than zero.");
        }

        var executionContext = new SyncExecutionContext("Cartola", "CartolaSyncService.SyncClosedRoundAsync");
        executionContext.SetContext("requestedClosedRound", round);
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
                Round = round,
                ContextJson = executionContext.ToContextJson(),
            },
            cancellationToken);

        IDbContextTransaction? transaction = null;

        try
        {
            executionContext.MarkStep("Load market status payload");
            var marketStatus = await cartolaApiClient.GetMarketStatusAsync(cancellationToken);
            executionContext.SetContext("season", marketStatus.Season);
            executionContext.SetContext("currentRound", marketStatus.CurrentRound);

            if (round >= marketStatus.CurrentRound)
            {
                throw new DataCollectionIntegrityException(
                    sourceSystem: "Cartola",
                    stepName: "Validate requested closed round",
                    errorLocation: "CartolaSyncService.SyncClosedRoundAsync",
                    message: $"Round '{round}' is not considered historically closed because the current Cartola round is '{marketStatus.CurrentRound}'.",
                    contextData: new Dictionary<string, object?>
                    {
                        ["requestedRound"] = round,
                        ["currentRound"] = marketStatus.CurrentRound,
                        ["season"] = marketStatus.Season,
                    });
            }

            executionContext.MarkStep("Load closed round score payload");
            var scoresPayload = await cartolaApiClient.GetAthleteScoresAsync(round, cancellationToken);
            executionContext.SetContext("scorePayloadRound", scoresPayload.Round);
            executionContext.SetContext("scorePayloadTotalAthletes", scoresPayload.TotalAthletes);

            executionContext.MarkStep("Begin database transaction");
            transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            executionContext.MarkStep("Upsert clubs");
            var clubs = await UpsertClubsAsync(scoresPayload.Clubs, EmptyClubPayload, EmptyClubPayload, cancellationToken);

            executionContext.MarkStep("Upsert positions");
            var positions = await UpsertPositionsAsync(scoresPayload.Positions, cancellationToken);

            executionContext.MarkStep("Persist catalog changes");
            await dbContext.SaveChangesAsync(cancellationToken);

            executionContext.MarkStep("Finalize athlete rounds from closed score payload");
            var finalizeResult = await FinalizeClosedRoundAthleteResultsAsync(
                scoresPayload,
                clubs,
                positions,
                cancellationToken);

            executionContext.MarkStep("Persist closed round changes");
            await dbContext.SaveChangesAsync(cancellationToken);

            executionContext.MarkStep("Commit database transaction");
            await transaction.CommitAsync(cancellationToken);

            var finishedAtUtc = DateTime.UtcNow;
            var durationMs = (long)(finishedAtUtc - startedAtUtc).TotalMilliseconds;
            var isPartial = finalizeResult.ScorePayloadRowsWithoutLocalRound > 0 || finalizeResult.AthleteRoundsFinalized == 0;
            var status = isPartial ? SyncExecutionStatus.Partial : SyncExecutionStatus.Success;
            var qualityCriteriaSatisfied = !isPartial;
            var notes = isPartial
                ? $"Closed-round sync applied final results to {finalizeResult.AthleteRoundsFinalized} local athlete rounds, but {finalizeResult.ScorePayloadRowsWithoutLocalRound} score rows could not be mapped to a local baseline."
                : string.Empty;
            const string qualityCriteriaDescription = "A closed Cartola round is fully training-ready when every final-score payload row can be reconciled with an existing local athlete-round baseline.";
            var metricsJson = JsonSerializer.Serialize(new
            {
                marketStatus.Season,
                marketStatus.CurrentRound,
                ClosedRound = scoresPayload.Round,
                ScoresPayloadAthletes = scoresPayload.TotalAthletes,
                ClubsUpserted = clubs.Count,
                PositionsUpserted = positions.Count,
                AthleteRoundsFinalized = finalizeResult.AthleteRoundsFinalized,
                ScoutsUpserted = finalizeResult.ScoutsUpserted,
                AthleteRoundsMissingScorePayload = finalizeResult.AthleteRoundsMissingScorePayload,
                ScorePayloadRowsWithoutLocalRound = finalizeResult.ScorePayloadRowsWithoutLocalRound,
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
                        Status = status,
                        LastStepName = executionContext.CurrentStep,
                        Season = marketStatus.Season,
                        Round = scoresPayload.Round,
                        MetricsJson = metricsJson,
                        ContextJson = executionContext.ToContextJson(),
                        QualityCriteriaJson = JsonSerializer.Serialize(new
                        {
                            Description = qualityCriteriaDescription,
                            QualityCriteriaSatisfied = qualityCriteriaSatisfied,
                        }),
                        QualityCriteriaSatisfied = qualityCriteriaSatisfied,
                        ErrorCode = isPartial ? IntegrationErrorCode.IncompleteHistoricalBaseline : string.Empty,
                        Notes = notes,
                    },
                    cancellationToken);
            }

            return new CartolaSyncResult
            {
                SyncExecutionId = syncExecutionId,
                CorrelationId = executionContext.CorrelationId,
                Status = status,
                StartedAtUtc = startedAtUtc,
                FinishedAtUtc = finishedAtUtc,
                DurationMs = durationMs,
                QualityCriteriaSatisfied = qualityCriteriaSatisfied,
                QualityCriteriaDescription = qualityCriteriaDescription,
                Season = marketStatus.Season,
                CurrentRound = marketStatus.CurrentRound,
                MatchesRound = scoresPayload.Round,
                AthleteRounds = [scoresPayload.Round],
                ClubsUpserted = clubs.Count,
                PositionsUpserted = positions.Count,
                StatusesUpserted = 0,
                AthletesUpserted = 0,
                MatchesUpserted = 0,
                AthleteRoundsUpserted = finalizeResult.AthleteRoundsFinalized,
                ScoutsUpserted = finalizeResult.ScoutsUpserted,
                AthleteRoundsMissingScorePayload = finalizeResult.AthleteRoundsMissingScorePayload,
                ScorePayloadRowsWithoutLocalRound = finalizeResult.ScorePayloadRowsWithoutLocalRound,
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
                        Season = TryGetIntContext(executionContext, "season"),
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
                message: $"Cartola closed-round synchronization failed at step '{executionContext.CurrentStep}'. Review IntegrationFailureLogs using correlation '{executionContext.CorrelationId}'.",
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

    private async Task<Dictionary<int, Club>> UpsertClubsAsync(
        Dictionary<int, CartolaClubResponse> clubsResponse,
        Dictionary<int, CartolaClubResponse> marketClubsResponse,
        Dictionary<int, CartolaClubResponse> matchClubsResponse,
        CancellationToken cancellationToken)
    {
        var allClubs = new Dictionary<int, CartolaClubResponse>();

        MergeClubs(allClubs, clubsResponse);
        MergeClubs(allClubs, marketClubsResponse);
        MergeClubs(allClubs, matchClubsResponse);

        var existingClubs = await dbContext.Clubs
            .ToDictionaryAsync(club => club.CartolaId, cancellationToken);

        foreach (var clubResponse in allClubs.Values.OrderBy(club => club.Id))
        {
            if (!existingClubs.TryGetValue(clubResponse.Id, out var club))
            {
                club = new Club(
                    id: 0,
                    cartolaId: clubResponse.Id,
                    abreviation: ToRequiredValue(clubResponse.Abbreviation, clubResponse.Name, 10),
                    slug: ToOptionalValue(clubResponse.Slug, 100),
                    nickname: ResolveClubNickname(clubResponse),
                    logoUrl: ResolveClubLogoUrl(clubResponse));

                dbContext.Clubs.Add(club);
                existingClubs.Add(clubResponse.Id, club);
            }

            club.CartolaId = clubResponse.Id;
            club.Abreviation = ToRequiredValue(clubResponse.Abbreviation, clubResponse.Name, 10);
            club.Slug = ToOptionalValue(clubResponse.Slug, 100);
            club.Nickname = ResolveClubNickname(clubResponse);
            club.LogoUrl = ResolveClubLogoUrl(clubResponse);
        }

        return existingClubs;
    }

    private async Task<Dictionary<int, Position>> UpsertPositionsAsync(
        Dictionary<int, CartolaPositionResponse> positionsResponse,
        CancellationToken cancellationToken)
    {
        var existingPositions = await dbContext.Positions
            .ToDictionaryAsync(position => position.CartolaId, cancellationToken);

        foreach (var positionResponse in positionsResponse.Values.OrderBy(position => position.Id))
        {
            if (!existingPositions.TryGetValue(positionResponse.Id, out var position))
            {
                position = new Position(
                    id: 0,
                    cartolaId: positionResponse.Id,
                    name: ToRequiredValue(positionResponse.Name, $"Position {positionResponse.Id}", 50),
                    abreviation: ToRequiredValue(positionResponse.Abbreviation, $"P{positionResponse.Id}", 5));

                dbContext.Positions.Add(position);
                existingPositions.Add(positionResponse.Id, position);
            }

            position.CartolaId = positionResponse.Id;
            position.Name = ToRequiredValue(positionResponse.Name, $"Position {positionResponse.Id}", 50);
            position.Abreviation = ToRequiredValue(positionResponse.Abbreviation, $"P{positionResponse.Id}", 5);
        }

        return existingPositions;
    }

    private async Task<Dictionary<int, AthleteStatus>> UpsertStatusesAsync(
        Dictionary<int, CartolaStatusResponse> statusesResponse,
        CancellationToken cancellationToken)
    {
        var existingStatuses = await dbContext.AthleteStatuses
            .ToDictionaryAsync(status => status.CartolaId, cancellationToken);

        foreach (var statusResponse in statusesResponse.Values.OrderBy(status => status.Id))
        {
            if (!existingStatuses.TryGetValue(statusResponse.Id, out var status))
            {
                status = new AthleteStatus(
                    id: 0,
                    cartolaId: statusResponse.Id,
                    name: ToRequiredValue(statusResponse.Name, $"Status {statusResponse.Id}", 50));

                dbContext.AthleteStatuses.Add(status);
                existingStatuses.Add(statusResponse.Id, status);
            }

            status.CartolaId = statusResponse.Id;
            status.Name = ToRequiredValue(statusResponse.Name, $"Status {statusResponse.Id}", 50);
        }

        return existingStatuses;
    }

    private async Task<Dictionary<int, Athlete>> UpsertAthletesAsync(
        IEnumerable<CartolaAthleteResponse> athletesResponse,
        CancellationToken cancellationToken)
    {
        var existingAthletes = await dbContext.Athletes
            .ToDictionaryAsync(athlete => athlete.CartolaId, cancellationToken);

        foreach (var athleteResponse in athletesResponse.OrderBy(athlete => athlete.AthleteId))
        {
            if (!existingAthletes.TryGetValue(athleteResponse.AthleteId, out var athlete))
            {
                athlete = new Athlete(
                    id: 0,
                    cartolaId: athleteResponse.AthleteId,
                    name: ResolveAthleteName(athleteResponse),
                    nickname: ResolveAthleteNickname(athleteResponse));

                dbContext.Athletes.Add(athlete);
                existingAthletes.Add(athleteResponse.AthleteId, athlete);
            }

            athlete.CartolaId = athleteResponse.AthleteId;
            athlete.Name = ResolveAthleteName(athleteResponse);
            athlete.Nickname = ResolveAthleteNickname(athleteResponse);
        }

        return existingAthletes;
    }

    private async Task<int> UpsertMatchesAsync(
        CartolaMatchesResponse matchesResponse,
        IReadOnlyDictionary<int, Club> clubs,
        CancellationToken cancellationToken)
    {
        var matchIds = matchesResponse.Matches
            .Select(match => match.MatchId)
            .ToArray();

        var existingMatches = await dbContext.Matches
            .Where(match => matchIds.Contains(match.CartolaId))
            .ToDictionaryAsync(match => match.CartolaId, cancellationToken);

        var upserted = 0;

        foreach (var matchResponse in matchesResponse.Matches.OrderBy(match => match.MatchId))
        {
            if (!clubs.TryGetValue(matchResponse.HomeClubId, out var homeClub) ||
                !clubs.TryGetValue(matchResponse.AwayClubId, out var awayClub))
            {
                throw new DataCollectionIntegrityException(
                    sourceSystem: "Cartola",
                    stepName: "Upsert matches",
                    errorLocation: "CartolaSyncService.UpsertMatchesAsync",
                    message: $"Match '{matchResponse.MatchId}' references clubs that were not loaded into the local catalog.",
                    contextData: new Dictionary<string, object?>
                    {
                        ["matchId"] = matchResponse.MatchId,
                        ["round"] = matchesResponse.Round,
                        ["homeClubCartolaId"] = matchResponse.HomeClubId,
                        ["awayClubCartolaId"] = matchResponse.AwayClubId,
                    });
            }

            if (!existingMatches.TryGetValue(matchResponse.MatchId, out var match))
            {
                match = new Match(
                    id: 0,
                    cartolaId: matchResponse.MatchId,
                    round: matchesResponse.Round,
                    matchDate: ParseMatchDate(matchResponse.MatchDate),
                    location: ToOptionalValue(matchResponse.Location, 150),
                    validForCartola: matchResponse.ValidForCartola,
                    homeClubId: homeClub.Id,
                    homeClubPosition: matchResponse.HomeClubPosition,
                    homeForm: ResolveForm(matchResponse.HomeForm),
                    homeScore: matchResponse.HomeScore,
                    awayClubId: awayClub.Id,
                    awayClubPosition: matchResponse.AwayClubPosition,
                    awayForm: ResolveForm(matchResponse.AwayForm),
                    awayScore: matchResponse.AwayScore);

                dbContext.Matches.Add(match);
                existingMatches.Add(matchResponse.MatchId, match);
            }

            match.CartolaId = matchResponse.MatchId;
            match.Round = matchesResponse.Round;
            match.MatchDate = ParseMatchDate(matchResponse.MatchDate);
            match.Location = ToOptionalValue(matchResponse.Location, 150);
            match.ValidForCartola = matchResponse.ValidForCartola;
            match.HomeClubId = homeClub.Id;
            match.HomeClubPosition = matchResponse.HomeClubPosition;
            match.HomeForm = ResolveForm(matchResponse.HomeForm);
            match.HomeScore = matchResponse.HomeScore;
            match.AwayClubId = awayClub.Id;
            match.AwayClubPosition = matchResponse.AwayClubPosition;
            match.AwayForm = ResolveForm(matchResponse.AwayForm);
            match.AwayScore = matchResponse.AwayScore;

            upserted++;
        }

        return upserted;
    }

    private async Task<(int AthleteRoundsUpserted, int ScoutsUpserted, IReadOnlyCollection<int> RoundIds)> UpsertAthleteRoundsAsync(
        IReadOnlyCollection<CartolaAthleteResponse> athleteResponses,
        IReadOnlyDictionary<int, Athlete> athletes,
        IReadOnlyDictionary<int, Club> clubs,
        IReadOnlyDictionary<int, Position> positions,
        IReadOnlyDictionary<int, AthleteStatus> statuses,
        int targetRoundId,
        int currentRound,
        CancellationToken cancellationToken)
    {
        var roundIds = new[] { targetRoundId };

        var existingRounds = await dbContext.AthleteRounds
            .Include(round => round.Scouts)
            .Where(round => roundIds.Contains(round.RoundId))
            .ToDictionaryAsync(round => (round.RoundId, round.AthleteId), cancellationToken);
        var clubRoundKeys = await dbContext.ClubRounds
            .Where(round => roundIds.Contains(round.Round) && round.PositionStats.Any())
            .Select(round => new { round.Round, round.ClubId })
            .ToListAsync(cancellationToken);
        var clubRoundKeySet = clubRoundKeys
            .Select(item => (item.Round, item.ClubId))
            .ToHashSet();

        var roundsUpserted = 0;
        var scoutsUpserted = 0;

        foreach (var athleteResponse in athleteResponses.OrderBy(athlete => athlete.RoundId).ThenBy(athlete => athlete.AthleteId))
        {
            if (!athletes.TryGetValue(athleteResponse.AthleteId, out var athlete) ||
                !clubs.TryGetValue(athleteResponse.ClubId, out var club) ||
                !positions.TryGetValue(athleteResponse.PositionId, out var position) ||
                !statuses.TryGetValue(athleteResponse.StatusId, out var status))
            {
                throw new DataCollectionIntegrityException(
                    sourceSystem: "Cartola",
                    stepName: "Upsert athlete rounds",
                    errorLocation: "CartolaSyncService.UpsertAthleteRoundsAsync",
                    message: $"Athlete round '{athleteResponse.RoundId}/{athleteResponse.AthleteId}' references catalog data that was not loaded.",
                    contextData: new Dictionary<string, object?>
                    {
                        ["roundId"] = athleteResponse.RoundId,
                        ["athleteCartolaId"] = athleteResponse.AthleteId,
                        ["clubCartolaId"] = athleteResponse.ClubId,
                        ["positionCartolaId"] = athleteResponse.PositionId,
                        ["statusCartolaId"] = athleteResponse.StatusId,
                    });
            }

            var key = (targetRoundId, athlete.Id);

            if (!existingRounds.TryGetValue(key, out var athleteRound))
            {
                athleteRound = new AthleteRound(
                    id: 0,
                    roundId: targetRoundId,
                    athleteId: athlete.Id,
                    clubId: club.Id,
                    positionId: position.Id,
                    statusId: status.Id,
                    gamesNum: athleteResponse.Games,
                    homeGamesNum: 0,
                    awayGamesNum: 0,
                    scoreNum: athleteResponse.Score,
                    movingAverage: athleteResponse.AverageScore,
                    averageScoreNum: athleteResponse.AverageScore,
                    homeAverageScore: 0,
                    awayAverageScore: 0,
                    variationNum: athleteResponse.PriceVariation,
                    priceNum: athleteResponse.Price,
                    wasInGame: athleteResponse.WasInGame)
                {
                    Scouts = [],
                };

                dbContext.AthleteRounds.Add(athleteRound);
                existingRounds.Add(key, athleteRound);
            }

            athleteRound.RoundId = targetRoundId;
            athleteRound.AthleteId = athlete.Id;
            athleteRound.ClubId = club.Id;
            athleteRound.PositionId = position.Id;
            athleteRound.StatusId = status.Id;
            athleteRound.GamesNum = athleteResponse.Games;
            athleteRound.HomeGamesNum = 0;
            athleteRound.AwayGamesNum = 0;
            athleteRound.ScoreNum = athleteResponse.Score;
            athleteRound.MovingAverage = athleteResponse.AverageScore;
            athleteRound.AverageScoreNum = athleteResponse.AverageScore;
            athleteRound.HomeAverageScore = 0;
            athleteRound.AwayAverageScore = 0;
            athleteRound.VariationNum = athleteResponse.PriceVariation;
            athleteRound.PriceNum = athleteResponse.Price;
            athleteRound.WasInGame = athleteResponse.WasInGame;
            TrainingReadinessEvaluator.ApplyCartolaBaseline(
                athleteRound,
                currentRound,
                clubRoundKeySet.Contains((athleteRound.RoundId, athleteRound.ClubId)));

            athleteRound.Scouts ??= [];
            dbContext.AthleteRoundScouts.RemoveRange(athleteRound.Scouts);
            athleteRound.Scouts.Clear();

            foreach (var scout in OrderScouts(athleteResponse.Scout))
            {
                var scoutEntity = new AthleteRoundScouts(
                    id: 0,
                    athleteRoundId: athleteRound.Id,
                    scoutKey: ToRequiredValue(scout.Key, "UNK", 5),
                    valor: scout.Value)
                {
                    AthleteRound = athleteRound,
                };

                athleteRound.Scouts.Add(scoutEntity);
                scoutsUpserted++;
            }

            roundsUpserted++;
        }

        return (roundsUpserted, scoutsUpserted, roundIds);
    }

    private async Task<(int AthleteRoundsFinalized, int ScoutsUpserted, int AthleteRoundsMissingScorePayload, int ScorePayloadRowsWithoutLocalRound)> FinalizeClosedRoundAthleteResultsAsync(
        CartolaAthleteScoresResponse scoresPayload,
        IReadOnlyDictionary<int, Club> clubs,
        IReadOnlyDictionary<int, Position> positions,
        CancellationToken cancellationToken)
    {
        var existingRounds = await dbContext.AthleteRounds
            .Include(round => round.Athlete)
            .Include(round => round.Scouts)
            .Where(round => round.RoundId == scoresPayload.Round)
            .ToListAsync(cancellationToken);
        var existingRoundsByAthleteCartolaId = existingRounds
            .ToDictionary(round => round.Athlete.CartolaId);
        var clubRoundClubIds = await dbContext.ClubRounds
            .Where(round => round.Round == scoresPayload.Round)
            .Select(round => round.ClubId)
            .ToListAsync(cancellationToken);
        var clubRoundKeySet = clubRoundClubIds.ToHashSet();

        var athleteRoundsFinalized = 0;
        var scoutsUpserted = 0;
        var athleteRoundsMissingScorePayload = 0;
        var scorePayloadRowsWithoutLocalRound = 0;
        var scoredAthleteIds = scoresPayload.Athletes.Keys.ToHashSet();

        foreach (var athleteScore in scoresPayload.Athletes.OrderBy(item => item.Key))
        {
            if (!existingRoundsByAthleteCartolaId.TryGetValue(athleteScore.Key, out var athleteRound))
            {
                scorePayloadRowsWithoutLocalRound++;
                continue;
            }

            if (!clubs.TryGetValue(athleteScore.Value.ClubId, out var club) ||
                !positions.TryGetValue(athleteScore.Value.PositionId, out var position))
            {
                throw new DataCollectionIntegrityException(
                    sourceSystem: "Cartola",
                    stepName: "Finalize athlete rounds from closed score payload",
                    errorLocation: "CartolaSyncService.FinalizeClosedRoundAthleteResultsAsync",
                    message: $"Closed score row '{scoresPayload.Round}/{athleteScore.Key}' references catalog data that could not be resolved locally.",
                    contextData: new Dictionary<string, object?>
                    {
                        ["roundId"] = scoresPayload.Round,
                        ["athleteCartolaId"] = athleteScore.Key,
                        ["clubCartolaId"] = athleteScore.Value.ClubId,
                        ["positionCartolaId"] = athleteScore.Value.PositionId,
                    });
            }

            athleteRound.ClubId = club.Id;
            athleteRound.PositionId = position.Id;
            athleteRound.ScoreNum = athleteScore.Value.Score;
            athleteRound.WasInGame = athleteScore.Value.WasInGame;
            athleteRound.DataPhase = RoundDataPhaseValues.HistoricalClosed;

            athleteRound.Scouts ??= [];
            dbContext.AthleteRoundScouts.RemoveRange(athleteRound.Scouts);
            athleteRound.Scouts.Clear();

            foreach (var scout in OrderScouts(athleteScore.Value.Scout))
            {
                var scoutEntity = new AthleteRoundScouts(
                    id: 0,
                    athleteRoundId: athleteRound.Id,
                    scoutKey: ToRequiredValue(scout.Key, "UNK", 5),
                    valor: scout.Value)
                {
                    AthleteRound = athleteRound,
                };

                athleteRound.Scouts.Add(scoutEntity);
                scoutsUpserted++;
            }

            TrainingReadinessEvaluator.ApplyAthleteReadiness(
                athleteRound,
                clubRoundKeySet.Contains(athleteRound.ClubId));

            athleteRoundsFinalized++;
        }

        foreach (var athleteRound in existingRounds.Where(round => !scoredAthleteIds.Contains(round.Athlete.CartolaId)))
        {
            athleteRound.ScoreNum = 0m;
            athleteRound.WasInGame = false;
            athleteRound.DataPhase = RoundDataPhaseValues.HistoricalClosed;
            athleteRound.Scouts ??= [];
            dbContext.AthleteRoundScouts.RemoveRange(athleteRound.Scouts);
            athleteRound.Scouts.Clear();
            TrainingReadinessEvaluator.ApplyAthleteReadiness(
                athleteRound,
                clubRoundKeySet.Contains(athleteRound.ClubId));
            athleteRoundsMissingScorePayload++;
            athleteRoundsFinalized++;
        }

        return (athleteRoundsFinalized, scoutsUpserted, athleteRoundsMissingScorePayload, scorePayloadRowsWithoutLocalRound);
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

    private static int? TryGetIntContext(SyncExecutionContext executionContext, string key)
    {
        if (executionContext.GetContextData().TryGetValue(key, out var value))
        {
            return value switch
            {
                int intValue => intValue,
                long longValue => (int)longValue,
                _ => null,
            };
        }

        return null;
    }

    private static IEnumerable<KeyValuePair<string, decimal>> OrderScouts(Dictionary<string, decimal>? scoutValues)
        => (scoutValues ?? []).OrderBy(scout => scout.Key);

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static void MergeClubs(
        IDictionary<int, CartolaClubResponse> target,
        IReadOnlyDictionary<int, CartolaClubResponse> source)
    {
        foreach (var club in source)
        {
            target[club.Key] = club.Value;
        }
    }

    private static string ResolveClubNickname(CartolaClubResponse clubResponse)
    {
        var preferredValue = string.IsNullOrWhiteSpace(clubResponse.FantasyName)
            ? clubResponse.Nickname
            : clubResponse.FantasyName;

        return ToRequiredValue(preferredValue, clubResponse.Name, 100);
    }

    private static string ResolveClubLogoUrl(CartolaClubResponse clubResponse)
    {
        return ToOptionalValue(
            clubResponse.Shields?.Size60 ??
            clubResponse.Shields?.Size45 ??
            clubResponse.Shields?.Size30,
            255);
    }

    private static string ResolveAthleteName(CartolaAthleteResponse athleteResponse)
    {
        return ToRequiredValue(athleteResponse.Name, athleteResponse.Nickname, 150);
    }

    private static string ResolveAthleteNickname(CartolaAthleteResponse athleteResponse)
    {
        return ToRequiredValue(athleteResponse.Nickname, athleteResponse.ShortNickname, 100);
    }

    private static string ResolveForm(IEnumerable<string> values)
    {
        return ToOptionalValue(values is null ? string.Empty : string.Concat(values), 10);
    }

    private static DateTime ParseMatchDate(string rawValue)
    {
        if (DateTime.TryParseExact(
                rawValue,
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsedDate))
        {
            return parsedDate;
        }

        return DateTime.Parse(rawValue, CultureInfo.InvariantCulture);
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
        return normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength];
    }
}
