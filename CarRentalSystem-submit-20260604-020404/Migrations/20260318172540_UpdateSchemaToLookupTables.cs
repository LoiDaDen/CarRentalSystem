using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarRentalSystem.Migrations
{
    /// <inheritdoc />
    public partial class UpdateSchemaToLookupTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Category",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "PickupLocation",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "ReturnLocation",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Bookings");

            migrationBuilder.AddColumn<int>(
                name: "CarStatusID",
                table: "Cars",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CarTypeID",
                table: "Cars",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LocationID",
                table: "Cars",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BookingStatusID",
                table: "Bookings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PickupLocationID",
                table: "Bookings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ReturnLocationID",
                table: "Bookings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "BookingStatuses",
                columns: table => new
                {
                    BookingStatusID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingStatuses", x => x.BookingStatusID);
                });

            migrationBuilder.CreateTable(
                name: "CarStatuses",
                columns: table => new
                {
                    CarStatusID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CarStatuses", x => x.CarStatusID);
                });

            migrationBuilder.CreateTable(
                name: "CarTypes",
                columns: table => new
                {
                    CarTypeID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CarTypes", x => x.CarTypeID);
                });

            migrationBuilder.CreateTable(
                name: "Locations",
                columns: table => new
                {
                    LocationID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Locations", x => x.LocationID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Cars_CarStatusID",
                table: "Cars",
                column: "CarStatusID");

            migrationBuilder.CreateIndex(
                name: "IX_Cars_CarTypeID",
                table: "Cars",
                column: "CarTypeID");

            migrationBuilder.CreateIndex(
                name: "IX_Cars_LocationID",
                table: "Cars",
                column: "LocationID");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_BookingStatusID",
                table: "Bookings",
                column: "BookingStatusID");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_PickupLocationID",
                table: "Bookings",
                column: "PickupLocationID");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_ReturnLocationID",
                table: "Bookings",
                column: "ReturnLocationID");

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_BookingStatuses_BookingStatusID",
                table: "Bookings",
                column: "BookingStatusID",
                principalTable: "BookingStatuses",
                principalColumn: "BookingStatusID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Locations_PickupLocationID",
                table: "Bookings",
                column: "PickupLocationID",
                principalTable: "Locations",
                principalColumn: "LocationID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Locations_ReturnLocationID",
                table: "Bookings",
                column: "ReturnLocationID",
                principalTable: "Locations",
                principalColumn: "LocationID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Cars_CarStatuses_CarStatusID",
                table: "Cars",
                column: "CarStatusID",
                principalTable: "CarStatuses",
                principalColumn: "CarStatusID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Cars_CarTypes_CarTypeID",
                table: "Cars",
                column: "CarTypeID",
                principalTable: "CarTypes",
                principalColumn: "CarTypeID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Cars_Locations_LocationID",
                table: "Cars",
                column: "LocationID",
                principalTable: "Locations",
                principalColumn: "LocationID",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_BookingStatuses_BookingStatusID",
                table: "Bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Locations_PickupLocationID",
                table: "Bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Locations_ReturnLocationID",
                table: "Bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_Cars_CarStatuses_CarStatusID",
                table: "Cars");

            migrationBuilder.DropForeignKey(
                name: "FK_Cars_CarTypes_CarTypeID",
                table: "Cars");

            migrationBuilder.DropForeignKey(
                name: "FK_Cars_Locations_LocationID",
                table: "Cars");

            migrationBuilder.DropTable(
                name: "BookingStatuses");

            migrationBuilder.DropTable(
                name: "CarStatuses");

            migrationBuilder.DropTable(
                name: "CarTypes");

            migrationBuilder.DropTable(
                name: "Locations");

            migrationBuilder.DropIndex(
                name: "IX_Cars_CarStatusID",
                table: "Cars");

            migrationBuilder.DropIndex(
                name: "IX_Cars_CarTypeID",
                table: "Cars");

            migrationBuilder.DropIndex(
                name: "IX_Cars_LocationID",
                table: "Cars");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_BookingStatusID",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_PickupLocationID",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_ReturnLocationID",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "CarStatusID",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "CarTypeID",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "LocationID",
                table: "Cars");

            migrationBuilder.DropColumn(
                name: "BookingStatusID",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "PickupLocationID",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "ReturnLocationID",
                table: "Bookings");

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Cars",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "Cars",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Cars",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PickupLocation",
                table: "Bookings",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ReturnLocation",
                table: "Bookings",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Bookings",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");
        }
    }
}
