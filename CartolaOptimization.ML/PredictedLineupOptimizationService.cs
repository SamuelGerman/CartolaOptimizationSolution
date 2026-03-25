using CartolaOptimization.Application.Interfaces;
using CartolaOptimization.Application.Models;

namespace CartolaOptimization.ML;

public sealed class PredictedLineupOptimizationService(
    IAthleteScorePredictionService athleteScorePredictionService,
    IAthleteContextAthleteScorePredictionService athleteContextAthleteScorePredictionService) : IPredictedLineupOptimizationService
{
    private const int ProbableStatusCartolaId = 7;
    private const string ProbableStatusName = "Provável";
    private const int MaxPredictionCandidates = 5000;

    private static readonly IReadOnlyDictionary<int, string> KnownPositionNames = new Dictionary<int, string>
    {
        [1] = "Goleiro",
        [2] = "Lateral",
        [3] = "Zagueiro",
        [4] = "Meia",
        [5] = "Atacante",
        [6] = "Técnico",
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

    private static async Task<PredictedLineupResult> GetOptimalLineupCoreAsync(
        int round,
        PredictedLineupRequest request,
        Func<int, int?, int?, int?, int, CancellationToken, Task<AthleteScorePredictionResult>> predictionFunc,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (round <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(round), "Round numbers must be greater than zero.");
        }

        if (request.Budget <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(request.Budget), "Budget must be greater than zero.");
        }

        var normalizedFormation = NormalizeFormation(request.Formation);
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
            .ToDictionary(group => group.Key, group => group
                .OrderByDescending(item => item.PredictedScore)
                .ThenBy(item => item.PriceNum)
                .ThenBy(item => item.AthleteName)
                .ToList());

        foreach (var requirement in normalizedFormation)
        {
            var availableCandidates = candidatesByPosition.TryGetValue(requirement.PositionCartolaId, out var candidates)
                ? candidates.Count
                : 0;

            if (availableCandidates < requirement.Quantity)
            {
                var positionName = ResolvePositionName(requirement.PositionCartolaId, candidatesByPosition);
                throw new InvalidOperationException(
                    $"The requested formation needs {requirement.Quantity} athlete(s) for position '{positionName}', but only {availableCandidates} provável athlete(s) were available for round {round}.");
            }
        }

        var positionOptions = normalizedFormation
            .Select(requirement => BuildPositionOptions(
                requirement,
                candidatesByPosition[requirement.PositionCartolaId],
                budgetInCents))
            .ToList();

        var optimalLineup = CombinePositionOptions(positionOptions, budgetInCents);
        if (optimalLineup is null)
        {
            throw new InvalidOperationException(
                $"No feasible lineup was found within budget {request.Budget:F2} for the requested formation. Increase the budget or relax the formation constraints.");
        }

        var selectedAthletes = ReconstructSelectedAthletes(optimalLineup)
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
                PredictionNotes = item.PredictionNotes,
            })
            .ToList();

        var totalCost = FromCents(optimalLineup.TotalCostInCents);
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
            TotalPredictedScore = Math.Round(optimalLineup.TotalScore, 2, MidpointRounding.AwayFromZero),
            TotalPlayers = selectedAthletes.Count,
            TotalProbableAthletesEvaluated = predictions.TotalAthletesConsidered,
            ProbableStatusCartolaId = ProbableStatusCartolaId,
            ProbableStatusName = ProbableStatusName,
            ValidationMeanAbsoluteError = predictions.ValidationMeanAbsoluteError,
            ValidationRootMeanSquaredError = predictions.ValidationRootMeanSquaredError,
            OptimizationNotes = usesFallbackBaselines
                ? $"Optimization considered only provável athletes and used the latest persisted baseline before round {round} because current pre-round rows were not available for all candidates."
                : "Optimization considered only provável athletes and used current pre-round baselines for the target round.",
            Formation = normalizedFormation,
            Athletes = selectedAthletes,
        };
    }

    private static IReadOnlyList<PredictedLineupPositionRequirement> NormalizeFormation(
        IReadOnlyCollection<PredictedLineupPositionRequirement> formation)
    {
        if (formation.Count == 0)
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
}
