namespace CartolaOptimization.Domain.Entities
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Defines the <see cref="AthleteStatus" />
    /// </summary>
    public class AthleteStatus
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AthleteStatus"/> class.
        /// </summary>
        /// <param name="id">The id<see cref="int"/></param>
        /// <param name="cartolaId">The cartolaId<see cref="int"/></param>
        /// <param name="name">The name<see cref="string"/></param>
        public AthleteStatus(int id, int cartolaId, string name)
        {
            Id = id;
            CartolaId = cartolaId;
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        /// <summary>
        /// Gets or sets the Id
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the CartolaId
        /// </summary>
        public required int CartolaId { get; set; }

        /// <summary>
        /// Gets or sets the Name
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the AthleteRounds
        /// </summary>
        public ICollection<AthleteRound> AthleteRounds { get; set; } = [];
    }
}
