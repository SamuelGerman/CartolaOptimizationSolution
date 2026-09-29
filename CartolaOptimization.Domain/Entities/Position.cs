namespace CartolaOptimization.Domain.Entities
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Define <see cref="Position" />
    /// </summary>
    public class Position
    {
        /// <summary>
        /// Inicializa uma nova instância da classe <see cref="Position"/>.
        /// </summary>
        /// <param name="id">O id<see cref="int"/></param>
        /// <param name="cartolaId">O cartolaId<see cref="int"/></param>
        /// <param name="name">O name<see cref="string"/></param>
        /// <param name="abreviation">O abreviation<see cref="string"/></param>
        public Position(int id, int cartolaId, string name, string abreviation)
        {
            Id = id;
            CartolaId = cartolaId;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Abreviation = abreviation ?? throw new ArgumentNullException(nameof(abreviation));
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
        /// Obtém ou define Abreviation
        /// </summary>
        public string Abreviation { get; set; } = string.Empty;

        /// <summary>
        /// Obtém ou define AthleteRounds
        /// </summary>
        public ICollection<AthleteRound> AthleteRounds { get; set; } = new List<AthleteRound>();
    }
}
