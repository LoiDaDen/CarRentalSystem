using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarRentalSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddPartnerDriverContracts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DriverContracts",
                columns: table => new
                {
                    DriverContractID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContractNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DriverID = table.Column<int>(type: "int", nullable: false),
                    PartyAName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PartyARepresentative = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PartyAPosition = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PartyAAddress = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    PartyBFullName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PartyBCCCD = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PartyBLicenseType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PartyBLicenseNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PartyBExperienceYears = table.Column<int>(type: "int", nullable: false),
                    AssignedVehiclePlate = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    BaseSalary = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TripFee = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BonusPolicy = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PaymentDay = table.Column<int>(type: "int", nullable: false),
                    TermsContent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SignedByPartyA = table.Column<bool>(type: "bit", nullable: false),
                    PartyASignDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SignedByPartyB = table.Column<bool>(type: "bit", nullable: false),
                    PartyBSignDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DriverContracts", x => x.DriverContractID);
                    table.ForeignKey(
                        name: "FK_DriverContracts_Drivers_DriverID",
                        column: x => x.DriverID,
                        principalTable: "Drivers",
                        principalColumn: "DriverID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PartnerContracts",
                columns: table => new
                {
                    PartnerContractID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContractNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PartnerUserID = table.Column<int>(type: "int", nullable: false),
                    PartyAName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PartyAAddress = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    PartyBFullName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PartyBCCCD = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    VehicleInfo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    VehiclePlate = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    VehicleCondition = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PartyBRevenuePercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    PartyARevenuePercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    PaymentDay = table.Column<int>(type: "int", nullable: false),
                    PaymentCycle = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ContractDurationMonths = table.Column<int>(type: "int", nullable: false),
                    NoticePeriodDays = table.Column<int>(type: "int", nullable: false),
                    TermsContent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SignedByPartyA = table.Column<bool>(type: "bit", nullable: false),
                    PartyASignDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SignedByPartyB = table.Column<bool>(type: "bit", nullable: false),
                    PartyBSignDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartnerContracts", x => x.PartnerContractID);
                    table.ForeignKey(
                        name: "FK_PartnerContracts_Users_PartnerUserID",
                        column: x => x.PartnerUserID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DriverContracts_DriverID",
                table: "DriverContracts",
                column: "DriverID");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerContracts_PartnerUserID",
                table: "PartnerContracts",
                column: "PartnerUserID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DriverContracts");

            migrationBuilder.DropTable(
                name: "PartnerContracts");
        }
    }
}
