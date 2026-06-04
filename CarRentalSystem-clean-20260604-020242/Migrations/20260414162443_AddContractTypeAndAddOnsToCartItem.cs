using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarRentalSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddContractTypeAndAddOnsToCartItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ContractTypeID",
                table: "CartItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SelectedAddOns",
                table: "CartItems",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CartItems_ContractTypeID",
                table: "CartItems",
                column: "ContractTypeID");

            migrationBuilder.AddForeignKey(
                name: "FK_CartItems_ContractTypes_ContractTypeID",
                table: "CartItems",
                column: "ContractTypeID",
                principalTable: "ContractTypes",
                principalColumn: "ContractTypeID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CartItems_ContractTypes_ContractTypeID",
                table: "CartItems");

            migrationBuilder.DropIndex(
                name: "IX_CartItems_ContractTypeID",
                table: "CartItems");

            migrationBuilder.DropColumn(
                name: "ContractTypeID",
                table: "CartItems");

            migrationBuilder.DropColumn(
                name: "SelectedAddOns",
                table: "CartItems");
        }
    }
}
