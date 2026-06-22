using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarRentalSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddRiskRevenueResearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomerRiskAssessments",
                columns: table => new
                {
                    CustomerRiskAssessmentID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserID = table.Column<int>(type: "int", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false),
                    RiskLevel = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ModelVersion = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ReasonCodesJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    FeatureSnapshotJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    AssessedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerRiskAssessments", x => x.CustomerRiskAssessmentID);
                    table.ForeignKey(
                        name: "FK_CustomerRiskAssessments_Users_UserID",
                        column: x => x.UserID,
                        principalTable: "Users",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateTable(
                name: "PartnerPayoutStatements",
                columns: table => new
                {
                    PartnerPayoutStatementID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartnerUserID = table.Column<int>(type: "int", nullable: false),
                    PeriodStartUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodEndUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedBookingCount = table.Column<int>(type: "int", nullable: false),
                    GrossBookingAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PlatformCommissionAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OwnerPayoutAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RefundAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DeductionAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    BookingBreakdownJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PaidAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartnerPayoutStatements", x => x.PartnerPayoutStatementID);
                    table.ForeignKey(
                        name: "FK_PartnerPayoutStatements_Users_PartnerUserID",
                        column: x => x.PartnerUserID,
                        principalTable: "Users",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerRiskAssessments_UserID_AssessedAtUtc",
                table: "CustomerRiskAssessments",
                columns: new[] { "UserID", "AssessedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PartnerPayoutStatements_IdempotencyKey",
                table: "PartnerPayoutStatements",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PartnerPayoutStatements_PartnerUserID_PeriodStartUtc_PeriodEndUtc",
                table: "PartnerPayoutStatements",
                columns: new[] { "PartnerUserID", "PeriodStartUtc", "PeriodEndUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerRiskAssessments");

            migrationBuilder.DropTable(
                name: "PartnerPayoutStatements");
        }
    }
}
