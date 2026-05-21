using System;
using CarRentalSystem.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarRentalSystem.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260521120000_AddBookingDeliveryTrackingFields")]
    public partial class AddBookingDeliveryTrackingFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeliveryMode",
                table: "Bookings",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "CustomerPickup");

            migrationBuilder.AddColumn<double>(
                name: "DeliveryLatitude",
                table: "Bookings",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "DeliveryLongitude",
                table: "Bookings",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DeliveryDistanceKm",
                table: "Bookings",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "DeliveryEtaMinutes",
                table: "Bookings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeliveryQuotedAt",
                table: "Bookings",
                type: "datetime2",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "DeliveryMode", table: "Bookings");
            migrationBuilder.DropColumn(name: "DeliveryLatitude", table: "Bookings");
            migrationBuilder.DropColumn(name: "DeliveryLongitude", table: "Bookings");
            migrationBuilder.DropColumn(name: "DeliveryDistanceKm", table: "Bookings");
            migrationBuilder.DropColumn(name: "DeliveryEtaMinutes", table: "Bookings");
            migrationBuilder.DropColumn(name: "DeliveryQuotedAt", table: "Bookings");
        }
    }
}
