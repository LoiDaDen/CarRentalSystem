using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarRentalSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddP2PModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CommissionRate",
                table: "Cars",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "OwnerID",
                table: "Cars",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cars_OwnerID",
                table: "Cars",
                column: "OwnerID");

            migrationBuilder.AddForeignKey(
                name: "FK_Cars_Users_OwnerID",
                table: "Cars",
                column: "OwnerID",
                principalTable: "Users",
                principalColumn: "UserID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cars_Users_OwnerID",
                table: "Cars");

            migrationBuilder.DropIndex(
                name: "IX_Cars_OwnerID",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "CommissionRate",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "OwnerID",
                table: "Cars");
        }
    }
}
