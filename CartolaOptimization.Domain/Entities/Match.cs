namespace CartolaOptimization.Domain.Entities
{
    using System;

    /// <summary>
    /// Define <see cref="Match" />
    /// </summary>
    public class Match
    {
        /// <summary>
        /// Inicializa uma nova instância da classe <see cref="Match"/>.
        /// </summary>
        /// <param name="id">O id<see cref="int"/></param>
        /// <param name="cartolaId">O cartolaId<see cref="int"/></param>
        /// <param name="round">O round<see cref="int"/></param>
        /// <param name="matchDate">O matchDate<see cref="DateTime"/></param>
        /// <param name="location">O location<see cref="string"/></param>
        /// <param name="validForCartola">O validForCartola<see cref="bool"/></param>
        /// <param name="homeClubId">O homeClubId<see cref="int"/></param>
        /// <param name="homeClubPosition">O homeClubPosition<see cref="int"/></param>
        /// <param name="homeForm">O homeForm<see cref="string"/></param>
        /// <param name="homeScore">O homeScore<see cref="Nullable{Int32}"/></param>
        /// <param name="awayClubId">O awayClubId<see cref="int"/></param>
        /// <param name="awayClubPosition">O awayClubPosition<see cref="int"/></param>
        /// <param name="awayForm">O awayForm<see cref="string"/></param>
        /// <param name="awayScore">O awayScore<see cref="Nullable{Int32}"/></param>
        public Match(int id, int cartolaId, int round, DateTime matchDate, string location, bool validForCartola, int homeClubId, int homeClubPosition, string homeForm, int? homeScore, int awayClubId, int awayClubPosition, string awayForm, int? awayScore)
        {
            Id = id;
            CartolaId = cartolaId;
            Round = round;
            MatchDate = matchDate;
            Location = location ?? throw new ArgumentNullException(nameof(location));
            ValidForCartola = validForCartola;
            HomeClubId = homeClubId;
            HomeClubPosition = homeClubPosition;
            HomeForm = homeForm ?? throw new ArgumentNullException(nameof(homeForm));
            HomeScore = homeScore;
            AwayClubId = awayClubId;
            AwayClubPosition = awayClubPosition;
            AwayForm = awayForm ?? throw new ArgumentNullException(nameof(awayForm));
            AwayScore = awayScore;
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
        /// Obtém ou define Round
        /// </summary>
        public int Round { get; set; }

        /// <summary>
        /// Obtém ou define MatchDate
        /// </summary>
        public DateTime MatchDate { get; set; }

        /// <summary>
        /// Obtém ou define Location
        /// </summary>
        public string Location { get; set; }

        /// <summary>
        /// Obtém ou define um valor que indica se ValidForCartola
        /// </summary>
        public bool ValidForCartola { get; set; }

        /// <summary>
        /// Obtém ou define HomeClubId
        /// </summary>
        public int HomeClubId { get; set; }

        /// <summary>
        /// Obtém ou define HomeClub
        /// </summary>
        public Club HomeClub { get; set; } = null!;

        /// <summary>
        /// Obtém ou define HomeClubPosition
        /// </summary>
        public int HomeClubPosition { get; set; }

        /// <summary>
        /// Obtém ou define HomeForm
        /// </summary>
        public string HomeForm { get; set; } = string.Empty;

        /// <summary>
        /// Obtém ou define HomeScore
        /// </summary>
        public int? HomeScore { get; set; }

        /// <summary>
        /// Obtém ou define AwayClubId
        /// </summary>
        public int AwayClubId { get; set; }

        /// <summary>
        /// Obtém ou define AwayClub
        /// </summary>
        public Club AwayClub { get; set; } = null!;

        /// <summary>
        /// Obtém ou define AwayClubPosition
        /// </summary>
        public int AwayClubPosition { get; set; }

        /// <summary>
        /// Obtém ou define AwayForm
        /// </summary>
        public string AwayForm { get; set; } = string.Empty;

        /// <summary>
        /// Obtém ou define AwayScore
        /// </summary>
        public int? AwayScore { get; set; }
    }
}
