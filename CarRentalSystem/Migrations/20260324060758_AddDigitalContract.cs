using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarRentalSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddDigitalContract : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DigitalContracts",
                columns: table => new
                {
                    DigitalContractID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContractNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BookingID = table.Column<int>(type: "int", nullable: false),
                    CustomerName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CustomerIdentityCard = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CustomerPhone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CustomerAddress = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    CarLicensePlate = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CarModel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RentalStartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RentalEndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RentalPricePerDay = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalContractValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DepositAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TermsAndConditions = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsSignedByCustomer = table.Column<bool>(type: "bit", nullable: false),
                    CustomerSignDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsSignedByOwner = table.Column<bool>(type: "bit", nullable: false),
                    OwnerSignDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DigitalContracts", x => x.DigitalContractID);
                    table.ForeignKey(
                        name: "FK_DigitalContracts_Bookings_BookingID",
                        column: x => x.BookingID,
                        principalTable: "Bookings",
                        principalColumn: "BookingID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DigitalContracts_BookingID",
                table: "DigitalContracts",
                column: "BookingID",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DigitalContracts");
        }
    }
}
