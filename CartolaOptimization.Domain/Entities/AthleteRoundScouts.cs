namespace CartolaOptimization.Domain.Entities
{
    /// <summary>
    /// Define <see cref="AthleteRoundScouts" />
    /// </summary>
    public class AthleteRoundScouts
    {
        /// <summary>
        /// Inicializa uma nova instância da classe <see cref="AthleteRoundScouts"/>.
        /// </summary>
        /// <param name="id">O id<see cref="long"/></param>
        /// <param name="athleteRoundId">O athleteRoundId<see cref="long"/></param>
        /// <param name="scoutKey">O scoutKey<see cref="string"/></param>
        /// <param name="valor">O valor<see cref="decimal"/></param>
        public AthleteRoundScouts(long id, long athleteRoundId, string scoutKey, decimal valor)
        {
            Id = id;
            AthleteRoundId = athleteRoundId;
            ScoutKey = scoutKey ?? throw new ArgumentNullException(nameof(scoutKey));
            Valor = valor;
        }

        /// <summary>
        /// Obtém ou define Id
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Obtém ou define AthleteRoundId
        /// </summary>
        public long AthleteRoundId { get; set; }

        /// <summary>
        /// Obtém ou define ScoutKey
        /// </summary>
        public string ScoutKey { get; set; } = null!;

        /// <summary>
        /// Obtém ou define Valor
        /// </summary>
        public decimal Valor { get; set; }

        /// <summary>
        /// Obtém ou define AthleteRound
        /// </summary>
        public AthleteRound AthleteRound { get; set; } = null!;
    }
}
