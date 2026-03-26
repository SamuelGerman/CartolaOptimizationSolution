using System.Text.Json;
using CartolaOptimization.Application.Interfaces;
using CartolaOptimization.Application.Models;
using CartolaOptimization.Infrastructure.Integrations.Cartola;
using CartolaOptimization.Infrastructure.Integrations.Cartola.Models;

namespace CartolaOptimization.Infrastructure.Services;

public sealed class CartolaLineupSubmissionService(
    ICartolaApiClient cartolaApiClient,
    IAthleteScorePredictionService athleteScorePredictionService,
    IAthleteContextAthleteScorePredictionService athleteContextAthleteScorePredictionService,
    IFullContextAthleteScorePredictionService fullContextAthleteScorePredictionService) : ICartolaLineupSubmissionService
{
    private const int ProbableStatusCartolaId = 7;
    private const int MaxPredictionCandidates = 5000;
    private const int CoachPositionCartolaId = 6;

    private static readonly IReadOnlyDictionary<string, int> SchemeIdsByFormationKey = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        [BuildFormationKey(CreateFormation(goalkeeperQuantity: 1, lateralQuantity: 0, defenderQuantity: 3, midfielderQuantity: 4, strikerQuantity: 3, coachQuantity: 1))] = 1,
        [BuildFormationKey(CreateFormation(goalkeeperQuantity: 1, lateralQuantity: 0, defenderQuantity: 3, midfielderQuantity: 5, strikerQuantity: 2, coachQuantity: 1))] = 2,
        [BuildFormationKey(CreateFormation(goalkeeperQuantity: 1, lateralQuantity: 2, defenderQuantity: 2, midfielderQuantity: 3, strikerQuantity: 3, coachQuantity: 1))] = 3,
        [BuildFormationKey(CreateFormation(goalkeeperQuantity: 1, lateralQuantity: 2, defenderQuantity: 2, midfielderQuantity: 4, strikerQuantity: 2, coachQuantity: 1))] = 4,
        [BuildFormationKey(CreateFormation(goalkeeperQuantity: 1, lateralQuantity: 2, defenderQuantity: 2, midfielderQuantity: 5, strikerQuantity: 1, coachQuantity: 1))] = 5,
        [BuildFormationKey(CreateFormation(goalkeeperQuantity: 1, lateralQuantity: 2, defenderQuantity: 3, midfielderQuantity: 3, strikerQuantity: 2, coachQuantity: 1))] = 6,
        [BuildFormationKey(CreateFormation(goalkeeperQuantity: 1, lateralQuantity: 2, defenderQuantity: 3, midfielderQuantity: 4, strikerQuantity: 1, coachQuantity: 1))] = 7,
    };

    public async Task<CartolaLineupSubmissionResult> SubmitAsync(
        int round,
        PredictedLineupResult lineup,
        PredictedLineupRequest request,
        string? cartolaAuthorization,
        string? cartolaGlbTag = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lineup);
        ArgumentNullException.ThrowIfNull(request);

        var submissionRequest = request.CartolaSubmission;
        if (submissionRequest is null || !submissionRequest.SaveToCartola)
        {
            throw new InvalidOperationException("Cartola submission was not requested for this lineup.");
        }

        if (string.IsNullOrWhiteSpace(cartolaAuthorization))
        {
            throw new ArgumentException(
                "Saving the optimized lineup to Cartola requires the 'X-Cartola-Authorization' header with a valid Bearer token.",
                nameof(cartolaAuthorization));
        }

        if (!lineup.Athletes.Any())
        {
            throw new InvalidOperationException("The optimized lineup is empty and cannot be saved to Cartola.");
        }

        var schemeId = ResolveSchemeId(lineup.Formation);
        var captainAthlete = ResolveCaptain(lineup, submissionRequest.CaptainAthleteCartolaId);
        var resolvedReserveSelections = await ResolveReserveSelectionsAsync(round, lineup, submissionRequest, cancellationToken);
        var reserveSelections = resolvedReserveSelections.Select(item => item.Selection).ToArray();
        var reserveLuxuryAthleteCartolaId = ResolveReserveLuxuryAthleteCartolaId(
            resolvedReserveSelections,
            submissionRequest.ReserveLuxuryAthleteCartolaId);

        var cartolaRequest = new CartolaSaveTeamRequest
        {
            Atletas = lineup.Athletes.Select(item => item.AthleteCartolaId).ToArray(),
            Capitao = captainAthlete.AthleteCartolaId,
            Esquema = schemeId,
            Reservas = reserveSelections.Length == 0
                ? null
                : reserveSelections.ToDictionary(item => item.PositionCartolaId, item => item.AthleteCartolaId),
            ReservaLuxoId = reserveLuxuryAthleteCartolaId,
        };

        var cartolaResponse = await cartolaApiClient.SaveTeamAsync(
            cartolaRequest,
            cartolaAuthorization,
            cartolaGlbTag,
            cancellationToken);

        return new CartolaLineupSubmissionResult
        {
            Submitted = true,
            SubmittedAtUtc = DateTime.UtcNow,
            SchemeId = schemeId,
            CaptainAthleteCartolaId = captainAthlete.AthleteCartolaId,
            ReserveLuxuryAthleteCartolaId = reserveLuxuryAthleteCartolaId,
            Message = "The optimized lineup was successfully sent to Cartola.",
            ReserveSelections = reserveSelections,
            CartolaResponse = cartolaResponse.ValueKind == JsonValueKind.Undefined ? null : cartolaResponse,
        };
    }

    private async Task<IReadOnlyCollection<ResolvedReserveSelection>> ResolveReserveSelectionsAsync(
        int round,
        PredictedLineupResult lineup,
        CartolaLineupSubmissionRequest submissionRequest,
        CancellationToken cancellationToken)
    {
        var reserveOverrides = NormalizeReserveOverrides(submissionRequest.ReserveOverrides);
        if (!submissionRequest.IncludeReserves && reserveOverrides.Count == 0 && !submissionRequest.ReserveLuxuryAthleteCartolaId.HasValue)
        {
            return [];
        }

        if (!submissionRequest.IncludeReserves && (reserveOverrides.Count > 0 || submissionRequest.ReserveLuxuryAthleteCartolaId.HasValue))
        {
            throw new ArgumentException(
                "Reserve overrides and reserve luxury selection require 'IncludeReserves' to be true.",
                nameof(submissionRequest));
        }

        var predictionPool = await GetPredictionPoolAsync(round, lineup.DatasetKey, cancellationToken);
        var lineupAthleteIds = lineup.Athletes.Select(item => item.AthleteCartolaId).ToHashSet();
        var selectedReserveIds = new HashSet<int>();
        var selections = new List<ResolvedReserveSelection>();

        foreach (var positionCartolaId in new[] { 1, 2, 3, 4, 5 })
        {
            var overrideAthleteId = reserveOverrides.TryGetValue(positionCartolaId, out var athleteCartolaId)
                ? athleteCartolaId
                : (int?)null;

            AthleteScorePredictionItem reserveAthlete;
            if (overrideAthleteId.HasValue)
            {
                reserveAthlete = predictionPool.Athletes.FirstOrDefault(item =>
                        item.PositionCartolaId == positionCartolaId &&
                        item.AthleteCartolaId == overrideAthleteId.Value)
                    ?? throw new InvalidOperationException(
                        $"The requested reserve athlete '{overrideAthleteId.Value}' for position '{positionCartolaId}' is not available among the current probable candidates.");
            }
            else
            {
                reserveAthlete = predictionPool.Athletes
                    .Where(item => item.PositionCartolaId == positionCartolaId)
                    .OrderByDescending(item => item.PredictedScore)
                    .ThenBy(item => item.PriceNum)
                    .FirstOrDefault(item => !lineupAthleteIds.Contains(item.AthleteCartolaId) && !selectedReserveIds.Contains(item.AthleteCartolaId))
                    ?? throw new InvalidOperationException(
                        $"No probable reserve candidate is available for Cartola position '{positionCartolaId}' in round {round}.");
            }

            if (!selectedReserveIds.Add(reserveAthlete.AthleteCartolaId) || lineupAthleteIds.Contains(reserveAthlete.AthleteCartolaId))
            {
                throw new InvalidOperationException(
                    $"The reserve athlete '{reserveAthlete.AthleteCartolaId}' was selected more than once or already belongs to the starting lineup.");
            }

            selections.Add(new ResolvedReserveSelection(
                new CartolaReserveSelection
                {
                    PositionCartolaId = positionCartolaId,
                    AthleteCartolaId = reserveAthlete.AthleteCartolaId,
                },
                reserveAthlete));
        }

        return selections
            .OrderBy(item => item.Selection.PositionCartolaId)
            .ToArray();
    }

    private async Task<AthleteScorePredictionResult> GetPredictionPoolAsync(
        int round,
        string datasetKey,
        CancellationToken cancellationToken)
    {
        return datasetKey switch
        {
            "athlete-only" => await athleteScorePredictionService.GetPredictedScoresAsync(
                round,
                ProbableStatusCartolaId,
                null,
                null,
                MaxPredictionCandidates,
                cancellationToken),
            "athlete-context" => await athleteContextAthleteScorePredictionService.GetPredictedScoresAsync(
                round,
                ProbableStatusCartolaId,
                null,
                null,
                MaxPredictionCandidates,
                cancellationToken),
            "full-context" => await fullContextAthleteScorePredictionService.GetPredictedScoresAsync(
                round,
                ProbableStatusCartolaId,
                null,
                null,
                MaxPredictionCandidates,
                cancellationToken),
            _ => throw new InvalidOperationException($"The lineup dataset '{datasetKey}' is not supported for Cartola submission."),
        };
    }

    private static PredictedLineupAthleteItem ResolveCaptain(
        PredictedLineupResult lineup,
        int? captainAthleteCartolaId)
    {
        if (captainAthleteCartolaId.HasValue)
        {
            var captain = lineup.Athletes.FirstOrDefault(item => item.AthleteCartolaId == captainAthleteCartolaId.Value)
                ?? throw new InvalidOperationException(
                    $"The requested captain '{captainAthleteCartolaId.Value}' is not part of the optimized lineup.");

            if (captain.PositionCartolaId == CoachPositionCartolaId)
            {
                throw new InvalidOperationException("A coach cannot be chosen as the Cartola captain.");
            }

            return captain;
        }

        return lineup.Athletes
            .Where(item => item.PositionCartolaId != CoachPositionCartolaId)
            .OrderByDescending(item => item.PredictedScore)
            .ThenBy(item => item.PriceNum)
            .FirstOrDefault()
            ?? throw new InvalidOperationException("The optimized lineup does not contain a valid captain candidate.");
    }

    private static int ResolveSchemeId(IReadOnlyCollection<PredictedLineupPositionRequirement> formation)
    {
        var formationKey = BuildFormationKey(formation);
        if (SchemeIdsByFormationKey.TryGetValue(formationKey, out var schemeId))
        {
            return schemeId;
        }

        throw new InvalidOperationException(
            "Only official Cartola formations can be submitted to the Cartola team endpoint.");
    }

    private static int? ResolveReserveLuxuryAthleteCartolaId(
        IReadOnlyCollection<ResolvedReserveSelection> reserveSelections,
        int? reserveLuxuryAthleteCartolaId)
    {
        if (reserveSelections.Count == 0)
        {
            return null;
        }

        if (reserveLuxuryAthleteCartolaId.HasValue)
        {
            var isValidReserve = reserveSelections.Any(item => item.Selection.AthleteCartolaId == reserveLuxuryAthleteCartolaId.Value);
            if (!isValidReserve)
            {
                throw new InvalidOperationException(
                    $"The requested luxury reserve '{reserveLuxuryAthleteCartolaId.Value}' is not part of the selected reserve bench.");
            }

            return reserveLuxuryAthleteCartolaId.Value;
        }

        return reserveSelections
            .OrderByDescending(item => item.Athlete.PredictedScore)
            .ThenBy(item => item.Athlete.PriceNum)
            .Select(item => item.Selection.AthleteCartolaId)
            .First();
    }

    private static IReadOnlyDictionary<int, int> NormalizeReserveOverrides(
        IReadOnlyCollection<CartolaReserveSelection>? reserveOverrides)
    {
        if (reserveOverrides is null || reserveOverrides.Count == 0)
        {
            return new Dictionary<int, int>();
        }

        return reserveOverrides
            .GroupBy(item => item.PositionCartolaId)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    if (group.Key is < 1 or > 5)
                    {
                        throw new ArgumentException(
                            $"Reserve overrides only support Cartola field positions 1..5. Position '{group.Key}' is invalid.",
                            nameof(reserveOverrides));
                    }

                    var overrides = group.Select(item => item.AthleteCartolaId).Distinct().ToArray();
                    if (overrides.Length != 1)
                    {
                        throw new ArgumentException(
                            $"Position '{group.Key}' must provide exactly one reserve override athlete.",
                            nameof(reserveOverrides));
                    }

                    return overrides[0];
                });
    }

    private static IReadOnlyCollection<PredictedLineupPositionRequirement> CreateFormation(
        int goalkeeperQuantity,
        int lateralQuantity,
        int defenderQuantity,
        int midfielderQuantity,
        int strikerQuantity,
        int coachQuantity)
        => new[]
        {
            new PredictedLineupPositionRequirement { PositionCartolaId = 1, Quantity = goalkeeperQuantity },
            new PredictedLineupPositionRequirement { PositionCartolaId = 2, Quantity = lateralQuantity },
            new PredictedLineupPositionRequirement { PositionCartolaId = 3, Quantity = defenderQuantity },
            new PredictedLineupPositionRequirement { PositionCartolaId = 4, Quantity = midfielderQuantity },
            new PredictedLineupPositionRequirement { PositionCartolaId = 5, Quantity = strikerQuantity },
            new PredictedLineupPositionRequirement { PositionCartolaId = 6, Quantity = coachQuantity },
        };

    private static string BuildFormationKey(IReadOnlyCollection<PredictedLineupPositionRequirement> formation)
        => string.Join(
            "|",
            formation
                .OrderBy(item => item.PositionCartolaId)
                .Select(item => $"{item.PositionCartolaId}:{item.Quantity}"));

    private sealed record ResolvedReserveSelection(
        CartolaReserveSelection Selection,
        AthleteScorePredictionItem Athlete);
}
