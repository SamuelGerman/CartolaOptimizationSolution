using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CartolaOptimization.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixAthleteRoundShadowForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AthleteRounds_Clubs_ClubId1",
                table: "AthleteRounds");

            migrationBuilder.DropForeignKey(
                name: "FK_AthleteRounds_Positions_PositionId1",
                table: "AthleteRounds");

            migrationBuilder.DropIndex(
                name: "IX_AthleteRounds_ClubId1",
                table: "AthleteRounds");

            migrationBuilder.DropIndex(
                name: "IX_AthleteRounds_PositionId1",
                table: "AthleteRounds");

            migrationBuilder.DropColumn(
                name: "ClubId1",
                table: "AthleteRounds");

            migrationBuilder.DropColumn(
                name: "PositionId1",
                table: "AthleteRounds");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ClubId1",
                table: "AthleteRounds",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PositionId1",
                table: "AthleteRounds",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AthleteRounds_ClubId1",
                table: "AthleteRounds",
                column: "ClubId1");

            migrationBuilder.CreateIndex(
                name: "IX_AthleteRounds_PositionId1",
                table: "AthleteRounds",
                column: "PositionId1");

            migrationBuilder.AddForeignKey(
                name: "FK_AthleteRounds_Clubs_ClubId1",
                table: "AthleteRounds",
                column: "ClubId1",
                principalTable: "Clubs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AthleteRounds_Positions_PositionId1",
                table: "AthleteRounds",
                column: "PositionId1",
                principalTable: "Positions",
                principalColumn: "Id");
        }
    }
}
