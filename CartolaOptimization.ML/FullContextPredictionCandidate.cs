using CartolaOptimization.Domain.Entities;

namespace CartolaOptimization.ML;

internal sealed class FullContextPredictionCandidate
{
    public required AthleteRound Baseline { get; init; }

    public required FullContextAthleteScorePredictionInput Input { get; init; }

    public bool IsHomeMatch { get; init; }

    public int ClubTablePosition { get; init; }

    public int OpponentClubCartolaId { get; init; }

    public string OpponentClubName { get; init; } = string.Empty;

    public int OpponentClubTablePosition { get; init; }

    public decimal ClubWinProbability { get; init; }

    public decimal ClubExpectedPoints { get; init; }

    public decimal OpponentWinProbability { get; init; }

    public decimal OpponentExpectedPoints { get; init; }
}
