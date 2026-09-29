namespace CartolaOptimization.Domain.Entities
{
    using System.Collections.Generic;

    /// <summary>
    /// Define <see cref="ClubRound" />
    /// </summary>
    public class ClubRound
    {
        /// <summary>
        /// Inicializa uma nova instância da classe <see cref="ClubRound"/>.
        /// </summary>
        /// <param name="id">O id<see cref="long"/></param>
        /// <param name="round">O round<see cref="int"/></param>
        /// <param name="clubId">O clubId<see cref="int"/></param>
        /// <param name="winProbability">O winProbability<see cref="decimal"/></param>
        /// <param name="drawProbability">O drawProbability<see cref="decimal"/></param>
        /// <param name="loseProbability">O loseProbability<see cref="decimal"/></param>
        /// <param name="overallAverage">O overallAverage<see cref="decimal"/></param>
        /// <param name="movingAverage">O movingAverage<see cref="decimal"/></param>
        /// <param name="expectedPoints">O expectedPoints<see cref="decimal"/></param>
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
        /// Obtém ou define Id
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Obtém ou define Round
        /// </summary>
        public int Round { get; set; }

        /// <summary>
        /// Obtém ou define ClubId
        /// </summary>
        public int ClubId { get; set; }

        // Probabilidade de Vitória e Empate (%) - Essencial para prever SG (Saldo de Gols)

        /// <summary>
        /// Obtém ou define WinProbability
        /// </summary>
        public decimal WinProbability { get; set; }

        /// <summary>
        /// Obtém ou define DrawProbability
        /// </summary>
        public decimal DrawProbability { get; set; }

        /// <summary>
        /// Obtém ou define LoseProbability
        /// </summary>
        public decimal LoseProbability { get; set; }

        // Médias de pontos da equipe (Gato Mestre)

        /// <summary>
        /// Obtém ou define OverallAverage
        /// </summary>
        public decimal OverallAverage { get; set; }

        /// <summary>
        /// Obtém ou define MovingAverage
        /// </summary>
        public decimal MovingAverage { get; set; }

        /// <summary>
        /// Obtém ou define ExpectedPoints
        /// </summary>
        public decimal ExpectedPoints { get; set; }

        /// <summary>
        /// Obtém ou define a classificação de prontidão da linha para o treino do modelo.
        /// </summary>
        public string TrainingReadinessStatus { get; set; } = string.Empty;

        /// <summary>
        /// Obtém ou define o motivo associado à classificação de prontidão para treino.
        /// </summary>
        public string TrainingReadinessReason { get; set; } = string.Empty;

        /// <summary>
        /// Obtém ou define um valor que indica se a linha está atualmente elegível para uso pelo conjunto de dados de treino.
        /// </summary>
        public bool IsTrainingEligible { get; set; }

        // Navegação

        /// <summary>
        /// Obtém ou define Club
        /// </summary>
        public Club Club { get; set; } = null!;

        // Estatísticas granulares por posição (A cereja do bolo para o ML)

        /// <summary>
        /// Obtém ou define PositionStats
        /// </summary>
        public ICollection<ClubRoundPosition> PositionStats { get; set; } = [];

        /// <summary>
        /// Obtém ou define o enriquecimento adicional de clube-rodada coletado do Gato Mestre.
        /// </summary>
        public ClubRoundContext? Context { get; set; }
    }
}
