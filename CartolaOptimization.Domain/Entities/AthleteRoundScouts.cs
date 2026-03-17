namespace CartolaOptimization.Domain.Entities
{
    /// <summary>
    /// Defines the <see cref="AthleteRoundScouts" />
    /// </summary>
    public class AthleteRoundScouts
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AthleteRoundScouts"/> class.
        /// </summary>
        /// <param name="id">The id<see cref="long"/></param>
        /// <param name="athleteRoundId">The athleteRoundId<see cref="long"/></param>
        /// <param name="scoutKey">The scoutKey<see cref="string"/></param>
        /// <param name="valor">The valor<see cref="decimal"/></param>
        public AthleteRoundScouts(long id, long athleteRoundId, string scoutKey, decimal valor)
        {
            Id = id;
            AthleteRoundId = athleteRoundId;
            ScoutKey = scoutKey ?? throw new ArgumentNullException(nameof(scoutKey));
            Valor = valor;
        }

        /// <summary>
        /// Gets or sets the Id
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Gets or sets the AthleteRoundId
        /// </summary>
        public long AthleteRoundId { get; set; }

        /// <summary>
        /// Gets or sets the ScoutKey
        /// </summary>
        public string ScoutKey { get; set; } = null!;

        /// <summary>
        /// Gets or sets the Valor
        /// </summary>
        public decimal Valor { get; set; }

        /// <summary>
        /// Gets or sets the AthleteRound
        /// </summary>
        public AthleteRound AthleteRound { get; set; } = null!;
    }
}
