using CartolaOptimization.Application.Interfaces;
using CartolaOptimization.Application.Models;
using CartolaOptimization.Domain.Entities;
using CartolaOptimization.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CartolaOptimization.Infrastructure.Services;

/// <summary>
/// Generates explainable baseline score projections for athletes in a target round using persisted historical and contextual features.
/// </summary>
public sealed class AthleteProjectionService(AppDbContext dbContext) : IAthleteProjectionService
{
    private const string ModelVersion = "baseline-v1";

    public async Task<AthleteRoundProjectionResult> GetRoundProjectionAsync(
        int round,
        int? positionCartolaId = null,
        int? clubCartolaId = null,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        if (round <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(round), "Round numbers must be greater than zero.");
        }

        take = Math.Clamp(take, 1, 500);

        var latestBaselines = await dbContext.AthleteRounds
            .AsNoTracking()
            .Include(item => item.Athlete)
            .Include(item => item.Club)
            .Include(item => item.Position)
            .Include(item => item.Status)
            .Where(item => item.RoundId < round)
            .ToListAsync(cancellationToken);

        var baselines = latestBaselines
            .GroupBy(item => item.AthleteId)
            .Select(group => group
                .OrderByDescending(item => item.RoundId)
                .ThenByDescending(item => item.Id)
                .First())
            .ToList();

        if (positionCartolaId.HasValue)
        {
            baselines = baselines
                .Where(item => item.Position.CartolaId == positionCartolaId.Value)
                .ToList();
        }

        if (clubCartolaId.HasValue)
        {
            baselines = baselines
                .Where(item => item.Club.CartolaId == clubCartolaId.Value)
                .ToList();
        }

        var matches = await dbContext.Matches
            .AsNoTracking()
            .Include(item => item.HomeClub)
            .Include(item => item.AwayClub)
            .Where(item => item.Round == round && item.ValidForCartola)
            .ToListAsync(cancellationToken);

        var matchByClubId = new Dictionary<int, Match>();
        foreach (var match in matches)
        {
            matchByClubId[match.HomeClubId] = match;
            matchByClubId[match.AwayClubId] = match;
        }

        var clubRounds = await dbContext.ClubRounds
            .AsNoTracking()
            .Include(item => item.Club)
            .Include(item => item.PositionStats)
            .Where(item => item.Round == round)
            .ToListAsync(cancellationToken);
        var clubRoundByClubId = clubRounds.ToDictionary(item => item.ClubId);

        var projections = baselines
            .Select(baseline => BuildProjection(baseline, round, matchByClubId, clubRoundByClubId))
            .OrderByDescending(item => item.ExpectedScore)
            .ThenByDescending(item => item.ConfidenceScore)
            .ThenBy(item => item.AthleteName)
            .Take(take)
            .ToList();

        return new AthleteRoundProjectionResult
        {
            Round = round,
            ModelVersion = ModelVersion,
            GeneratedAtUtc = DateTime.UtcNow,
            TotalAthletesConsidered = baselines.Count,
            TotalAthletesProjected = projections.Count,
            PositionCartolaIdFilter = positionCartolaId,
            ClubCartolaIdFilter = clubCartolaId,
            Athletes = projections,
        };
    }

    private static AthleteProjectedScoreItem BuildProjection(
        AthleteRound baseline,
        int targetRound,
        IReadOnlyDictionary<int, Match> matchByClubId,
        IReadOnlyDictionary<int, ClubRound> clubRoundByClubId)
    {
        matchByClubId.TryGetValue(baseline.ClubId, out var match);

        var isHomeMatch = match is not null
            ? match.HomeClubId == baseline.ClubId
            : (bool?)null;
        var opponentClubId = match is null
            ? (int?)null
            : isHomeMatch == true ? match.AwayClubId : match.HomeClubId;
        var opponentClubName = match is null
            ? string.Empty
            : isHomeMatch == true ? match.AwayClub.Nickname : match.HomeClub.Nickname;

        clubRoundByClubId.TryGetValue(baseline.ClubId, out var clubRound);
        ClubRound? opponentClubRound = null;
        if (opponentClubId.HasValue)
        {
            clubRoundByClubId.TryGetValue(opponentClubId.Value, out opponentClubRound);
        }

        var opponentPositionStats = opponentClubRound?.PositionStats
            .FirstOrDefault(item => item.PositionId == baseline.PositionId);

        var contextAverage = isHomeMatch switch
        {
            true when baseline.HomeAverageScore > 0 => baseline.HomeAverageScore,
            false when baseline.AwayAverageScore > 0 => baseline.AwayAverageScore,
            _ => baseline.AverageScoreNum,
        };

        var baselineComponent = CalculateWeightedAverage(
            (baseline.MovingAverage, 0.45m),
            (baseline.AverageScoreNum, 0.30m),
            (contextAverage, 0.25m));

        var teamContextAdjustment = CalculateTeamContextAdjustment(clubRound);
        var opponentPositionAdjustment = CalculateOpponentPositionAdjustment(opponentPositionStats);
        var priceTrendAdjustment = Clamp(baseline.VariationNum, -3m, 3m) * 0.10m;
        var statusMultiplier = ResolveStatusMultiplier(baseline.Status.CartolaId);

        var projectedScore = Math.Round(
            Clamp((baselineComponent + teamContextAdjustment + opponentPositionAdjustment + priceTrendAdjustment) * statusMultiplier, -10m, 30m),
            2,
            MidpointRounding.AwayFromZero);

        var confidenceScore = CalculateConfidenceScore(
            baseline,
            match,
            clubRound,
            opponentPositionStats,
            statusMultiplier);

        var notes = BuildProjectionNotes(match, clubRound, opponentPositionStats, baseline.Status.CartolaId, targetRound);

        return new AthleteProjectedScoreItem
        {
            AthleteCartolaId = baseline.Athlete.CartolaId,
            AthleteName = baseline.Athlete.Name,
            AthleteNickname = baseline.Athlete.Nickname,
            ClubCartolaId = baseline.Club.CartolaId,
            ClubName = baseline.Club.Nickname,
            PositionCartolaId = baseline.Position.CartolaId,
            PositionName = baseline.Position.Name,
            StatusCartolaId = baseline.Status.CartolaId,
            StatusName = baseline.Status.Name,
            BaselineRound = baseline.RoundId,
            OpponentClubCartolaId = opponentClubId is null
                ? null
                : match is null
                    ? null
                    : isHomeMatch == true ? match.AwayClub.CartolaId : match.HomeClub.CartolaId,
            OpponentClubName = opponentClubName,
            IsHomeMatch = isHomeMatch,
            ExpectedScore = projectedScore,
            ConfidenceScore = confidenceScore,
            BaselineComponent = Math.Round(baselineComponent, 2, MidpointRounding.AwayFromZero),
            TeamContextAdjustment = Math.Round(teamContextAdjustment, 2, MidpointRounding.AwayFromZero),
            OpponentPositionAdjustment = Math.Round(opponentPositionAdjustment, 2, MidpointRounding.AwayFromZero),
            PriceTrendAdjustment = Math.Round(priceTrendAdjustment, 2, MidpointRounding.AwayFromZero),
            StatusMultiplier = statusMultiplier,
            MovingAverage = baseline.MovingAverage,
            AverageScore = baseline.AverageScoreNum,
            ContextAverage = contextAverage,
            ClubExpectedPoints = clubRound?.ExpectedPoints ?? 0m,
            ClubWinProbability = clubRound?.WinProbability ?? 0m,
            ClubLoseProbability = clubRound?.LoseProbability ?? 0m,
            OpponentPointsConcededAverage = opponentPositionStats?.PointsConcededAverage ?? 0m,
            ProjectionNotes = notes,
        };
    }

    private static decimal CalculateWeightedAverage(params (decimal Value, decimal Weight)[] inputs)
    {
        var filtered = inputs.Where(item => item.Value > 0m && item.Weight > 0m).ToList();
        if (filtered.Count == 0)
        {
            return 0m;
        }

        var numerator = filtered.Sum(item => item.Value * item.Weight);
        var denominator = filtered.Sum(item => item.Weight);

        return denominator == 0m ? 0m : numerator / denominator;
    }

    private static decimal CalculateTeamContextAdjustment(ClubRound? clubRound)
    {
        if (clubRound is null)
        {
            return 0m;
        }

        var winLoseBalance = ((clubRound.WinProbability - clubRound.LoseProbability) / 100m) * 1.25m;
        var expectedPointsAdjustment = ((clubRound.ExpectedPoints - 50m) / 50m) * 0.75m;

        return Clamp(winLoseBalance + expectedPointsAdjustment, -2.5m, 2.5m);
    }

    private static decimal CalculateOpponentPositionAdjustment(ClubRoundPosition? opponentPositionStats)
    {
        if (opponentPositionStats is null)
        {
            return 0m;
        }

        return Clamp((opponentPositionStats.PointsConcededAverage - 4m) * 0.35m, -2m, 2m);
    }

    private static decimal ResolveStatusMultiplier(int statusCartolaId)
    {
        return statusCartolaId switch
        {
            7 => 1.00m,
            2 => 0.70m,
            6 => 0.40m,
            3 or 5 => 0.15m,
            _ => 0.85m,
        };
    }

    private static decimal CalculateConfidenceScore(
        AthleteRound baseline,
        Match? match,
        ClubRound? clubRound,
        ClubRoundPosition? opponentPositionStats,
        decimal statusMultiplier)
    {
        var confidence = 35m;

        confidence += baseline.GamesNum switch
        {
            >= 5 => 20m,
            >= 3 => 10m,
            _ => 0m,
        };

        if (match is not null)
        {
            confidence += 15m;
        }

        if (clubRound is not null)
        {
            confidence += 15m;
        }

        if (opponentPositionStats is not null)
        {
            confidence += 10m;
        }

        if (baseline.HomeAverageScore > 0m || baseline.AwayAverageScore > 0m)
        {
            confidence += 5m;
        }

        if (statusMultiplier < 1m)
        {
            confidence -= 15m;
        }

        return Math.Round(Clamp(confidence, 10m, 95m), 2, MidpointRounding.AwayFromZero);
    }

    private static string BuildProjectionNotes(
        Match? match,
        ClubRound? clubRound,
        ClubRoundPosition? opponentPositionStats,
        int statusCartolaId,
        int targetRound)
    {
        var notes = new List<string>
        {
            $"Projection uses the latest persisted athlete baseline before round {targetRound}.",
        };

        if (match is null)
        {
            notes.Add("No valid Cartola match was found for the target round, so venue and opponent context were not applied.");
        }

        if (clubRound is null)
        {
            notes.Add("Club-level Gato Mestre round context is missing for the target round.");
        }

        if (opponentPositionStats is null)
        {
            notes.Add("Opponent position-conceded context is missing for the athlete position.");
        }

        if (statusCartolaId != 7)
        {
            notes.Add("Athlete availability status reduced the projection confidence and expected score.");
        }

        return string.Join(" ", notes);
    }

    private static decimal Clamp(decimal value, decimal min, decimal max)
        => value < min ? min : value > max ? max : value;
}
