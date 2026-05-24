using System;
using CarRentalSystem.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarRentalSystem.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260522161000_AddCameraAnprIntegration")]
    public partial class AddCameraAnprIntegration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LicensePlateNormalized",
                table: "Cars",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.Sql(@"
UPDATE Cars
SET LicensePlateNormalized = UPPER(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(LicensePlate, '-', ''), '.', ''), ' ', ''), CHAR(9), ''), CHAR(160), ''))
WHERE LicensePlate IS NOT NULL AND LicensePlateNormalized IS NULL;
");

            migrationBuilder.CreateTable(
                name: "CameraDevices",
                columns: table => new
                {
                    CameraDeviceID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    AreaID = table.Column<int>(type: "int", nullable: true),
                    LocationID = table.Column<int>(type: "int", nullable: true),
                    DefaultDirection = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    LiveViewUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EncryptedWebhookSecret = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CameraDevices", x => x.CameraDeviceID);
                    table.ForeignKey(
                        name: "FK_CameraDevices_Areas_AreaID",
                        column: x => x.AreaID,
                        principalTable: "Areas",
                        principalColumn: "AreaID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CameraDevices_Locations_LocationID",
                        column: x => x.LocationID,
                        principalTable: "Locations",
                        principalColumn: "LocationID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CameraGateEvents",
                columns: table => new
                {
                    CameraGateEventID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CameraDeviceID = table.Column<int>(type: "int", nullable: false),
                    ProviderEventId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    LicensePlateRaw = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    LicensePlateNormalized = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Direction = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Confidence = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    CapturedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SnapshotUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CarID = table.Column<int>(type: "int", nullable: true),
                    AreaID = table.Column<int>(type: "int", nullable: true),
                    LocationID = table.Column<int>(type: "int", nullable: true),
                    MatchStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    FailureReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CameraGateEvents", x => x.CameraGateEventID);
                    table.ForeignKey(
                        name: "FK_CameraGateEvents_Areas_AreaID",
                        column: x => x.AreaID,
                        principalTable: "Areas",
                        principalColumn: "AreaID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CameraGateEvents_CameraDevices_CameraDeviceID",
                        column: x => x.CameraDeviceID,
                        principalTable: "CameraDevices",
                        principalColumn: "CameraDeviceID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CameraGateEvents_Cars_CarID",
                        column: x => x.CarID,
                        principalTable: "Cars",
                        principalColumn: "CarID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CameraGateEvents_Locations_LocationID",
                        column: x => x.LocationID,
                        principalTable: "Locations",
                        principalColumn: "LocationID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CameraWebhookNonces",
                columns: table => new
                {
                    CameraWebhookNonceID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CameraDeviceID = table.Column<int>(type: "int", nullable: false),
                    Nonce = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CameraWebhookNonces", x => x.CameraWebhookNonceID);
                    table.ForeignKey(
                        name: "FK_CameraWebhookNonces_CameraDevices_CameraDeviceID",
                        column: x => x.CameraDeviceID,
                        principalTable: "CameraDevices",
                        principalColumn: "CameraDeviceID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VehicleAreaPresences",
                columns: table => new
                {
                    VehicleAreaPresenceID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CarID = table.Column<int>(type: "int", nullable: false),
                    AreaID = table.Column<int>(type: "int", nullable: true),
                    LocationID = table.Column<int>(type: "int", nullable: true),
                    IsInside = table.Column<bool>(type: "bit", nullable: false),
                    LastDirection = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    LastEventAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastCameraGateEventID = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleAreaPresences", x => x.VehicleAreaPresenceID);
                    table.ForeignKey(
                        name: "FK_VehicleAreaPresences_Areas_AreaID",
                        column: x => x.AreaID,
                        principalTable: "Areas",
                        principalColumn: "AreaID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VehicleAreaPresences_CameraGateEvents_LastCameraGateEventID",
                        column: x => x.LastCameraGateEventID,
                        principalTable: "CameraGateEvents",
                        principalColumn: "CameraGateEventID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VehicleAreaPresences_Cars_CarID",
                        column: x => x.CarID,
                        principalTable: "Cars",
                        principalColumn: "CarID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VehicleAreaPresences_Locations_LocationID",
                        column: x => x.LocationID,
                        principalTable: "Locations",
                        principalColumn: "LocationID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(name: "IX_Cars_LicensePlateNormalized", table: "Cars", column: "LicensePlateNormalized");
            migrationBuilder.CreateIndex(name: "IX_CameraDevices_AreaID_LocationID_Status", table: "CameraDevices", columns: new[] { "AreaID", "LocationID", "Status" });
            migrationBuilder.CreateIndex(name: "IX_CameraDevices_Code", table: "CameraDevices", column: "Code", unique: true);
            migrationBuilder.CreateIndex(name: "IX_CameraDevices_LocationID", table: "CameraDevices", column: "LocationID");
            migrationBuilder.CreateIndex(name: "IX_CameraGateEvents_AreaID_LocationID_CapturedAt", table: "CameraGateEvents", columns: new[] { "AreaID", "LocationID", "CapturedAt" });
            migrationBuilder.CreateIndex(name: "IX_CameraGateEvents_CameraDeviceID_ProviderEventId", table: "CameraGateEvents", columns: new[] { "CameraDeviceID", "ProviderEventId" }, unique: true, filter: "[ProviderEventId] IS NOT NULL");
            migrationBuilder.CreateIndex(name: "IX_CameraGateEvents_CarID", table: "CameraGateEvents", column: "CarID");
            migrationBuilder.CreateIndex(name: "IX_CameraGateEvents_LicensePlateNormalized", table: "CameraGateEvents", column: "LicensePlateNormalized");
            migrationBuilder.CreateIndex(name: "IX_CameraGateEvents_LocationID", table: "CameraGateEvents", column: "LocationID");
            migrationBuilder.CreateIndex(name: "IX_CameraGateEvents_MatchStatus_CapturedAt", table: "CameraGateEvents", columns: new[] { "MatchStatus", "CapturedAt" });
            migrationBuilder.CreateIndex(name: "IX_CameraWebhookNonces_CameraDeviceID_Nonce", table: "CameraWebhookNonces", columns: new[] { "CameraDeviceID", "Nonce" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_CameraWebhookNonces_CreatedAt", table: "CameraWebhookNonces", column: "CreatedAt");
            migrationBuilder.CreateIndex(name: "IX_VehicleAreaPresences_AreaID_LocationID_IsInside", table: "VehicleAreaPresences", columns: new[] { "AreaID", "LocationID", "IsInside" });
            migrationBuilder.CreateIndex(name: "IX_VehicleAreaPresences_CarID_AreaID", table: "VehicleAreaPresences", columns: new[] { "CarID", "AreaID" }, unique: true, filter: "[AreaID] IS NOT NULL AND [LocationID] IS NULL");
            migrationBuilder.CreateIndex(name: "IX_VehicleAreaPresences_CarID_AreaID_LocationID", table: "VehicleAreaPresences", columns: new[] { "CarID", "AreaID", "LocationID" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_VehicleAreaPresences_CarID_LocationID", table: "VehicleAreaPresences", columns: new[] { "CarID", "LocationID" }, unique: true, filter: "[AreaID] IS NULL AND [LocationID] IS NOT NULL");
            migrationBuilder.CreateIndex(name: "IX_VehicleAreaPresences_LastCameraGateEventID", table: "VehicleAreaPresences", column: "LastCameraGateEventID");
            migrationBuilder.CreateIndex(name: "IX_VehicleAreaPresences_LocationID", table: "VehicleAreaPresences", column: "LocationID");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "VehicleAreaPresences");
            migrationBuilder.DropTable(name: "CameraWebhookNonces");
            migrationBuilder.DropTable(name: "CameraGateEvents");
            migrationBuilder.DropTable(name: "CameraDevices");
            migrationBuilder.DropIndex(name: "IX_Cars_LicensePlateNormalized", table: "Cars");
            migrationBuilder.DropColumn(name: "LicensePlateNormalized", table: "Cars");
        }
    }
}
