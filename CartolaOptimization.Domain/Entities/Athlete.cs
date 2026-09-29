namespace CartolaOptimization.Domain.Entities
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Define <see cref="Athlete" />
    /// </summary>
    public class Athlete
    {
        /// <summary>
        /// Inicializa uma nova instância da classe <see cref="Athlete"/>.
        /// </summary>
        /// <param name="id">O id<see cref="int"/></param>
        /// <param name="cartolaId">O cartolaId<see cref="int"/></param>
        /// <param name="name">O name<see cref="string"/></param>
        /// <param name="nickname">O nickname<see cref="string"/></param>
        public Athlete(int id, int cartolaId, string name, string nickname)
        {
            Id = id;
            CartolaId = cartolaId;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Nickname = nickname ?? throw new ArgumentNullException(nameof(nickname));
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
        /// Obtém ou define Nickname
        /// </summary>
        public string Nickname { get; set; } = string.Empty;

        /// <summary>
        /// Obtém ou define Rounds
        /// </summary>
        public ICollection<AthleteRound> Rounds { get; set; } = new List<AthleteRound>();
    }
}
