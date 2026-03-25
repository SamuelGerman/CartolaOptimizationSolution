using CartolaOptimization.Domain.Entities;

namespace CartolaOptimization.ML;

internal sealed class AthleteContextPredictionCandidate
{
    public required AthleteRound Baseline { get; init; }

    public required AthleteContextAthleteScorePredictionInput Input { get; init; }

    public bool IsHomeMatch { get; init; }

    public int ClubTablePosition { get; init; }

    public int OpponentClubCartolaId { get; init; }

    public string OpponentClubName { get; init; } = string.Empty;

    public int OpponentClubTablePosition { get; init; }
}
