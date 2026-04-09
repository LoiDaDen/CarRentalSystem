using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarRentalSystem.Migrations
{
    /// <inheritdoc />
    public partial class DigitalContract_CorporateFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomerBankAccount",
                table: "DigitalContracts",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerCompanyName",
                table: "DigitalContracts",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerDepartmentCode",
                table: "DigitalContracts",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                table: "DigitalContracts",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomerBankAccount",
                table: "DigitalContracts");

            migrationBuilder.DropColumn(
                name: "CustomerCompanyName",
                table: "DigitalContracts");

            migrationBuilder.DropColumn(
                name: "CustomerDepartmentCode",
                table: "DigitalContracts");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "DigitalContracts");
        }
    }
}
