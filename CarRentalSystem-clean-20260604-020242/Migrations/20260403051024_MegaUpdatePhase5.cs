using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarRentalSystem.Migrations
{
    /// <inheritdoc />
    public partial class MegaUpdatePhase5 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LoyaltyPoints",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MembershipTierID",
                table: "Users",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalSpending",
                table: "Users",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "BatteryCapacity",
                table: "Cars",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ChargeCyclesEstimate",
                table: "Cars",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsElectric",
                table: "Cars",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "PartnerDiscountPercent",
                table: "Cars",
                type: "decimal(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RangePerCharge",
                table: "Cars",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ContractTypeID",
                table: "Bookings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DriverBookingID",
                table: "Bookings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FuelFee",
                table: "Bookings",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "LateFeeAmount",
                table: "Bookings",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ShippingDistance",
                table: "Bookings",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ShippingFee",
                table: "Bookings",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "ContractTypes",
                columns: table => new
                {
                    ContractTypeID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    MinDurationDays = table.Column<int>(type: "int", nullable: false),
                    MaxDurationDays = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    FreeMaintenanceCount = table.Column<int>(type: "int", nullable: false),
                    FreeInsurance = table.Column<bool>(type: "bit", nullable: false),
                    AfterSalesDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractTypes", x => x.ContractTypeID);
                });

            migrationBuilder.CreateTable(
                name: "Drivers",
                columns: table => new
                {
                    DriverID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserID = table.Column<int>(type: "int", nullable: true),
                    FullName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LicenseNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LicenseType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ExperienceYears = table.Column<int>(type: "int", nullable: false),
                    DailyRate = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    HourlyRate = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsAvailable = table.Column<bool>(type: "bit", nullable: false),
                    IsApproved = table.Column<bool>(type: "bit", nullable: false),
                    Rating = table.Column<decimal>(type: "decimal(3,1)", nullable: false),
                    TotalTrips = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AvatarUrl = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    LicenseFrontUrl = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    LicenseBackUrl = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    IdCardUrl = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    LocationID = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Drivers", x => x.DriverID);
                    table.ForeignKey(
                        name: "FK_Drivers_Locations_LocationID",
                        column: x => x.LocationID,
                        principalTable: "Locations",
                        principalColumn: "LocationID");
                    table.ForeignKey(
                        name: "FK_Drivers_Users_UserID",
                        column: x => x.UserID,
                        principalTable: "Users",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateTable(
                name: "HolidayPricings",
                columns: table => new
                {
                    HolidayPricingID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CarID = table.Column<int>(type: "int", nullable: true),
                    HolidayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PriceMultiplier = table.Column<decimal>(type: "decimal(4,2)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HolidayPricings", x => x.HolidayPricingID);
                    table.ForeignKey(
                        name: "FK_HolidayPricings_Cars_CarID",
                        column: x => x.CarID,
                        principalTable: "Cars",
                        principalColumn: "CarID");
                });

            migrationBuilder.CreateTable(
                name: "MembershipTiers",
                columns: table => new
                {
                    MembershipTierID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MinSpending = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    FreeDeliveryKm = table.Column<int>(type: "int", nullable: false),
                    PrioritySupport = table.Column<bool>(type: "bit", nullable: false),
                    ExtraFreeHours = table.Column<int>(type: "int", nullable: false),
                    Icon = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Color = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MembershipTiers", x => x.MembershipTierID);
                });

            migrationBuilder.CreateTable(
                name: "DriverBookings",
                columns: table => new
                {
                    DriverBookingID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookingID = table.Column<int>(type: "int", nullable: false),
                    DriverID = table.Column<int>(type: "int", nullable: false),
                    DriverFee = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DriverBookings", x => x.DriverBookingID);
                    table.ForeignKey(
                        name: "FK_DriverBookings_Bookings_BookingID",
                        column: x => x.BookingID,
                        principalTable: "Bookings",
                        principalColumn: "BookingID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DriverBookings_Drivers_DriverID",
                        column: x => x.DriverID,
                        principalTable: "Drivers",
                        principalColumn: "DriverID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_MembershipTierID",
                table: "Users",
                column: "MembershipTierID");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_ContractTypeID",
                table: "Bookings",
                column: "ContractTypeID");

            migrationBuilder.CreateIndex(
                name: "IX_DriverBookings_BookingID",
                table: "DriverBookings",
                column: "BookingID");

            migrationBuilder.CreateIndex(
                name: "IX_DriverBookings_DriverID",
                table: "DriverBookings",
                column: "DriverID");

            migrationBuilder.CreateIndex(
                name: "IX_Drivers_LocationID",
                table: "Drivers",
                column: "LocationID");

            migrationBuilder.CreateIndex(
                name: "IX_Drivers_UserID",
                table: "Drivers",
                column: "UserID");

            migrationBuilder.CreateIndex(
                name: "IX_HolidayPricings_CarID",
                table: "HolidayPricings",
                column: "CarID");

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_ContractTypes_ContractTypeID",
                table: "Bookings",
                column: "ContractTypeID",
                principalTable: "ContractTypes",
                principalColumn: "ContractTypeID");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_MembershipTiers_MembershipTierID",
                table: "Users",
                column: "MembershipTierID",
                principalTable: "MembershipTiers",
                principalColumn: "MembershipTierID",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_ContractTypes_ContractTypeID",
                table: "Bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_MembershipTiers_MembershipTierID",
                table: "Users");

            migrationBuilder.DropTable(
                name: "ContractTypes");

            migrationBuilder.DropTable(
                name: "DriverBookings");

            migrationBuilder.DropTable(
                name: "HolidayPricings");

            migrationBuilder.DropTable(
                name: "MembershipTiers");

            migrationBuilder.DropTable(
                name: "Drivers");

            migrationBuilder.DropIndex(
                name: "IX_Users_MembershipTierID",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_ContractTypeID",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "LoyaltyPoints",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "MembershipTierID",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TotalSpending",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "BatteryCapacity",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "ChargeCyclesEstimate",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "IsElectric",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "PartnerDiscountPercent",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "RangePerCharge",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "ContractTypeID",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "DriverBookingID",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "FuelFee",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "LateFeeAmount",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "ShippingDistance",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "ShippingFee",
                table: "Bookings");
        }
    }
}
