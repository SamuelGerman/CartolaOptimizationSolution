namespace CartolaOptimization.Domain.Entities
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Defines the <see cref="Athlete" />
    /// </summary>
    public class Athlete
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Athlete"/> class.
        /// </summary>
        /// <param name="id">The id<see cref="int"/></param>
        /// <param name="cartolaId">The cartolaId<see cref="int"/></param>
        /// <param name="name">The name<see cref="string"/></param>
        /// <param name="nickname">The nickname<see cref="string"/></param>
        public Athlete(int id, int cartolaId, string name, string nickname)
        {
            Id = id;
            CartolaId = cartolaId;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Nickname = nickname ?? throw new ArgumentNullException(nameof(nickname));
        }

        /// <summary>
        /// Gets or sets the Id
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the CartolaId
        /// </summary>
        public int CartolaId { get; set; }

        /// <summary>
        /// Gets or sets the Name
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Nickname
        /// </summary>
        public string Nickname { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Rounds
        /// </summary>
        public ICollection<AthleteRound> Rounds { get; set; } = new List<AthleteRound>();
    }
}
