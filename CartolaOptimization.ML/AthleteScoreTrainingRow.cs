namespace CartolaOptimization.ML;

internal sealed class AthleteScoreTrainingRow
{
    public float RoundId { get; init; }

    public float ClubCartolaId { get; init; }

    public float PositionCartolaId { get; init; }

    public float StatusCartolaId { get; init; }

    public float GamesNum { get; init; }

    public float HomeGamesNum { get; init; }

    public float AwayGamesNum { get; init; }

    public float MovingAverage { get; init; }

    public float AverageScore { get; init; }

    public float HomeAverageScore { get; init; }

    public float AwayAverageScore { get; init; }

    public float PriceNum { get; init; }

    public float VariationNum { get; init; }

    public float Label { get; init; }
}
