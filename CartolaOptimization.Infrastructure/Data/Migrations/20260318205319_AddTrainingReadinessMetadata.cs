using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CartolaOptimization.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTrainingReadinessMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsTrainingEligible",
                table: "ClubRounds",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TrainingReadinessReason",
                table: "ClubRounds",
                type: "varchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "TrainingReadinessStatus",
                table: "ClubRounds",
                type: "varchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "DataPhase",
                table: "AthleteRounds",
                type: "varchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "IsTrainingEligible",
                table: "AthleteRounds",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TrainingReadinessReason",
                table: "AthleteRounds",
                type: "varchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "TrainingReadinessStatus",
                table: "AthleteRounds",
                type: "varchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ClubRounds_Round_IsTrainingEligible",
                table: "ClubRounds",
                columns: new[] { "Round", "IsTrainingEligible" });

            migrationBuilder.CreateIndex(
                name: "IX_ClubRounds_TrainingReadinessStatus",
                table: "ClubRounds",
                column: "TrainingReadinessStatus");

            migrationBuilder.CreateIndex(
                name: "IX_AthleteRounds_RoundId_IsTrainingEligible",
                table: "AthleteRounds",
                columns: new[] { "RoundId", "IsTrainingEligible" });

            migrationBuilder.CreateIndex(
                name: "IX_AthleteRounds_TrainingReadinessStatus",
                table: "AthleteRounds",
                column: "TrainingReadinessStatus");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ClubRounds_Round_IsTrainingEligible",
                table: "ClubRounds");

            migrationBuilder.DropIndex(
                name: "IX_ClubRounds_TrainingReadinessStatus",
                table: "ClubRounds");

            migrationBuilder.DropIndex(
                name: "IX_AthleteRounds_RoundId_IsTrainingEligible",
                table: "AthleteRounds");

            migrationBuilder.DropIndex(
                name: "IX_AthleteRounds_TrainingReadinessStatus",
                table: "AthleteRounds");

            migrationBuilder.DropColumn(
                name: "IsTrainingEligible",
                table: "ClubRounds");

            migrationBuilder.DropColumn(
                name: "TrainingReadinessReason",
                table: "ClubRounds");

            migrationBuilder.DropColumn(
                name: "TrainingReadinessStatus",
                table: "ClubRounds");

            migrationBuilder.DropColumn(
                name: "DataPhase",
                table: "AthleteRounds");

            migrationBuilder.DropColumn(
                name: "IsTrainingEligible",
                table: "AthleteRounds");

            migrationBuilder.DropColumn(
                name: "TrainingReadinessReason",
                table: "AthleteRounds");

            migrationBuilder.DropColumn(
                name: "TrainingReadinessStatus",
                table: "AthleteRounds");
        }
    }
}
