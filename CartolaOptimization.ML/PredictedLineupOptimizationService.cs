using CartolaOptimization.Application.Interfaces;
using CartolaOptimization.Application.Models;

namespace CartolaOptimization.ML;

public sealed class PredictedLineupOptimizationService(
    IAthleteScorePredictionService athleteScorePredictionService,
    IAthleteContextAthleteScorePredictionService athleteContextAthleteScorePredictionService,
    IFullContextAthleteScorePredictionService fullContextAthleteScorePredictionService) : IPredictedLineupOptimizationService
{
    private const int ProbableStatusCartolaId = 7;
    private const string ProbableStatusName = "Provavel";
    private const int MaxPredictionCandidates = 5000;

    private static readonly IReadOnlyList<IReadOnlyList<PredictedLineupPositionRequirement>> SupportedCartolaFormations =
    [
        CreateFormation(goalkeeperQuantity: 1, lateralQuantity: 0, defenderQuantity: 3, midfielderQuantity: 4, strikerQuantity: 3, coachQuantity: 1),
        CreateFormation(goalkeeperQuantity: 1, lateralQuantity: 0, defenderQuantity: 3, midfielderQuantity: 5, strikerQuantity: 2, coachQuantity: 1),
        CreateFormation(goalkeeperQuantity: 1, lateralQuantity: 2, defenderQuantity: 2, midfielderQuantity: 3, strikerQuantity: 3, coachQuantity: 1),
        CreateFormation(goalkeeperQuantity: 1, lateralQuantity: 2, defenderQuantity: 2, midfielderQuantity: 4, strikerQuantity: 2, coachQuantity: 1),
        CreateFormation(goalkeeperQuantity: 1, lateralQuantity: 2, defenderQuantity: 2, midfielderQuantity: 5, strikerQuantity: 1, coachQuantity: 1),
        CreateFormation(goalkeeperQuantity: 1, lateralQuantity: 2, defenderQuantity: 3, midfielderQuantity: 3, strikerQuantity: 2, coachQuantity: 1),
        CreateFormation(goalkeeperQuantity: 1, lateralQuantity: 2, defenderQuantity: 3, midfielderQuantity: 4, strikerQuantity: 1, coachQuantity: 1),
    ];

    private static readonly IReadOnlyDictionary<int, string> KnownPositionNames = new Dictionary<int, string>
    {
        [1] = "Goleiro",
        [2] = "Lateral",
        [3] = "Zagueiro",
        [4] = "Meia",
        [5] = "Atacante",
        [6] = "Tecnico",
    };

    public async Task<PredictedLineupResult> GetOptimalLineupAsync(
        int round,
        PredictedLineupRequest request,
        CancellationToken cancellationToken = default)
        => await GetOptimalLineupCoreAsync(
            round,
            request,
            athleteScorePredictionService.GetPredictedScoresAsync,
            cancellationToken);

    public async Task<PredictedLineupResult> GetOptimalAthleteContextLineupAsync(
        int round,
        PredictedLineupRequest request,
        CancellationToken cancellationToken = default)
        => await GetOptimalLineupCoreAsync(
            round,
            request,
            athleteContextAthleteScorePredictionService.GetPredictedScoresAsync,
            cancellationToken);

    public async Task<PredictedLineupResult> GetOptimalFullContextLineupAsync(
        int round,
        PredictedLineupRequest request,
        CancellationToken cancellationToken = default)
        => await GetOptimalLineupCoreAsync(
            round,
            request,
            fullContextAthleteScorePredictionService.GetPredictedScoresAsync,
            cancellationToken);

    private static async Task<PredictedLineupResult> GetOptimalLineupCoreAsync(
        int round,
        PredictedLineupRequest request,
        Func<int, int?, int?, int?, int, CancellationToken, Task<AthleteScorePredictionResult>> predictionFunc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (round <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(round), "Round numbers must be greater than zero.");
        }

        if (request.Budget <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(request.Budget), "Budget must be greater than zero.");
        }

        var autoSelectFormation = request.Formation is null || request.Formation.Count == 0;
        var requestedFormations = ResolveRequestedFormations(request.Formation);
        var budgetInCents = ToCents(request.Budget);

        var predictions = await predictionFunc(
            round,
            ProbableStatusCartolaId,
            null,
            null,
            MaxPredictionCandidates,
            cancellationToken);

        var candidatesByPosition = predictions.Athletes
            .GroupBy(item => item.PositionCartolaId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(item => item.PredictedScore)
                    .ThenBy(item => item.PriceNum)
                    .ThenBy(item => item.AthleteName)
                    .ToList());

        var positionOptionCache = new Dictionary<(int PositionCartolaId, int Quantity), IReadOnlyList<PositionBudgetOption>>();
        FormationOptimizationResult? optimalResult = null;

        foreach (var formation in requestedFormations)
        {
            var validationFailure = ValidateFormationAvailability(formation, candidatesByPosition, round);
            if (validationFailure is not null)
            {
                if (!autoSelectFormation)
                {
                    throw new InvalidOperationException(validationFailure);
                }

                continue;
            }

            var positionOptions = new List<IReadOnlyList<PositionBudgetOption>>(formation.Count);
            foreach (var requirement in formation)
            {
                var cacheKey = (requirement.PositionCartolaId, requirement.Quantity);
                if (!positionOptionCache.TryGetValue(cacheKey, out var options))
                {
                    options = BuildPositionOptions(
                        requirement,
                        candidatesByPosition[requirement.PositionCartolaId],
                        budgetInCents);
                    positionOptionCache[cacheKey] = options;
                }

                positionOptions.Add(options);
            }

            var optimalLineup = CombinePositionOptions(positionOptions, budgetInCents);
            if (optimalLineup is null)
            {
                if (!autoSelectFormation)
                {
                    throw new InvalidOperationException(
                        $"No feasible lineup was found within budget {request.Budget:F2} for the requested formation. Increase the budget or relax the formation constraints.");
                }

                continue;
            }

            var candidate = new FormationOptimizationResult(formation, optimalLineup);
            if (optimalResult is null ||
                candidate.TotalScore > optimalResult.TotalScore ||
                (candidate.TotalScore == optimalResult.TotalScore && candidate.TotalCostInCents < optimalResult.TotalCostInCents))
            {
                optimalResult = candidate;
            }
        }

        if (optimalResult is null)
        {
            throw new InvalidOperationException(
                autoSelectFormation
                    ? $"No feasible lineup was found within budget {request.Budget:F2} for any supported Cartola formation. Increase the budget or provide a custom formation."
                    : $"No feasible lineup was found within budget {request.Budget:F2} for the requested formation. Increase the budget or relax the formation constraints.");
        }

        var selectedAthletes = ReconstructSelectedAthletes(optimalResult.Lineup)
            .OrderBy(item => item.PositionCartolaId)
            .ThenByDescending(item => item.PredictedScore)
            .ThenBy(item => item.AthleteName)
            .Select(item => new PredictedLineupAthleteItem
            {
                AthleteCartolaId = item.AthleteCartolaId,
                AthleteName = item.AthleteName,
                AthleteNickname = item.AthleteNickname,
                ClubCartolaId = item.ClubCartolaId,
                ClubName = item.ClubName,
                PositionCartolaId = item.PositionCartolaId,
                PositionName = item.PositionName,
                StatusCartolaId = item.StatusCartolaId,
                StatusName = item.StatusName,
                BaselineRound = item.BaselineRound,
                BaselineDataPhase = item.BaselineDataPhase,
                PriceNum = item.PriceNum,
                PredictedScore = item.PredictedScore,
                PredictionConfidence = item.PredictionConfidence,
                PredictionNotes = item.PredictionNotes,
            })
            .ToList();

        var totalCost = FromCents(optimalResult.TotalCostInCents);
        var usesFallbackBaselines = selectedAthletes.Any(item =>
            item.PredictionNotes.Contains("latest persisted athlete baseline", StringComparison.OrdinalIgnoreCase));

        return new PredictedLineupResult
        {
            Round = round,
            DatasetKey = predictions.DatasetKey,
            ModelVersion = predictions.ModelVersion,
            GeneratedAtUtc = DateTime.UtcNow,
            ModelTrainedAtUtc = predictions.ModelTrainedAtUtc,
            Budget = request.Budget,
            TotalCost = totalCost,
            RemainingBudget = request.Budget - totalCost,
            TotalPredictedScore = Math.Round(optimalResult.TotalScore, 2, MidpointRounding.AwayFromZero),
            TotalPlayers = selectedAthletes.Count,
            TotalProbableAthletesEvaluated = predictions.TotalAthletesConsidered,
            ProbableStatusCartolaId = ProbableStatusCartolaId,
            ProbableStatusName = ProbableStatusName,
            ValidationMeanAbsoluteError = predictions.ValidationMeanAbsoluteError,
            ValidationRootMeanSquaredError = predictions.ValidationRootMeanSquaredError,
            OptimizationNotes = BuildOptimizationNotes(round, usesFallbackBaselines, autoSelectFormation, optimalResult.Formation),
            Formation = optimalResult.Formation,
            Athletes = selectedAthletes,
        };
    }

    private static IReadOnlyList<PredictedLineupPositionRequirement> NormalizeFormation(
        IReadOnlyCollection<PredictedLineupPositionRequirement>? formation)
    {
        if (formation is null || formation.Count == 0)
        {
            throw new ArgumentException("At least one position quantity must be informed in the formation request.", nameof(formation));
        }

        var normalized = formation
            .GroupBy(item => item.PositionCartolaId)
            .Select(group =>
            {
                if (group.Key <= 0)
                {
                    throw new ArgumentException("PositionCartolaId values must be greater than zero.", nameof(formation));
                }

                var quantity = group.Sum(item => item.Quantity);
                if (quantity <= 0)
                {
                    throw new ArgumentException($"Position '{group.Key}' must request at least one athlete.", nameof(formation));
                }

                return new PredictedLineupPositionRequirement
                {
                    PositionCartolaId = group.Key,
                    Quantity = quantity,
                };
            })
            .OrderBy(item => item.PositionCartolaId)
            .ToList();

        if (normalized.Count == 0)
        {
            throw new ArgumentException("At least one valid position quantity must be informed in the formation request.", nameof(formation));
        }

        return normalized;
    }

    private static IReadOnlyList<IReadOnlyList<PredictedLineupPositionRequirement>> ResolveRequestedFormations(
        IReadOnlyCollection<PredictedLineupPositionRequirement>? formation)
    {
        if (formation is null || formation.Count == 0)
        {
            return SupportedCartolaFormations;
        }

        return [NormalizeFormation(formation)];
    }

    private static string? ValidateFormationAvailability(
        IReadOnlyList<PredictedLineupPositionRequirement> formation,
        IReadOnlyDictionary<int, List<AthleteScorePredictionItem>> candidatesByPosition,
        int round)
    {
        foreach (var requirement in formation)
        {
            var availableCandidates = candidatesByPosition.TryGetValue(requirement.PositionCartolaId, out var candidates)
                ? candidates.Count
                : 0;

            if (availableCandidates < requirement.Quantity)
            {
                var positionName = ResolvePositionName(requirement.PositionCartolaId, candidatesByPosition);
                return $"The requested formation needs {requirement.Quantity} athlete(s) for position '{positionName}', but only {availableCandidates} provavel athlete(s) were available for round {round}.";
            }
        }

        return null;
    }

    private static string BuildOptimizationNotes(
        int round,
        bool usesFallbackBaselines,
        bool autoSelectFormation,
        IReadOnlyList<PredictedLineupPositionRequirement> formation)
    {
        var formationDescription = DescribeFormation(formation);
        var baselineNotes = usesFallbackBaselines
            ? $"used the latest persisted baseline before round {round} because current pre-round rows were not available for all candidates"
            : "used current pre-round baselines for the target round";

        if (autoSelectFormation)
        {
            return $"Optimization considered only provavel athletes, automatically selected the supported Cartola formation {formationDescription}, and {baselineNotes}.";
        }

        return $"Optimization considered only provavel athletes, respected the requested formation {formationDescription}, and {baselineNotes}.";
    }

    private static string DescribeFormation(IReadOnlyList<PredictedLineupPositionRequirement> formation)
    {
        var laterals = formation.FirstOrDefault(item => item.PositionCartolaId == 2)?.Quantity ?? 0;
        var defenders = formation.FirstOrDefault(item => item.PositionCartolaId == 3)?.Quantity ?? 0;
        var midfielders = formation.FirstOrDefault(item => item.PositionCartolaId == 4)?.Quantity ?? 0;
        var strikers = formation.FirstOrDefault(item => item.PositionCartolaId == 5)?.Quantity ?? 0;

        return $"{laterals + defenders}-{midfielders}-{strikers}";
    }

    private static IReadOnlyList<PredictedLineupPositionRequirement> CreateFormation(
        int goalkeeperQuantity,
        int lateralQuantity,
        int defenderQuantity,
        int midfielderQuantity,
        int strikerQuantity,
        int coachQuantity)
        => new List<PredictedLineupPositionRequirement>
        {
            new() { PositionCartolaId = 1, Quantity = goalkeeperQuantity },
            new() { PositionCartolaId = 2, Quantity = lateralQuantity },
            new() { PositionCartolaId = 3, Quantity = defenderQuantity },
            new() { PositionCartolaId = 4, Quantity = midfielderQuantity },
            new() { PositionCartolaId = 5, Quantity = strikerQuantity },
            new() { PositionCartolaId = 6, Quantity = coachQuantity },
        }
        .Where(item => item.Quantity > 0)
        .ToList();

    private static IReadOnlyList<PositionBudgetOption> BuildPositionOptions(
        PredictedLineupPositionRequirement requirement,
        IReadOnlyList<AthleteScorePredictionItem> candidates,
        int budgetInCents)
    {
        var states = new PositionState?[requirement.Quantity + 1, budgetInCents + 1];
        states[0, 0] = PositionState.Empty;

        foreach (var candidate in candidates)
        {
            var costInCents = ToCents(candidate.PriceNum);
            if (costInCents > budgetInCents)
            {
                continue;
            }

            for (var quantity = requirement.Quantity; quantity >= 1; quantity--)
            {
                for (var budget = budgetInCents; budget >= costInCents; budget--)
                {
                    var previous = states[quantity - 1, budget - costInCents];
                    if (previous is null)
                    {
                        continue;
                    }

                    var candidateState = new PositionState(
                        previous.TotalScore + candidate.PredictedScore,
                        previous,
                        candidate);

                    var current = states[quantity, budget];
                    if (current is null || candidateState.TotalScore > current.TotalScore)
                    {
                        states[quantity, budget] = candidateState;
                    }
                }
            }
        }

        var options = new List<PositionBudgetOption>();
        var bestScoreSoFar = decimal.MinValue;

        for (var budget = 0; budget <= budgetInCents; budget++)
        {
            var state = states[requirement.Quantity, budget];
            if (state is null)
            {
                continue;
            }

            if (state.TotalScore <= bestScoreSoFar)
            {
                continue;
            }

            options.Add(new PositionBudgetOption(
                budget,
                state.TotalScore,
                ReconstructPositionSelection(state)));
            bestScoreSoFar = state.TotalScore;
        }

        return options;
    }

    private static CombinedState? CombinePositionOptions(
        IReadOnlyList<IReadOnlyList<PositionBudgetOption>> positionOptions,
        int budgetInCents)
    {
        CombinedState?[] states = new CombinedState[budgetInCents + 1];
        states[0] = CombinedState.Empty;

        foreach (var options in positionOptions)
        {
            var nextStates = new CombinedState?[budgetInCents + 1];

            for (var currentBudget = 0; currentBudget <= budgetInCents; currentBudget++)
            {
                var current = states[currentBudget];
                if (current is null)
                {
                    continue;
                }

                foreach (var option in options)
                {
                    var nextBudget = currentBudget + option.TotalCostInCents;
                    if (nextBudget > budgetInCents)
                    {
                        break;
                    }

                    var candidate = new CombinedState(
                        nextBudget,
                        current.TotalScore + option.TotalScore,
                        current,
                        option);

                    var existing = nextStates[nextBudget];
                    if (existing is null || candidate.TotalScore > existing.TotalScore)
                    {
                        nextStates[nextBudget] = candidate;
                    }
                }
            }

            states = nextStates;
        }

        return states
            .Where(item => item is not null)
            .OrderByDescending(item => item!.TotalScore)
            .ThenBy(item => item!.TotalCostInCents)
            .FirstOrDefault();
    }

    private static IReadOnlyList<AthleteScorePredictionItem> ReconstructPositionSelection(PositionState state)
    {
        var athletes = new List<AthleteScorePredictionItem>();
        var current = state;

        while (current.Athlete is not null)
        {
            athletes.Add(current.Athlete);
            current = current.Previous!;
        }

        athletes.Reverse();
        return athletes;
    }

    private static IReadOnlyList<AthleteScorePredictionItem> ReconstructSelectedAthletes(CombinedState state)
    {
        var athletes = new List<AthleteScorePredictionItem>();
        var current = state;

        while (current.Option is not null)
        {
            athletes.AddRange(current.Option.Athletes);
            current = current.Previous!;
        }

        return athletes;
    }

    private static string ResolvePositionName(
        int positionCartolaId,
        IReadOnlyDictionary<int, List<AthleteScorePredictionItem>> candidatesByPosition)
    {
        if (candidatesByPosition.TryGetValue(positionCartolaId, out var candidates) && candidates.Count > 0)
        {
            return candidates[0].PositionName;
        }

        return KnownPositionNames.TryGetValue(positionCartolaId, out var positionName)
            ? positionName
            : $"Position {positionCartolaId}";
    }

    private static int ToCents(decimal value)
        => (int)Math.Round(value * 100m, MidpointRounding.AwayFromZero);

    private static decimal FromCents(int value)
        => Math.Round(value / 100m, 2, MidpointRounding.AwayFromZero);

    private sealed class PositionState(decimal totalScore, PositionState? previous, AthleteScorePredictionItem? athlete)
    {
        public static PositionState Empty { get; } = new(0m, null, null);

        public decimal TotalScore { get; } = totalScore;

        public PositionState? Previous { get; } = previous;

        public AthleteScorePredictionItem? Athlete { get; } = athlete;
    }

    private sealed class PositionBudgetOption(int totalCostInCents, decimal totalScore, IReadOnlyList<AthleteScorePredictionItem> athletes)
    {
        public int TotalCostInCents { get; } = totalCostInCents;

        public decimal TotalScore { get; } = totalScore;

        public IReadOnlyList<AthleteScorePredictionItem> Athletes { get; } = athletes;
    }

    private sealed class CombinedState(int totalCostInCents, decimal totalScore, CombinedState? previous, PositionBudgetOption? option)
    {
        public static CombinedState Empty { get; } = new(0, 0m, null, null);

        public int TotalCostInCents { get; } = totalCostInCents;

        public decimal TotalScore { get; } = totalScore;

        public CombinedState? Previous { get; } = previous;

        public PositionBudgetOption? Option { get; } = option;
    }

    private sealed class FormationOptimizationResult(IReadOnlyList<PredictedLineupPositionRequirement> formation, CombinedState lineup)
    {
        public IReadOnlyList<PredictedLineupPositionRequirement> Formation { get; } = formation;

        public CombinedState Lineup { get; } = lineup;

        public decimal TotalScore => Lineup.TotalScore;

        public int TotalCostInCents => Lineup.TotalCostInCents;
    }
}
