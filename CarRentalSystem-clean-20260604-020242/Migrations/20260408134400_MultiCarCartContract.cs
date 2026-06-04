using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarRentalSystem.Migrations
{
    /// <inheritdoc />
    public partial class MultiCarCartContract : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DigitalContracts_Bookings_BookingID",
                table: "DigitalContracts");

            migrationBuilder.DropIndex(
                name: "IX_DigitalContracts_BookingID",
                table: "DigitalContracts");

            migrationBuilder.AlterColumn<string>(
                name: "CarModel",
                table: "DigitalContracts",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "CarLicensePlate",
                table: "DigitalContracts",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<int>(
                name: "BookingID",
                table: "DigitalContracts",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "TransactionID",
                table: "DigitalContracts",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DigitalContracts_BookingID",
                table: "DigitalContracts",
                column: "BookingID",
                unique: true,
                filter: "[BookingID] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DigitalContracts_TransactionID",
                table: "DigitalContracts",
                column: "TransactionID",
                unique: true,
                filter: "[TransactionID] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_DigitalContracts_BookingTransactions_TransactionID",
                table: "DigitalContracts",
                column: "TransactionID",
                principalTable: "BookingTransactions",
                principalColumn: "TransactionID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DigitalContracts_Bookings_BookingID",
                table: "DigitalContracts",
                column: "BookingID",
                principalTable: "Bookings",
                principalColumn: "BookingID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DigitalContracts_BookingTransactions_TransactionID",
                table: "DigitalContracts");

            migrationBuilder.DropForeignKey(
                name: "FK_DigitalContracts_Bookings_BookingID",
                table: "DigitalContracts");

            migrationBuilder.DropIndex(
                name: "IX_DigitalContracts_BookingID",
                table: "DigitalContracts");

            migrationBuilder.DropIndex(
                name: "IX_DigitalContracts_TransactionID",
                table: "DigitalContracts");

            migrationBuilder.DropColumn(
                name: "TransactionID",
                table: "DigitalContracts");

            migrationBuilder.AlterColumn<string>(
                name: "CarModel",
                table: "DigitalContracts",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AlterColumn<string>(
                name: "CarLicensePlate",
                table: "DigitalContracts",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AlterColumn<int>(
                name: "BookingID",
                table: "DigitalContracts",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DigitalContracts_BookingID",
                table: "DigitalContracts",
                column: "BookingID",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_DigitalContracts_Bookings_BookingID",
                table: "DigitalContracts",
                column: "BookingID",
                principalTable: "Bookings",
                principalColumn: "BookingID",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
