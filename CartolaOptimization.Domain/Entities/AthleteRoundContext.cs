namespace CartolaOptimization.Domain.Entities
{
    /// <summary>
    /// Armazena sinais adicionais de enriquecimento de atleta-rodada coletados do Gato Mestre.
    /// </summary>
    public class AthleteRoundContext
    {
        public AthleteRoundContext()
        {
        }

        public AthleteRoundContext(
            long id,
            long athleteRoundId,
            decimal teamAverageScore,
            decimal teamHomeAverageCurrent,
            decimal teamHomeAveragePrevious,
            decimal teamAwayAverageCurrent,
            decimal teamAwayAveragePrevious,
            decimal pointsPerCartoletaCurrent,
            decimal pointsPerCartoletaPrevious,
            decimal grossOverallAverageCurrent,
            decimal grossOverallMovingAverageCurrent,
            decimal grossOverallAveragePrevious,
            decimal grossOverallMovingAveragePrevious,
            decimal netOverallAverageCurrent,
            decimal netOverallMovingAverageCurrent,
            decimal netOverallAveragePrevious,
            decimal netOverallMovingAveragePrevious,
            decimal valuationCurrent,
            decimal minimumToAppreciateCurrent,
            decimal valuationPrevious,
            decimal minimumToAppreciatePrevious,
            decimal indexPointsPerCartoleta,
            decimal indexOffensiveUsage,
            decimal indexOffensiveRepresentation,
            decimal indexPointsRepresentation,
            decimal indexOffensiveEfficiency,
            decimal indexDefensiveEfficiency,
            decimal indexTackleDuelPercent,
            decimal indexShotsPerGame,
            decimal indexShotsPerGoal)
        {
            Id = id;
            AthleteRoundId = athleteRoundId;
            TeamAverageScore = teamAverageScore;
            TeamHomeAverageCurrent = teamHomeAverageCurrent;
            TeamHomeAveragePrevious = teamHomeAveragePrevious;
            TeamAwayAverageCurrent = teamAwayAverageCurrent;
            TeamAwayAveragePrevious = teamAwayAveragePrevious;
            PointsPerCartoletaCurrent = pointsPerCartoletaCurrent;
            PointsPerCartoletaPrevious = pointsPerCartoletaPrevious;
            GrossOverallAverageCurrent = grossOverallAverageCurrent;
            GrossOverallMovingAverageCurrent = grossOverallMovingAverageCurrent;
            GrossOverallAveragePrevious = grossOverallAveragePrevious;
            GrossOverallMovingAveragePrevious = grossOverallMovingAveragePrevious;
            NetOverallAverageCurrent = netOverallAverageCurrent;
            NetOverallMovingAverageCurrent = netOverallMovingAverageCurrent;
            NetOverallAveragePrevious = netOverallAveragePrevious;
            NetOverallMovingAveragePrevious = netOverallMovingAveragePrevious;
            ValuationCurrent = valuationCurrent;
            MinimumToAppreciateCurrent = minimumToAppreciateCurrent;
            ValuationPrevious = valuationPrevious;
            MinimumToAppreciatePrevious = minimumToAppreciatePrevious;
            IndexPointsPerCartoleta = indexPointsPerCartoleta;
            IndexOffensiveUsage = indexOffensiveUsage;
            IndexOffensiveRepresentation = indexOffensiveRepresentation;
            IndexPointsRepresentation = indexPointsRepresentation;
            IndexOffensiveEfficiency = indexOffensiveEfficiency;
            IndexDefensiveEfficiency = indexDefensiveEfficiency;
            IndexTackleDuelPercent = indexTackleDuelPercent;
            IndexShotsPerGame = indexShotsPerGame;
            IndexShotsPerGoal = indexShotsPerGoal;
        }

        public long Id { get; set; }

        public long AthleteRoundId { get; set; }

        public decimal TeamAverageScore { get; set; }

        public decimal TeamHomeAverageCurrent { get; set; }

        public decimal TeamHomeAveragePrevious { get; set; }

        public decimal TeamAwayAverageCurrent { get; set; }

        public decimal TeamAwayAveragePrevious { get; set; }

        public decimal PointsPerCartoletaCurrent { get; set; }

        public decimal PointsPerCartoletaPrevious { get; set; }

        public decimal GrossOverallAverageCurrent { get; set; }

        public decimal GrossOverallMovingAverageCurrent { get; set; }

        public decimal GrossOverallAveragePrevious { get; set; }

        public decimal GrossOverallMovingAveragePrevious { get; set; }

        public decimal NetOverallAverageCurrent { get; set; }

        public decimal NetOverallMovingAverageCurrent { get; set; }

        public decimal NetOverallAveragePrevious { get; set; }

        public decimal NetOverallMovingAveragePrevious { get; set; }

        public decimal ValuationCurrent { get; set; }

        public decimal MinimumToAppreciateCurrent { get; set; }

        public decimal ValuationPrevious { get; set; }

        public decimal MinimumToAppreciatePrevious { get; set; }

        public decimal IndexPointsPerCartoleta { get; set; }

        public decimal IndexOffensiveUsage { get; set; }

        public decimal IndexOffensiveRepresentation { get; set; }

        public decimal IndexPointsRepresentation { get; set; }

        public decimal IndexOffensiveEfficiency { get; set; }

        public decimal IndexDefensiveEfficiency { get; set; }

        public decimal IndexTackleDuelPercent { get; set; }

        public decimal IndexShotsPerGame { get; set; }

        public decimal IndexShotsPerGoal { get; set; }

        public AthleteRound AthleteRound { get; set; } = null!;
    }
}
