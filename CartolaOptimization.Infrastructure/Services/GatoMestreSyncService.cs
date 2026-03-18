using System.Text.Json;
using CartolaOptimization.Application.Interfaces;
using CartolaOptimization.Application.Models;
using CartolaOptimization.Domain.Entities;
using CartolaOptimization.Infrastructure.Context;
using CartolaOptimization.Infrastructure.Integrations.GatoMestre;
using CartolaOptimization.Infrastructure.Integrations.GatoMestre.Models;
using Microsoft.EntityFrameworkCore;

namespace CartolaOptimization.Infrastructure.Services;

/// <summary>
/// Synchronizes team and athlete enrichment data published by Gato Mestre.
/// </summary>
public sealed class GatoMestreSyncService(
    AppDbContext dbContext,
    IGatoMestreApiClient gatoMestreApiClient) : IGatoMestreSyncService
{
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
        ["DÚVIDA"] = 2,
        ["DUVIDA"] = 2,
        ["SUSPENSO"] = 3,
        ["CONTUNDIDO"] = 5,
        ["NULO"] = 6,
        ["PROVÁVEL"] = 7,
        ["PROVAVEL"] = 7,
    };

    public async Task<GatoMestreSyncResult> SyncRoundAsync(int season, int round, CancellationToken cancellationToken = default)
    {
        var teamsPayload = await gatoMestreApiClient.GetTeamsAsync(cancellationToken);
        var athletesRoundPayload = await gatoMestreApiClient.GetAthletesRoundAsync(season, round, cancellationToken);

        await EnsureKnownPositionsAsync(cancellationToken);
        await EnsureKnownStatusesAsync(cancellationToken);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var clubs = await UpsertClubsAsync(teamsPayload, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        var clubRoundResult = await UpsertClubRoundsAsync(round, clubs, cancellationToken);
        var athleteRoundResult = await UpsertAthleteRoundsAsync(round, athletesRoundPayload.Result, clubs, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new GatoMestreSyncResult
        {
            Season = season,
            Round = round,
            ClubsUpserted = clubs.Count,
            ClubRoundsUpserted = clubRoundResult.ClubRoundsUpserted,
            ClubRoundPositionsUpserted = clubRoundResult.PositionStatsUpserted,
            AthleteRoundsUpserted = athleteRoundResult.AthleteRoundsUpserted,
            AthleteDetailsFetched = athleteRoundResult.AthleteDetailsFetched,
            ClubRoundDataAvailable = clubRoundResult.DataAvailable,
            Notes = clubRoundResult.Note,
        };
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
            new Position(0, 6, "Técnico", "tec"),
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
            new AthleteStatus(0, 2, "Dúvida"),
            new AthleteStatus(0, 3, "Suspenso"),
            new AthleteStatus(0, 5, "Contundido"),
            new AthleteStatus(0, 6, "Nulo"),
            new AthleteStatus(0, 7, "Provável"),
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
                    ToOptionalValue(team.Shields.Size60, 255));

                dbContext.Clubs.Add(club);
                existingClubs.Add(team.TeamId, club);
            }

            club.CartolaId = team.TeamId;
            club.Abreviation = ToRequiredValue(team.Acronym, team.Name, 10);
            club.Slug = ToOptionalValue(team.Slug, 100);
            club.Nickname = ToRequiredValue(string.IsNullOrWhiteSpace(team.PopularName) ? team.Nickname : team.PopularName, team.Name, 100);
            club.LogoUrl = ToOptionalValue(team.Shields.Size60, 255);
        }

        return existingClubs;
    }

    private async Task<(int ClubRoundsUpserted, int PositionStatsUpserted, bool DataAvailable, string Note)> UpsertClubRoundsAsync(
        int requestedRound,
        IReadOnlyDictionary<int, Club> clubs,
        CancellationToken cancellationToken)
    {
        var positions = await dbContext.Positions.ToDictionaryAsync(position => position.CartolaId, cancellationToken);
        var processedClubRounds = 0;
        var processedPositionStats = 0;
        var dataAvailable = true;
        var note = string.Empty;

        foreach (var club in clubs.Values.OrderBy(club => club.CartolaId))
        {
            var confrontation = await gatoMestreApiClient.GetClubConfrontationAsync(club.CartolaId, cancellationToken);

            if (confrontation.Round != requestedRound)
            {
                dataAvailable = false;
                note = $"The confrontation endpoint currently exposes round {confrontation.Round}, so club round data was not synchronized for requested round {requestedRound}.";
                continue;
            }

            var clubBlock = ResolveClubBlock(confrontation, club.CartolaId);
            if (clubBlock is null)
            {
                continue;
            }

            var existingClubRound = await dbContext.ClubRounds
                .Include(round => round.PositionStats)
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

            foreach (var positionStat in ResolvePositionStats(clubBlock).OrderBy(item => item.PositionId))
            {
                if (!positions.TryGetValue(positionStat.PositionId, out var position))
                {
                    continue;
                }

                existingClubRound.PositionStats.Add(new ClubRoundPosition(
                    0,
                    existingClubRound.Id,
                    position.Id,
                    positionStat.PointsConcededAverage,
                    positionStat.PointsConqueredAverage)
                {
                    ClubRound = existingClubRound,
                    Position = position,
                });

                processedPositionStats++;
            }

            processedClubRounds++;
        }

        return (processedClubRounds, processedPositionStats, dataAvailable, note);
    }

    private async Task<(int AthleteRoundsUpserted, int AthleteDetailsFetched)> UpsertAthleteRoundsAsync(
        int round,
        IReadOnlyCollection<GatoMestreAthleteRoundSummaryResponse> summaries,
        IReadOnlyDictionary<int, Club> clubs,
        CancellationToken cancellationToken)
    {
        var athletesByCartolaId = await dbContext.Athletes.ToDictionaryAsync(athlete => athlete.CartolaId, cancellationToken);
        var positionsByCartolaId = await dbContext.Positions.ToDictionaryAsync(position => position.CartolaId, cancellationToken);
        var statusesByName = await dbContext.AthleteStatuses.ToDictionaryAsync(status => Normalize(status.Name), cancellationToken);

        var athleteIds = athletesByCartolaId.Values.Select(athlete => athlete.Id).ToArray();
        var existingRounds = await dbContext.AthleteRounds
            .Include(roundEntity => roundEntity.Scouts)
            .Where(roundEntity => roundEntity.RoundId == round && athleteIds.Contains(roundEntity.AthleteId))
            .ToDictionaryAsync(roundEntity => roundEntity.AthleteId, cancellationToken);

        var upserted = 0;
        var athleteDetailsFetched = 0;

        foreach (var summary in summaries.OrderBy(item => item.AthleteId))
        {
            athletesByCartolaId.TryGetValue(summary.AthleteId, out var athlete);
            existingRounds.TryGetValue(athlete?.Id ?? 0, out var athleteRound);

            GatoMestreAthleteDetailResponse? athleteDetail = null;
            GatoMestreAthleteRoundDetailResponse? detailRound = null;

            if (athlete is null || athleteRound is null || athleteRound.StatusId == 0 || athleteRound.PositionId == 0 || athleteRound.ClubId == 0)
            {
                athleteDetail = await gatoMestreApiClient.GetAthleteDetailAsync(summary.AthleteId, cancellationToken);
                detailRound = athleteDetail.Rounds.FirstOrDefault(item => item.Round == round);
                athleteDetailsFetched++;

                athlete = await EnsureAthleteAsync(athletesByCartolaId, athleteDetail, cancellationToken);
                existingRounds.TryGetValue(athlete.Id, out athleteRound);
            }

            var club = ResolveClub(clubs, athleteDetail?.Team.TeamId ?? 0, athleteRound?.ClubId);
            var position = ResolvePosition(positionsByCartolaId, athleteDetail?.Athlete.Position);
            var status = ResolveStatus(statusesByName, detailRound?.StatusName);

            if (athleteRound is null)
            {
                if (club is null || position is null || status is null)
                {
                    continue;
                }

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
                    detailRound?.Points?.Num ?? 0,
                    ReadAverage(summary.Averages, "geral", "movel") ?? detailRound?.Averages.Overall.Moving?.Num ?? 0,
                    ReadAverage(summary.Averages, "geral", "total") ?? detailRound?.Averages.Overall.Total?.Num ?? 0,
                    ReadAverage(summary.Averages, "mandante", "total") ?? detailRound?.Averages.Home.Total?.Num ?? 0,
                    ReadAverage(summary.Averages, "visitante", "total") ?? detailRound?.Averages.Away.Total?.Num ?? 0,
                    detailRound?.Price?.Variation ?? 0,
                    detailRound?.Price?.Num ?? 0,
                    detailRound?.WasInGame ?? false)
                {
                    Scouts = [],
                };

                dbContext.AthleteRounds.Add(athleteRound);
                existingRounds[athlete.Id] = athleteRound;
            }

            if (club is not null)
            {
                athleteRound.ClubId = club.Id;
            }

            if (position is not null)
            {
                athleteRound.PositionId = position.Id;
            }

            if (status is not null)
            {
                athleteRound.StatusId = status.Id;
            }

            athleteRound.RoundId = round;
            athleteRound.AthleteId = athlete.Id;
            athleteRound.GamesNum = ReadGames(summary.Games, "geral") ?? detailRound?.GamesCount ?? athleteRound.GamesNum;
            athleteRound.HomeGamesNum = ReadGames(summary.Games, "mandante") ?? athleteRound.HomeGamesNum;
            athleteRound.AwayGamesNum = ReadGames(summary.Games, "visitante") ?? athleteRound.AwayGamesNum;
            athleteRound.MovingAverage = ReadAverage(summary.Averages, "geral", "movel") ?? detailRound?.Averages.Overall.Moving?.Num ?? athleteRound.MovingAverage;
            athleteRound.AverageScoreNum = ReadAverage(summary.Averages, "geral", "total") ?? detailRound?.Averages.Overall.Total?.Num ?? athleteRound.AverageScoreNum;
            athleteRound.HomeAverageScore = ReadAverage(summary.Averages, "mandante", "total") ?? detailRound?.Averages.Home.Total?.Num ?? athleteRound.HomeAverageScore;
            athleteRound.AwayAverageScore = ReadAverage(summary.Averages, "visitante", "total") ?? detailRound?.Averages.Away.Total?.Num ?? athleteRound.AwayAverageScore;
            athleteRound.ScoreNum = athleteRound.ScoreNum == 0 && detailRound?.Points is not null ? detailRound.Points.Num : athleteRound.ScoreNum;
            athleteRound.VariationNum = athleteRound.VariationNum == 0 && detailRound?.Price is not null ? detailRound.Price.Variation : athleteRound.VariationNum;
            athleteRound.PriceNum = athleteRound.PriceNum == 0 && detailRound?.Price is not null ? detailRound.Price.Num : athleteRound.PriceNum;
            athleteRound.WasInGame = detailRound?.WasInGame ?? athleteRound.WasInGame;

            if (detailRound is not null && detailRound.Scouts.Count > 0 && (athleteRound.Scouts is null || athleteRound.Scouts.Count == 0))
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

            upserted++;
        }

        return (upserted, athleteDetailsFetched);
    }

    private async Task<Athlete> EnsureAthleteAsync(
        IDictionary<int, Athlete> athletesByCartolaId,
        GatoMestreAthleteDetailResponse athleteDetail,
        CancellationToken cancellationToken)
    {
        if (athletesByCartolaId.TryGetValue(athleteDetail.Athlete.AthleteId, out var athlete))
        {
            athlete.Name = ToRequiredValue(athleteDetail.Athlete.Name, athleteDetail.Athlete.PopularName, 150);
            athlete.Nickname = ToRequiredValue(athleteDetail.Athlete.PopularName, athleteDetail.Athlete.Nickname, 100);
            return athlete;
        }

        athlete = new Athlete(
            0,
            athleteDetail.Athlete.AthleteId,
            ToRequiredValue(athleteDetail.Athlete.Name, athleteDetail.Athlete.PopularName, 150),
            ToRequiredValue(athleteDetail.Athlete.PopularName, athleteDetail.Athlete.Nickname, 100));

        dbContext.Athletes.Add(athlete);
        await dbContext.SaveChangesAsync(cancellationToken);

        athletesByCartolaId.Add(athlete.CartolaId, athlete);
        return athlete;
    }

    private static Club? ResolveClub(
        IReadOnlyDictionary<int, Club> clubs,
        int cartolaClubId,
        int? existingClubId)
    {
        if (cartolaClubId != 0 && clubs.TryGetValue(cartolaClubId, out var club))
        {
            return club;
        }

        return existingClubId.HasValue
            ? clubs.Values.FirstOrDefault(clubEntity => clubEntity.Id == existingClubId.Value)
            : null;
    }

    private static Position? ResolvePosition(
        IReadOnlyDictionary<int, Position> positionsByCartolaId,
        GatoMestrePositionDescriptorResponse? descriptor)
    {
        if (descriptor is null)
        {
            return null;
        }

        return PositionMacroToCartolaId.TryGetValue(descriptor.Macro, out var positionId) &&
               positionsByCartolaId.TryGetValue(positionId, out var position)
            ? position
            : null;
    }

    private static AthleteStatus? ResolveStatus(
        IReadOnlyDictionary<string, AthleteStatus> statusesByName,
        string? statusName)
    {
        if (string.IsNullOrWhiteSpace(statusName))
        {
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
            JsonValueKind.Object when metric.TryGetProperty("num", out var num) && num.TryGetDecimal(out var value) => value,
            JsonValueKind.Number when metric.TryGetDecimal(out var scalar) => scalar,
            JsonValueKind.Null => 0,
            _ => null,
        };
    }

    private static string Normalize(string value)
        => value.Trim().ToUpperInvariant();

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
}
