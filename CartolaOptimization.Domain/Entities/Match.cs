namespace CartolaOptimization.Domain.Entities
{
    using System;

    /// <summary>
    /// Defines the <see cref="Match" />
    /// </summary>
    public class Match
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Match"/> class.
        /// </summary>
        /// <param name="id">The id<see cref="int"/></param>
        /// <param name="cartolaId">The cartolaId<see cref="int"/></param>
        /// <param name="round">The round<see cref="int"/></param>
        /// <param name="matchDate">The matchDate<see cref="DateTime"/></param>
        /// <param name="location">The location<see cref="string"/></param>
        /// <param name="validForCartola">The validForCartola<see cref="bool"/></param>
        /// <param name="homeClubId">The homeClubId<see cref="int"/></param>
        /// <param name="homeClubPosition">The homeClubPosition<see cref="int"/></param>
        /// <param name="homeForm">The homeForm<see cref="string"/></param>
        /// <param name="homeScore">The homeScore<see cref="Nullable{Int32}"/></param>
        /// <param name="awayClubId">The awayClubId<see cref="int"/></param>
        /// <param name="awayClubPosition">The awayClubPosition<see cref="int"/></param>
        /// <param name="awayForm">The awayForm<see cref="string"/></param>
        /// <param name="awayScore">The awayScore<see cref="Nullable{Int32}"/></param>
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
        /// Gets or sets the Id
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the CartolaId
        /// </summary>
        public int CartolaId { get; set; }

        /// <summary>
        /// Gets or sets the Round
        /// </summary>
        public int Round { get; set; }

        /// <summary>
        /// Gets or sets the MatchDate
        /// </summary>
        public DateTime MatchDate { get; set; }

        /// <summary>
        /// Gets or sets the Location
        /// </summary>
        public string Location { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether ValidForCartola
        /// </summary>
        public bool ValidForCartola { get; set; }

        /// <summary>
        /// Gets or sets the HomeClubId
        /// </summary>
        public int HomeClubId { get; set; }

        /// <summary>
        /// Gets or sets the HomeClub
        /// </summary>
        public Club HomeClub { get; set; } = null!;

        /// <summary>
        /// Gets or sets the HomeClubPosition
        /// </summary>
        public int HomeClubPosition { get; set; }

        /// <summary>
        /// Gets or sets the HomeForm
        /// </summary>
        public string HomeForm { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the HomeScore
        /// </summary>
        public int? HomeScore { get; set; }

        /// <summary>
        /// Gets or sets the AwayClubId
        /// </summary>
        public int AwayClubId { get; set; }

        /// <summary>
        /// Gets or sets the AwayClub
        /// </summary>
        public Club AwayClub { get; set; } = null!;

        /// <summary>
        /// Gets or sets the AwayClubPosition
        /// </summary>
        public int AwayClubPosition { get; set; }

        /// <summary>
        /// Gets or sets the AwayForm
        /// </summary>
        public string AwayForm { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the AwayScore
        /// </summary>
        public int? AwayScore { get; set; }
    }
}
