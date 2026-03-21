using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CartolaOptimization.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGatoMestreRoundContexts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DistributionValue",
                table: "ClubRoundPositions",
                type: "decimal(10,4)",
                precision: 10,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "AthleteRoundContexts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    AthleteRoundId = table.Column<long>(type: "bigint", nullable: false),
                    TeamAverageScore = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    TeamHomeAverageCurrent = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    TeamHomeAveragePrevious = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    TeamAwayAverageCurrent = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    TeamAwayAveragePrevious = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    PointsPerCartoletaCurrent = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    PointsPerCartoletaPrevious = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    GrossOverallAverageCurrent = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    GrossOverallMovingAverageCurrent = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    GrossOverallAveragePrevious = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    GrossOverallMovingAveragePrevious = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    NetOverallAverageCurrent = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    NetOverallMovingAverageCurrent = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    NetOverallAveragePrevious = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    NetOverallMovingAveragePrevious = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    ValuationCurrent = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    MinimumToAppreciateCurrent = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    ValuationPrevious = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    MinimumToAppreciatePrevious = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    IndexPointsPerCartoleta = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    IndexOffensiveUsage = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    IndexOffensiveRepresentation = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    IndexPointsRepresentation = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    IndexOffensiveEfficiency = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    IndexDefensiveEfficiency = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    IndexTackleDuelPercent = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    IndexShotsPerGame = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    IndexShotsPerGoal = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AthleteRoundContexts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AthleteRoundContexts_AthleteRounds_AthleteRoundId",
                        column: x => x.AthleteRoundId,
                        principalTable: "AthleteRounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ClubRoundContexts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ClubRoundId = table.Column<long>(type: "bigint", nullable: false),
                    PreviousOverallAverage = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    RankingsOverallAverage = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    RankingsMovingAverage = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    RankingsHomeAverage = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    RankingsHomeMovingAverage = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    RankingsAwayAverage = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    RankingsAwayMovingAverage = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    GeneralPointsConcededAverage = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    GeneralPointsConqueredAverage = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    HomePointsConcededAverage = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    HomePointsConqueredAverage = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    AwayPointsConcededAverage = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    AwayPointsConqueredAverage = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    HistoricalWinRate = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    HistoricalDrawRate = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    HistoricalLossRate = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    FavoriteWinProbability = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    FavoriteDrawProbability = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    FavoriteLoseProbability = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    ForecastExpectedResult = table.Column<int>(type: "int", nullable: false),
                    ForecastRiskLevel = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TeamTipsCount = table.Column<int>(type: "int", nullable: false),
                    HotTeamTipsCount = table.Column<int>(type: "int", nullable: false),
                    MatchTipsCount = table.Column<int>(type: "int", nullable: false),
                    HotMatchTipsCount = table.Column<int>(type: "int", nullable: false),
                    InsightCount = table.Column<int>(type: "int", nullable: false),
                    HotInsightCount = table.Column<int>(type: "int", nullable: false),
                    AverageInsightConfidence = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    PositiveInsightCount = table.Column<int>(type: "int", nullable: false),
                    NegativeInsightCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubRoundContexts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClubRoundContexts_ClubRounds_ClubRoundId",
                        column: x => x.ClubRoundId,
                        principalTable: "ClubRounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_AthleteRoundContexts_AthleteRoundId",
                table: "AthleteRoundContexts",
                column: "AthleteRoundId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClubRoundContexts_ClubRoundId",
                table: "ClubRoundContexts",
                column: "ClubRoundId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AthleteRoundContexts");

            migrationBuilder.DropTable(
                name: "ClubRoundContexts");

            migrationBuilder.DropColumn(
                name: "DistributionValue",
                table: "ClubRoundPositions");
        }
    }
}
