using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarRentalSystem.Migrations
{
    /// <inheritdoc />
    public partial class Phase2Risk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BlacklistReason",
                table: "Users",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DrivingLicenseUrl",
                table: "Users",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdCardBackUrl",
                table: "Users",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdCardFrontUrl",
                table: "Users",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsBlacklisted",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsVerified",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "CleaningFee",
                table: "Cars",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DefaultDeposit",
                table: "Cars",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OverMileageFee",
                table: "Cars",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OverTimeFee",
                table: "Cars",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "ActualReturnDate",
                table: "Bookings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DepositAmount",
                table: "Bookings",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "DepositStatus",
                table: "Bookings",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "HandoverRecords",
                columns: table => new
                {
                    HandoverID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookingID = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RecordDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Odo = table.Column<int>(type: "int", nullable: false),
                    FuelLevel = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    FrontImgUrl = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    BackImgUrl = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    LeftImgUrl = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    RightImgUrl = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    InsideImgUrl = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HandoverRecords", x => x.HandoverID);
                    table.ForeignKey(
                        name: "FK_HandoverRecords_Bookings_BookingID",
                        column: x => x.BookingID,
                        principalTable: "Bookings",
                        principalColumn: "BookingID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HandoverRecords_BookingID",
                table: "HandoverRecords",
                column: "BookingID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HandoverRecords");

            migrationBuilder.DropColumn(
                name: "BlacklistReason",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DrivingLicenseUrl",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IdCardBackUrl",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IdCardFrontUrl",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsBlacklisted",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsVerified",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CleaningFee",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "DefaultDeposit",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "OverMileageFee",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "OverTimeFee",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "ActualReturnDate",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "DepositAmount",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "DepositStatus",
                table: "Bookings");
        }
    }
}
