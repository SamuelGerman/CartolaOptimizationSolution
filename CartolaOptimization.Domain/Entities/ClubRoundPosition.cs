namespace CartolaOptimization.Domain.Entities
{
    /// <summary>
    /// Defines the <see cref="ClubRoundPosition" />
    /// </summary>
    public class ClubRoundPosition
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ClubRoundPosition"/> class.
        /// </summary>
        /// <param name="id">The id<see cref="long"/></param>
        /// <param name="clubRoundId">The clubRoundId<see cref="long"/></param>
        /// <param name="positionId">The positionId<see cref="int"/></param>
        /// <param name="pointsConcededAverage">The pointsConcededAverage<see cref="decimal"/></param>
        /// <param name="pointsConqueredAverage">The pointsConqueredAverage<see cref="decimal"/></param>
        public ClubRoundPosition(long id, long clubRoundId, int positionId, decimal pointsConcededAverage, decimal pointsConqueredAverage)
        {
            Id = id;
            ClubRoundId = clubRoundId;
            PositionId = positionId;
            PointsConcededAverage = pointsConcededAverage;
            PointsConqueredAverage = pointsConqueredAverage;
        }

        /// <summary>
        /// Gets or sets the Id
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Gets or sets the ClubRoundId
        /// </summary>
        public required long ClubRoundId { get; set; }

        /// <summary>
        /// Gets or sets the PositionId
        /// </summary>
        public required int PositionId { get; set; }

        // Representa o quão fácil/difícil é para um adversário pontuar contra este time nesta posição

        /// <summary>
        /// Gets or sets the PointsConcededAverage
        /// </summary>
        public decimal PointsConcededAverage { get; set; }

        // Representa o quanto os jogadores DESTE time pontuam nesta posição

        /// <summary>
        /// Gets or sets the PointsConqueredAverage
        /// </summary>
        public decimal PointsConqueredAverage { get; set; }

        // Navegação

        /// <summary>
        /// Gets or sets the ClubRound
        /// </summary>
        public ClubRound ClubRound { get; set; } = null!;

        /// <summary>
        /// Gets or sets the Position
        /// </summary>
        public Position Position { get; set; } = null!;
    }
}
