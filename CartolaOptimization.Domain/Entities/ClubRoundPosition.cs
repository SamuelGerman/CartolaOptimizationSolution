namespace CartolaOptimization.Domain.Entities
{
    /// <summary>
    /// Define <see cref="ClubRoundPosition" />
    /// </summary>
    public class ClubRoundPosition
    {
        /// <summary>
        /// Inicializa uma nova instância da classe <see cref="ClubRoundPosition"/>.
        /// </summary>
        /// <param name="id">O id<see cref="long"/></param>
        /// <param name="clubRoundId">O clubRoundId<see cref="long"/></param>
        /// <param name="positionId">O positionId<see cref="int"/></param>
        /// <param name="pointsConcededAverage">O pointsConcededAverage<see cref="decimal"/></param>
        /// <param name="pointsConqueredAverage">O pointsConqueredAverage<see cref="decimal"/></param>
        /// <param name="distributionValue">O distributionValue<see cref="decimal"/></param>
        public ClubRoundPosition(long id, long clubRoundId, int positionId, decimal pointsConcededAverage, decimal pointsConqueredAverage, decimal distributionValue = 0m)
        {
            Id = id;
            ClubRoundId = clubRoundId;
            PositionId = positionId;
            PointsConcededAverage = pointsConcededAverage;
            PointsConqueredAverage = pointsConqueredAverage;
            DistributionValue = distributionValue;
        }

        /// <summary>
        /// Obtém ou define Id
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Obtém ou define ClubRoundId
        /// </summary>
        public long ClubRoundId { get; set; }

        /// <summary>
        /// Obtém ou define PositionId
        /// </summary>
        public int PositionId { get; set; }

        // Representa o quão fácil/difícil é para um adversário pontuar contra este time nesta posição

        /// <summary>
        /// Obtém ou define PointsConcededAverage
        /// </summary>
        public decimal PointsConcededAverage { get; set; }

        // Representa o quanto os jogadores DESTE time pontuam nesta posição

        /// <summary>
        /// Obtém ou define PointsConqueredAverage
        /// </summary>
        public decimal PointsConqueredAverage { get; set; }

        /// <summary>
        /// Obtém ou define a parcela de pontos do time historicamente concentrada nesta posição.
        /// </summary>
        public decimal DistributionValue { get; set; }

        // Navegação

        /// <summary>
        /// Obtém ou define ClubRound
        /// </summary>
        public ClubRound ClubRound { get; set; } = null!;

        /// <summary>
        /// Obtém ou define Position
        /// </summary>
        public Position Position { get; set; } = null!;
    }
}
