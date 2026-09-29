namespace CartolaOptimization.Domain.Entities
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Define <see cref="AthleteStatus" />
    /// </summary>
    public class AthleteStatus
    {
        /// <summary>
        /// Inicializa uma nova instância da classe <see cref="AthleteStatus"/>.
        /// </summary>
        /// <param name="id">O id<see cref="int"/></param>
        /// <param name="cartolaId">O cartolaId<see cref="int"/></param>
        /// <param name="name">O name<see cref="string"/></param>
        public AthleteStatus(int id, int cartolaId, string name)
        {
            Id = id;
            CartolaId = cartolaId;
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        /// <summary>
        /// Obtém ou define Id
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Obtém ou define CartolaId
        /// </summary>
        public int CartolaId { get; set; }

        /// <summary>
        /// Obtém ou define Name
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Obtém ou define AthleteRounds
        /// </summary>
        public ICollection<AthleteRound> AthleteRounds { get; set; } = [];
    }
}
