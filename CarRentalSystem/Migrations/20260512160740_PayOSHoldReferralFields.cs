using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarRentalSystem.Migrations
{
    /// <inheritdoc />
    public partial class PayOSHoldReferralFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PayOSTransactions_Bookings_BookingID",
                table: "PayOSTransactions");

            migrationBuilder.AddColumn<string>(
                name: "ReferralCode",
                table: "Users",
                type: "nvarchar(6)",
                maxLength: 6,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReferralDiscountUsedAt",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReferredByUserID",
                table: "Users",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "BookingID",
                table: "PayOSTransactions",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "AccountName",
                table: "PayOSTransactions",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccountNumber",
                table: "PayOSTransactions",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Bin",
                table: "PayOSTransactions",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BookingHoldID",
                table: "PayOSTransactions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "PayOSTransactions",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PaymentLinkId",
                table: "PayOSTransactions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QrCode",
                table: "PayOSTransactions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UserID",
                table: "PayOSTransactions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReservationFee",
                table: "Bookings",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_Users_ReferralCode",
                table: "Users",
                column: "ReferralCode",
                unique: true,
                filter: "[ReferralCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Users_ReferredByUserID",
                table: "Users",
                column: "ReferredByUserID");

            migrationBuilder.CreateIndex(
                name: "IX_PayOSTransactions_BookingHoldID",
                table: "PayOSTransactions",
                column: "BookingHoldID");

            migrationBuilder.CreateIndex(
                name: "IX_PayOSTransactions_UserID",
                table: "PayOSTransactions",
                column: "UserID");

            migrationBuilder.AddForeignKey(
                name: "FK_PayOSTransactions_BookingHolds_BookingHoldID",
                table: "PayOSTransactions",
                column: "BookingHoldID",
                principalTable: "BookingHolds",
                principalColumn: "BookingHoldID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PayOSTransactions_Bookings_BookingID",
                table: "PayOSTransactions",
                column: "BookingID",
                principalTable: "Bookings",
                principalColumn: "BookingID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PayOSTransactions_Users_UserID",
                table: "PayOSTransactions",
                column: "UserID",
                principalTable: "Users",
                principalColumn: "UserID");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Users_ReferredByUserID",
                table: "Users",
                column: "ReferredByUserID",
                principalTable: "Users",
                principalColumn: "UserID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PayOSTransactions_BookingHolds_BookingHoldID",
                table: "PayOSTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_PayOSTransactions_Bookings_BookingID",
                table: "PayOSTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_PayOSTransactions_Users_UserID",
                table: "PayOSTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Users_ReferredByUserID",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_ReferralCode",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_ReferredByUserID",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_PayOSTransactions_BookingHoldID",
                table: "PayOSTransactions");

            migrationBuilder.DropIndex(
                name: "IX_PayOSTransactions_UserID",
                table: "PayOSTransactions");

            migrationBuilder.DropColumn(
                name: "ReferralCode",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ReferralDiscountUsedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ReferredByUserID",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AccountName",
                table: "PayOSTransactions");

            migrationBuilder.DropColumn(
                name: "AccountNumber",
                table: "PayOSTransactions");

            migrationBuilder.DropColumn(
                name: "Bin",
                table: "PayOSTransactions");

            migrationBuilder.DropColumn(
                name: "BookingHoldID",
                table: "PayOSTransactions");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "PayOSTransactions");

            migrationBuilder.DropColumn(
                name: "PaymentLinkId",
                table: "PayOSTransactions");

            migrationBuilder.DropColumn(
                name: "QrCode",
                table: "PayOSTransactions");

            migrationBuilder.DropColumn(
                name: "UserID",
                table: "PayOSTransactions");

            migrationBuilder.DropColumn(
                name: "ReservationFee",
                table: "Bookings");

            migrationBuilder.AlterColumn<int>(
                name: "BookingID",
                table: "PayOSTransactions",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PayOSTransactions_Bookings_BookingID",
                table: "PayOSTransactions",
                column: "BookingID",
                principalTable: "Bookings",
                principalColumn: "BookingID",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
