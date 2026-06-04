using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarRentalSystem.Migrations
{
    /// <inheritdoc />
    public partial class CartItem_DriverID : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DriverID",
                table: "CartItems",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CartItems_DriverID",
                table: "CartItems",
                column: "DriverID");

            migrationBuilder.AddForeignKey(
                name: "FK_CartItems_Drivers_DriverID",
                table: "CartItems",
                column: "DriverID",
                principalTable: "Drivers",
                principalColumn: "DriverID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CartItems_Drivers_DriverID",
                table: "CartItems");

            migrationBuilder.DropIndex(
                name: "IX_CartItems_DriverID",
                table: "CartItems");

            migrationBuilder.DropColumn(
                name: "DriverID",
                table: "CartItems");
        }
    }
}
