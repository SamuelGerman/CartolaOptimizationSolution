namespace CartolaOptimization.ML;

internal sealed class AthleteContextAthleteScoreTrainingRow
{
    public float RoundId { get; init; }

    public float ClubCartolaId { get; init; }

    public float OpponentClubCartolaId { get; init; }

    public float PositionCartolaId { get; init; }

    public float StatusCartolaId { get; init; }

    public float GamesNum { get; init; }

    public float HomeGamesNum { get; init; }

    public float AwayGamesNum { get; init; }

    public float GamesAtVenueNum { get; init; }

    public float MovingAverage { get; init; }

    public float AverageScore { get; init; }

    public float HomeAverageScore { get; init; }

    public float AwayAverageScore { get; init; }

    public float ContextualAverageScore { get; init; }

    public float PriceNum { get; init; }

    public float VariationNum { get; init; }

    public float HasAthleteContext { get; init; }

    public float TeamAverageScore { get; init; }

    public float TeamHomeAverageCurrent { get; init; }

    public float TeamHomeAveragePrevious { get; init; }

    public float TeamAwayAverageCurrent { get; init; }

    public float TeamAwayAveragePrevious { get; init; }

    public float TeamVenueAverageCurrent { get; init; }

    public float TeamVenueAveragePrevious { get; init; }

    public float PointsPerCartoletaCurrent { get; init; }

    public float PointsPerCartoletaPrevious { get; init; }

    public float GrossOverallAverageCurrent { get; init; }

    public float GrossOverallMovingAverageCurrent { get; init; }

    public float GrossOverallAveragePrevious { get; init; }

    public float GrossOverallMovingAveragePrevious { get; init; }

    public float NetOverallAverageCurrent { get; init; }

    public float NetOverallMovingAverageCurrent { get; init; }

    public float NetOverallAveragePrevious { get; init; }

    public float NetOverallMovingAveragePrevious { get; init; }

    public float ValuationCurrent { get; init; }

    public float MinimumToAppreciateCurrent { get; init; }

    public float ValuationPrevious { get; init; }

    public float MinimumToAppreciatePrevious { get; init; }

    public float IndexPointsPerCartoleta { get; init; }

    public float IndexOffensiveUsage { get; init; }

    public float IndexOffensiveRepresentation { get; init; }

    public float IndexPointsRepresentation { get; init; }

    public float IndexOffensiveEfficiency { get; init; }

    public float IndexDefensiveEfficiency { get; init; }

    public float IndexTackleDuelPercent { get; init; }

    public float IndexShotsPerGame { get; init; }

    public float IndexShotsPerGoal { get; init; }

    public float IsHomeMatch { get; init; }

    public float ClubTablePosition { get; init; }

    public float OpponentClubTablePosition { get; init; }

    public float TablePositionDelta { get; init; }

    public float ClubRecentFormPoints { get; init; }

    public float OpponentRecentFormPoints { get; init; }

    public float RecentFormPointsDelta { get; init; }

    public float Label { get; init; }
}
