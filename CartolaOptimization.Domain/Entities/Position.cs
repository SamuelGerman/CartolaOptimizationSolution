namespace CartolaOptimization.Domain.Entities
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Defines the <see cref="Position" />
    /// </summary>
    public class Position
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Position"/> class.
        /// </summary>
        /// <param name="id">The id<see cref="int"/></param>
        /// <param name="cartolaId">The cartolaId<see cref="int"/></param>
        /// <param name="name">The name<see cref="string"/></param>
        /// <param name="abrevivation">The abrevivation<see cref="string"/></param>
        public Position(int id, int cartolaId, string name, string abrevivation)
        {
            Id = id;
            CartolaId = cartolaId;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Abreviation = abrevivation ?? throw new ArgumentNullException(nameof(abrevivation));
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
        /// Gets or sets the Abreviation
        /// </summary>
        public string Abreviation { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the AthleteRounds
        /// </summary>
        public ICollection<AthleteRound> AthleteRounds { get; set; } = new List<AthleteRound>();
    }
}
