using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CartolaOptimization.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSyncExecutionsAndErrorCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ErrorCode",
                table: "IntegrationFailureLogs",
                type: "varchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Severity",
                table: "IntegrationFailureLogs",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "SyncExecutions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CorrelationId = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SourceSystem = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProcessName = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    FinishedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LastStepName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Season = table.Column<int>(type: "int", nullable: true),
                    Round = table.Column<int>(type: "int", nullable: true),
                    ErrorCode = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Notes = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MetricsJson = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ContextJson = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    QualityCriteriaJson = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CoverageRate = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    SkippedRate = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    QualityCriteriaSatisfied = table.Column<bool>(type: "tinyint(1)", nullable: true),
                    WarningLogsCount = table.Column<int>(type: "int", nullable: false),
                    ErrorLogsCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncExecutions", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationFailureLogs_ErrorCode",
                table: "IntegrationFailureLogs",
                column: "ErrorCode");

            migrationBuilder.CreateIndex(
                name: "IX_SyncExecutions_CorrelationId",
                table: "SyncExecutions",
                column: "CorrelationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SyncExecutions_SourceSystem_Status_StartedAtUtc",
                table: "SyncExecutions",
                columns: new[] { "SourceSystem", "Status", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SyncExecutions_StartedAtUtc",
                table: "SyncExecutions",
                column: "StartedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SyncExecutions");

            migrationBuilder.DropIndex(
                name: "IX_IntegrationFailureLogs_ErrorCode",
                table: "IntegrationFailureLogs");

            migrationBuilder.DropColumn(
                name: "ErrorCode",
                table: "IntegrationFailureLogs");

            migrationBuilder.DropColumn(
                name: "Severity",
                table: "IntegrationFailureLogs");
        }
    }
}
