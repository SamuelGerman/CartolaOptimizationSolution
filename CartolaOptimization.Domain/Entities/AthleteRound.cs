namespace CartolaOptimization.Domain.Entities
{
    using System.Collections.Generic;

    //essa classe vira do endpoint /mercado/atletas e https://api.gatomestre.globo.com/api/v2/atletas/2026/campeonato-brasileiro/home/atletas/rodadas/{numrodada}?a=true

    /// <summary>
    /// Define <see cref="AthleteRound" />
    /// </summary>
    public class AthleteRound
    {
        /// <summary>
        /// Inicializa uma nova instância da classe <see cref="AthleteRound"/>.
        /// </summary>
        /// <param name="id">O id<see cref="long"/></param>
        /// <param name="roundId">O roundId<see cref="int"/></param>
        /// <param name="athleteId">O athleteId<see cref="int"/></param>
        /// <param name="clubId">O clubId<see cref="int"/></param>
        /// <param name="positionId">O positionId<see cref="int"/></param>
        /// <param name="statusId">O statusId<see cref="int"/></param>
        /// <param name="gamesNum">O gamesNum<see cref="int"/></param>
        /// <param name="homeGamesNum">O homeGamesNum<see cref="int"/></param>
        /// <param name="awayGamesNum">O awayGamesNum<see cref="int"/></param>
        /// <param name="scoreNum">O scoreNum<see cref="decimal"/></param>
        /// <param name="movingAverage">O movingAverage<see cref="decimal"/></param>
        /// <param name="averageScoreNum">O averageScoreNum<see cref="decimal"/></param>
        /// <param name="homeAverageScore">O homeAverageScore<see cref="decimal"/></param>
        /// <param name="awayAverageScore">O awayAverageScore<see cref="decimal"/></param>
        /// <param name="variationNum">O variationNum<see cref="decimal"/></param>
        /// <param name="priceNum">O priceNum<see cref="decimal"/></param>
        /// <param name="wasInGame">O wasInGame<see cref="bool"/></param>
        public AthleteRound(long id, int roundId, int athleteId, int clubId, int positionId, int statusId, int gamesNum, int homeGamesNum, int awayGamesNum, decimal scoreNum, decimal movingAverage, decimal averageScoreNum, decimal homeAverageScore, decimal awayAverageScore, decimal variationNum, decimal priceNum, bool wasInGame)
        {
            Id = id;
            RoundId = roundId;
            AthleteId = athleteId;
            ClubId = clubId;
            PositionId = positionId;
            StatusId = statusId;
            GamesNum = gamesNum;
            HomeGamesNum = homeGamesNum;
            AwayGamesNum = awayGamesNum;
            ScoreNum = scoreNum;
            MovingAverage = movingAverage;
            AverageScoreNum = averageScoreNum;
            HomeAverageScore = homeAverageScore;
            AwayAverageScore = awayAverageScore;
            VariationNum = variationNum;
            PriceNum = priceNum;
            WasInGame = wasInGame;
        }

        /// <summary>
        /// Obtém ou define Id
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Obtém ou define RoundId
        /// </summary>
        public int RoundId { get; set; }

        /// <summary>
        /// Obtém ou define AthleteId
        /// </summary>
        public int AthleteId { get; set; }

        /// <summary>
        /// Obtém ou define ClubId
        /// </summary>
        public int ClubId { get; set; }

        /// <summary>
        /// Obtém ou define PositionId
        /// </summary>
        public int PositionId { get; set; }

        /// <summary>
        /// Obtém ou define StatusId
        /// </summary>
        public int StatusId { get; set; }

        // Jogos disputados (Recorte Casa/Fora)

        /// <summary>
        /// Obtém ou define GamesNum
        /// </summary>
        public int GamesNum { get; set; }

        /// <summary>
        /// Obtém ou define HomeGamesNum
        /// </summary>
        public int HomeGamesNum { get; set; }

        /// <summary>
        /// Obtém ou define AwayGamesNum
        /// </summary>
        public int AwayGamesNum { get; set; }

        // Médias e Pontuações

        /// <summary>
        /// Obtém ou define ScoreNum
        /// </summary>
        public decimal ScoreNum { get; set; }

        /// <summary>
        /// Obtém ou define MovingAverage
        /// </summary>
        public decimal MovingAverage { get; set; }

        /// <summary>
        /// Obtém ou define AverageScoreNum
        /// </summary>
        public decimal AverageScoreNum { get; set; }

        /// <summary>
        /// Obtém ou define HomeAverageScore
        /// </summary>
        public decimal HomeAverageScore { get; set; }

        /// <summary>
        /// Obtém ou define AwayAverageScore
        /// </summary>
        public decimal AwayAverageScore { get; set; }

        /// <summary>
        /// Obtém ou define VariationNum
        /// </summary>
        public decimal VariationNum { get; set; }

        /// <summary>
        /// Obtém ou define PriceNum
        /// </summary>
        public decimal PriceNum { get; set; }

        /// <summary>
        /// Obtém ou define um valor que indica se WasInGame
        /// </summary>
        public bool WasInGame { get; set; }

        /// <summary>
        /// Obtém ou define a fase que descreve se esta linha pertence a uma rodada histórica fechada ou a uma rodada aberta/atual.
        /// </summary>
        public string DataPhase { get; set; } = string.Empty;

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

        /// <summary>
        /// Obtém ou define Athlete
        /// </summary>
        public Athlete Athlete { get; set; } = null!;

        /// <summary>
        /// Obtém ou define Status
        /// </summary>
        public AthleteStatus Status { get; set; } = null!;

        /// <summary>
        /// Obtém ou define Club
        /// </summary>
        public Club Club { get; set; } = null!;

        /// <summary>
        /// Obtém ou define Position
        /// </summary>
        public Position Position { get; set; } = null!;

        /// <summary>
        /// Obtém ou define Scouts
        /// </summary>
        public ICollection<AthleteRoundScouts> Scouts { get; set; } = null!;

        /// <summary>
        /// Obtém ou define o enriquecimento adicional de atleta-rodada coletado do Gato Mestre.
        /// </summary>
        public AthleteRoundContext? Context { get; set; }
    }
}
