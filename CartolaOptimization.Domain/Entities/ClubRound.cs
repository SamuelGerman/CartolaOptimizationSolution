namespace CartolaOptimization.Domain.Entities
{
    using System.Collections.Generic;

    /// <summary>
    /// Defines the <see cref="ClubRound" />
    /// </summary>
    public class ClubRound
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ClubRound"/> class.
        /// </summary>
        /// <param name="id">The id<see cref="long"/></param>
        /// <param name="round">The round<see cref="int"/></param>
        /// <param name="clubId">The clubId<see cref="int"/></param>
        /// <param name="winProbability">The winProbability<see cref="decimal"/></param>
        /// <param name="drawProbability">The drawProbability<see cref="decimal"/></param>
        /// <param name="loseProbability">The loseProbability<see cref="decimal"/></param>
        /// <param name="overallAverage">The overallAverage<see cref="decimal"/></param>
        /// <param name="movingAverage">The movingAverage<see cref="decimal"/></param>
        /// <param name="expectedPoints">The expectedPoints<see cref="decimal"/></param>
        public ClubRound(long id, int round, int clubId, decimal winProbability, decimal drawProbability, decimal loseProbability, decimal overallAverage, decimal movingAverage, decimal expectedPoints)
        {
            Id = id;
            Round = round;
            ClubId = clubId;
            WinProbability = winProbability;
            DrawProbability = drawProbability;
            LoseProbability = loseProbability;
            OverallAverage = overallAverage;
            MovingAverage = movingAverage;
            ExpectedPoints = expectedPoints;
        }

        /// <summary>
        /// Gets or sets the Id
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Gets or sets the Round
        /// </summary>
        public required int Round { get; set; }

        /// <summary>
        /// Gets or sets the ClubId
        /// </summary>
        public required int ClubId { get; set; }

        // Probabilidade de Vitória e Empate (%) - Essencial para prever SG (Saldo de Gols)

        /// <summary>
        /// Gets or sets the WinProbability
        /// </summary>
        public decimal WinProbability { get; set; }

        /// <summary>
        /// Gets or sets the DrawProbability
        /// </summary>
        public decimal DrawProbability { get; set; }

        /// <summary>
        /// Gets or sets the LoseProbability
        /// </summary>
        public decimal LoseProbability { get; set; }

        // Médias de pontos da equipe (Gato Mestre)

        /// <summary>
        /// Gets or sets the OverallAverage
        /// </summary>
        public decimal OverallAverage { get; set; }

        /// <summary>
        /// Gets or sets the MovingAverage
        /// </summary>
        public decimal MovingAverage { get; set; }

        /// <summary>
        /// Gets or sets the ExpectedPoints
        /// </summary>
        public decimal ExpectedPoints { get; set; }

        // Navegação

        /// <summary>
        /// Gets or sets the Club
        /// </summary>
        public Club Club { get; set; } = null!;

        // Estatísticas granulares por posição (A cereja do bolo para o ML)

        /// <summary>
        /// Gets or sets the PositionStats
        /// </summary>
        public ICollection<ClubRoundPosition> PositionStats { get; set; } = [];
    }
}
