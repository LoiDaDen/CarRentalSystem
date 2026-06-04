using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarRentalSystem.Migrations
{
    /// <inheritdoc />
    public partial class FixModelMappings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Cars_CarID",
                table: "Bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Users_UserID",
                table: "Bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_Cars_Locations_LocationID",
                table: "Cars");

            migrationBuilder.DropForeignKey(
                name: "FK_Reviews_Users_UserID1",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_Reviews_UserID1",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "UserID1",
                table: "Reviews");

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Cars_CarID",
                table: "Bookings",
                column: "CarID",
                principalTable: "Cars",
                principalColumn: "CarID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Users_UserID",
                table: "Bookings",
                column: "UserID",
                principalTable: "Users",
                principalColumn: "UserID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Cars_Locations_LocationID",
                table: "Cars",
                column: "LocationID",
                principalTable: "Locations",
                principalColumn: "LocationID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Cars_CarID",
                table: "Bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Users_UserID",
                table: "Bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_Cars_Locations_LocationID",
                table: "Cars");

            migrationBuilder.AddColumn<int>(
                name: "UserID1",
                table: "Reviews",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_UserID1",
                table: "Reviews",
                column: "UserID1");

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Cars_CarID",
                table: "Bookings",
                column: "CarID",
                principalTable: "Cars",
                principalColumn: "CarID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Users_UserID",
                table: "Bookings",
                column: "UserID",
                principalTable: "Users",
                principalColumn: "UserID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Cars_Locations_LocationID",
                table: "Cars",
                column: "LocationID",
                principalTable: "Locations",
                principalColumn: "LocationID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Reviews_Users_UserID1",
                table: "Reviews",
                column: "UserID1",
                principalTable: "Users",
                principalColumn: "UserID");
        }
    }
}
