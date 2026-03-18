namespace CartolaOptimization.Domain.Entities
{
    using System.Collections.Generic;

    //essa classe vira do endpoint /mercado/atletas e https://api.gatomestre.globo.com/api/v2/atletas/2026/campeonato-brasileiro/home/atletas/rodadas/{numrodada}?a=true

    /// <summary>
    /// Defines the <see cref="AthleteRound" />
    /// </summary>
    public class AthleteRound
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AthleteRound"/> class.
        /// </summary>
        /// <param name="id">The id<see cref="long"/></param>
        /// <param name="roundId">The roundId<see cref="int"/></param>
        /// <param name="athleteId">The athleteId<see cref="int"/></param>
        /// <param name="clubId">The clubId<see cref="int"/></param>
        /// <param name="positionId">The positionId<see cref="int"/></param>
        /// <param name="statusId">The statusId<see cref="int"/></param>
        /// <param name="gamesNum">The gamesNum<see cref="int"/></param>
        /// <param name="homeGamesNum">The homeGamesNum<see cref="int"/></param>
        /// <param name="awayGamesNum">The awayGamesNum<see cref="int"/></param>
        /// <param name="scoreNum">The scoreNum<see cref="decimal"/></param>
        /// <param name="movingAverage">The movingAverage<see cref="decimal"/></param>
        /// <param name="averageScoreNum">The averageScoreNum<see cref="decimal"/></param>
        /// <param name="homeAverageScore">The homeAverageScore<see cref="decimal"/></param>
        /// <param name="awayAverageScore">The awayAverageScore<see cref="decimal"/></param>
        /// <param name="variationNum">The variationNum<see cref="decimal"/></param>
        /// <param name="priceNum">The priceNum<see cref="decimal"/></param>
        /// <param name="wasInGame">The wasInGame<see cref="bool"/></param>
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
        /// Gets or sets the Id
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Gets or sets the RoundId
        /// </summary>
        public int RoundId { get; set; }

        /// <summary>
        /// Gets or sets the AthleteId
        /// </summary>
        public int AthleteId { get; set; }

        /// <summary>
        /// Gets or sets the ClubId
        /// </summary>
        public int ClubId { get; set; }

        /// <summary>
        /// Gets or sets the PositionId
        /// </summary>
        public int PositionId { get; set; }

        /// <summary>
        /// Gets or sets the StatusId
        /// </summary>
        public int StatusId { get; set; }

        // Jogos disputados (Recorte Casa/Fora)

        /// <summary>
        /// Gets or sets the GamesNum
        /// </summary>
        public int GamesNum { get; set; }

        /// <summary>
        /// Gets or sets the HomeGamesNum
        /// </summary>
        public int HomeGamesNum { get; set; }

        /// <summary>
        /// Gets or sets the AwayGamesNum
        /// </summary>
        public int AwayGamesNum { get; set; }

        // Médias e Pontuações

        /// <summary>
        /// Gets or sets the ScoreNum
        /// </summary>
        public decimal ScoreNum { get; set; }

        /// <summary>
        /// Gets or sets the MovingAverage
        /// </summary>
        public decimal MovingAverage { get; set; }

        /// <summary>
        /// Gets or sets the AverageScoreNum
        /// </summary>
        public decimal AverageScoreNum { get; set; }

        /// <summary>
        /// Gets or sets the HomeAverageScore
        /// </summary>
        public decimal HomeAverageScore { get; set; }

        /// <summary>
        /// Gets or sets the AwayAverageScore
        /// </summary>
        public decimal AwayAverageScore { get; set; }

        /// <summary>
        /// Gets or sets the VariationNum
        /// </summary>
        public decimal VariationNum { get; set; }

        /// <summary>
        /// Gets or sets the PriceNum
        /// </summary>
        public decimal PriceNum { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether WasInGame
        /// </summary>
        public bool WasInGame { get; set; }

        /// <summary>
        /// Gets or sets the phase that describes whether this row belongs to a closed historical round or an open/current round.
        /// </summary>
        public string DataPhase { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the readiness classification of the row for model training.
        /// </summary>
        public string TrainingReadinessStatus { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the reason associated with the training readiness classification.
        /// </summary>
        public string TrainingReadinessReason { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the row is currently eligible to be used by the training dataset.
        /// </summary>
        public bool IsTrainingEligible { get; set; }

        /// <summary>
        /// Gets or sets the Athlete
        /// </summary>
        public Athlete Athlete { get; set; } = null!;

        /// <summary>
        /// Gets or sets the Status
        /// </summary>
        public AthleteStatus Status { get; set; } = null!;

        /// <summary>
        /// Gets or sets the Club
        /// </summary>
        public Club Club { get; set; } = null!;

        /// <summary>
        /// Gets or sets the Position
        /// </summary>
        public Position Position { get; set; } = null!;

        /// <summary>
        /// Gets or sets the Scouts
        /// </summary>
        public ICollection<AthleteRoundScouts> Scouts { get; set; } = null!;
    }
}
