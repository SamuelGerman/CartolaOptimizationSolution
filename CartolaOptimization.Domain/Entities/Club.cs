namespace CartolaOptimization.Domain.Entities
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Define <see cref="Club" />
    /// </summary>
    public class Club
    {
        /// <summary>
        /// Inicializa uma nova instância da classe <see cref="Club"/>.
        /// </summary>
        /// <param name="id">O id<see cref="int"/></param>
        /// <param name="cartolaId">O cartolaId<see cref="int"/></param>
        /// <param name="abreviation">O abreviation<see cref="string"/></param>
        /// <param name="slug">O slug<see cref="string"/></param>
        /// <param name="nickname">O nickname<see cref="string"/></param>
        /// <param name="logoUrl">O logoUrl<see cref="string"/></param>
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
        /// Obtém ou define Id
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Obtém ou define CartolaId
        /// </summary>
        public int CartolaId { get; set; }

        /// <summary>
        /// Obtém ou define Abreviation
        /// </summary>
        public string Abreviation { get; set; } = string.Empty;

        /// <summary>
        /// Obtém ou define Slug
        /// </summary>
        public string Slug { get; set; } = string.Empty;

        /// <summary>
        /// Obtém ou define Nickname
        /// </summary>
        public string Nickname { get; set; } = string.Empty;

        /// <summary>
        /// Obtém ou define LogoUrl
        /// </summary>
        public string LogoUrl { get; set; } = string.Empty;

        /// <summary>
        /// Obtém ou define AthleteRounds
        /// </summary>
        public ICollection<AthleteRound> AthleteRounds { get; set; } = new List<AthleteRound>();
    }
}
