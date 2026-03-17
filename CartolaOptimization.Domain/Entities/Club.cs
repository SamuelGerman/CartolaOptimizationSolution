namespace CartolaOptimization.Domain.Entities
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Defines the <see cref="Club" />
    /// </summary>
    public class Club
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Club"/> class.
        /// </summary>
        /// <param name="id">The id<see cref="int"/></param>
        /// <param name="cartolaId">The cartolaId<see cref="int"/></param>
        /// <param name="abreviation">The abreviation<see cref="string"/></param>
        /// <param name="slug">The slug<see cref="string"/></param>
        /// <param name="nickname">The nickname<see cref="string"/></param>
        /// <param name="logoUrl">The logoUrl<see cref="string"/></param>
        public Club(int id, int cartolaId, string abreviation, string slug, string nickname, string logoUrl)
        {
            Id = id;
            CartolaId = cartolaId;
            Abreviation = abreviation ?? throw new ArgumentNullException(nameof(abreviation));
            Slug = slug ?? throw new ArgumentNullException(nameof(slug));
            Nickname = nickname ?? throw new ArgumentNullException(nameof(nickname));
            LogoUrl = logoUrl ?? throw new ArgumentNullException(nameof(logoUrl));
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
        /// Gets or sets the Abreviation
        /// </summary>
        public string Abreviation { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Slug
        /// </summary>
        public string Slug { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Nickname
        /// </summary>
        public string Nickname { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the LogoUrl
        /// </summary>
        public string LogoUrl { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the AthleteRounds
        /// </summary>
        public ICollection<AthleteRound> AthleteRounds { get; set; }
    }
}
