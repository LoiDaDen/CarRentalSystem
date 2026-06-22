using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarRentalSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddMobileGpsTrackerTelemetry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "AccuracyMeters",
                table: "VehicleTrackings",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BatteryLevel",
                table: "VehicleTrackings",
                type: "decimal(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeviceLabel",
                table: "VehicleTrackings",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Heading",
                table: "VehicleTrackings",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCharging",
                table: "VehicleTrackings",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "VehicleTrackings",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "HardwareGps");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleTrackings_CarID_Timestamp",
                table: "VehicleTrackings",
                columns: new[] { "CarID", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VehicleTrackings_CarID_Timestamp",
                table: "VehicleTrackings");

            migrationBuilder.DropColumn(
                name: "AccuracyMeters",
                table: "VehicleTrackings");

            migrationBuilder.DropColumn(
                name: "BatteryLevel",
                table: "VehicleTrackings");

            migrationBuilder.DropColumn(
                name: "DeviceLabel",
                table: "VehicleTrackings");

            migrationBuilder.DropColumn(
                name: "Heading",
                table: "VehicleTrackings");

            migrationBuilder.DropColumn(
                name: "IsCharging",
                table: "VehicleTrackings");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "VehicleTrackings");
        }
    }
}
